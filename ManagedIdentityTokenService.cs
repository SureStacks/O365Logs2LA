using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SureStacks.O365Logs2LA
{
    public class ManagedIdentityTokenService : IManagedIdentityTokenService
    {
        private readonly ILogger _logger;

        public ManagedIdentityTokenService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ManagedIdentityTokenService>();
        }
        public async Task<string> GetToken(string resource = "manage.office.com")
        {
            var defaultAzureCredential = new DefaultAzureCredential();
            var token = await defaultAzureCredential.GetTokenAsync(new TokenRequestContext(new[] { $"https://{resource}/.default" }));
            // Decode the JWT
            var handler = new JsonWebTokenHandler();
            var jsonWebToken = handler.ReadJsonWebToken(token.Token);
            // Access the claims
            var claims = jsonWebToken.Claims;
            var aud = claims.First(c => c.Type == "aud").Value;
            var iss = claims.First(c => c.Type == "iss").Value;
            _logger.LogInformation($"Retrvied token for {aud} from {iss}.");
            return token.Token;
        }
    }
}