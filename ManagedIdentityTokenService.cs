using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SureStacks.O365Logs2LA
{
    public class ManagedIdentityTokenService : IManagedIdentityTokenService
    {
        private readonly ILogger _logger;
        // declare a cache for tokens for different ressources
        private readonly Dictionary<string, string> _tokenCache = new();

        public ManagedIdentityTokenService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ManagedIdentityTokenService>();
            // add a token to cache for manage.office.com
            _logger.LogInformation($"Auth: Getting new token for 'manage.office.com'.");
            _tokenCache.Add("manage.office.com", RawGetToken("manage.office.com"));
            // add a token to cache for monitor.azure.com
            _logger.LogInformation($"Auth: Getting new token for 'monitor.azure.com'.");
            _tokenCache.Add("monitor.azure.com", RawGetToken("monitor.azure.com"));
        }
        
        private static string RawGetToken(string resource = "manage.office.com") {
            var defaultAzureCredential = new DefaultAzureCredential();
            var token = defaultAzureCredential.GetToken(new TokenRequestContext(new[] { $"https://{resource}/.default" }));
            return token.Token;
        }

        private static async Task<string> RawGetTokenAsync(string resource = "manage.office.com") {
            var defaultAzureCredential = new DefaultAzureCredential();
            var token = await defaultAzureCredential.GetTokenAsync(new TokenRequestContext(new[] { $"https://{resource}/.default" }));
            return token.Token;
        }

        public async Task<string> GetToken(string resource = "manage.office.com")
        {
            // get the token from cache
            if (!_tokenCache.TryGetValue(resource, out var token)) {
                _logger.LogInformation($"Auth: Retrieving new token for '{resource}'.");
                // get a new token
                token = await RawGetTokenAsync(resource);
                // add the token to cache
                _tokenCache.Add(resource, token);
            }
            // Decode the JWT
            var handler = new JsonWebTokenHandler();
            var jsonWebToken = handler.ReadJsonWebToken(token);
            // Check if the token is expired
            if (jsonWebToken.ValidTo < DateTime.UtcNow.AddMinutes(-10))
            {
                _logger.LogInformation($"Auth: Refreshing token for '{resource}'.");
                // get a new token
                token = await RawGetTokenAsync(resource);
                // update the token in cache
                _tokenCache[resource] = token;
            }
            // Access the claims
            var claims = jsonWebToken.Claims;
            var aud = claims.First(c => c.Type == "aud").Value;
            var iss = claims.First(c => c.Type == "iss").Value;
            _logger.LogInformation($"Retrieved token for {aud} from {iss}.");
            return token;
        }

        public async Task InvalidateToken(string resource = "manage.office.com")
        {
            _logger.LogInformation($"Auth: Invalidating token for '{resource}'.");
            // remove the token from cache
            _tokenCache.Remove(resource);
            // get a new token for the resource but don' block to wait for it
            await RawGetTokenAsync(resource).ConfigureAwait(false);
        }   
    }
}