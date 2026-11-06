using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Intake.Authentication
{
    public static class IdentityAuthentication
    {
        public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var url = configuration["Identity:JwksUrl"];
            bool requireHttps = configuration.GetValue("Identity:RequireHttps", true);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var address)
                || (address.Scheme != Uri.UriSchemeHttps && address.Scheme != Uri.UriSchemeHttp)
                || (requireHttps && address.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("Set Identity:JwksUrl to the Identity service's JWKS URL. HTTPS is required unless Identity:RequireHttps is explicitly false.");
            }
            string issuer = configuration["Identity:Issuer"] ?? string.Empty;
            string audience = configuration["Identity:Audience"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            {
                throw new InvalidOperationException("Identity:Issuer and Identity:Audience are required.");
            }

            services.AddHttpClient("IdentityKeys", client => client.Timeout = TimeSpan.FromSeconds(5));
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IHttpClientFactory>((options, clients) =>
                {
                    options.MapInboundClaims = false;
                    options.IncludeErrorDetails = false;
                    options.RefreshOnIssuerKeyNotFound = true;
                    var keys = new IdentityKeysRetriever();
                    // Identity exposes JWKS directly, not /.well-known/openid-configuration.
                    options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                        address.AbsoluteUri,
                        keys,
                        new HttpDocumentRetriever(clients.CreateClient("IdentityKeys")) { RequireHttps = requireHttps });
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        ValidateAudience = true,
                        ValidAudience = audience,
                        ValidateIssuerSigningKey = true,
                        RequireSignedTokens = true,
                        ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        RoleClaimType = "role",
                        NameClaimType = "name",
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            // No usable cached keys and Identity cannot supply them: treat as a service outage.
                            if (keys.Unavailable)
                            {
                                context.HttpContext.Items["IdentityKeysUnavailable"] = true;
                                context.NoResult();
                            }
                            return Task.CompletedTask;
                        },
                        OnChallenge = async context =>
                        {
                            if (context.HttpContext.Items.ContainsKey("IdentityKeysUnavailable"))
                            {
                                context.HandleResponse();
                                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                                await context.Response.WriteAsJsonAsync(new ProblemDetails
                                {
                                    Status = StatusCodes.Status503ServiceUnavailable,
                                    Title = "Identity signing keys are unavailable. Try again shortly.",
                                }, options: null, contentType: "application/problem+json", cancellationToken: context.HttpContext.RequestAborted);
                            }
                        },
                    };
                });
            services.AddAuthorization();
            return services;
        }
    }
}
