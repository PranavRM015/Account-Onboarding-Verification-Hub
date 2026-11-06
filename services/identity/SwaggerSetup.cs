using Microsoft.OpenApi;

// Swagger UI for trying the API by hand. Off unless Swagger:Enabled is true (docker-compose turns it on for local
// runs) or the app runs in Development, so a deployed environment never serves it by accident.
public static class SwaggerSetup
{
    public static IServiceCollection AddHubSwagger(this IServiceCollection services, string title, bool internalKey = false)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = title, Version = "v1" });

            // Log in through Identity, then paste the "token" from the login response here (without the word Bearer).
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "The token returned by POST /api/admins/login (or /api/users/login) on the Identity service.",
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>(),
            });

            if (internalKey)
            {
                options.AddSecurityDefinition("InternalKey", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = "X-Internal-Key",
                    Description = "The shared INTERNAL_API_KEY. Only the /internal endpoints need it.",
                });
                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("InternalKey", document)] = new List<string>(),
                });
            }
        });
        return services;
    }

    public static WebApplication UseHubSwagger(this WebApplication app)
    {
        if (app.Configuration.GetValue("Swagger:Enabled", false) || app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(ui => ui.EnableTryItOutByDefault());
        }
        return app;
    }
}
