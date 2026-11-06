using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Intake.Authentication
{
    public class IdentityKeysRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        private volatile bool _hasKeys;
        private volatile bool _fetchFailed;
        public bool Unavailable => _fetchFailed && !_hasKeys;

        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
        {
            try
            {
                string json = await retriever.GetDocumentAsync(address, cancel);
                var keys = new JsonWebKeySet(json).GetSigningKeys();
                if (keys.Count == 0)
                {
                    throw new InvalidOperationException("Identity published no usable signing keys.");
                }
                var configuration = new OpenIdConnectConfiguration();
                foreach (var key in keys)
                {
                    configuration.SigningKeys.Add(key);
                }
                _hasKeys = true;
                _fetchFailed = false;
                return configuration;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancel.IsCancellationRequested)
            {
                _fetchFailed = true;
                throw new InvalidOperationException("Could not retrieve Identity's public signing keys.", ex);
            }
        }

    }
}
