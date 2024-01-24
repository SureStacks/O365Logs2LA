using Azure.Core;
using Azure.Identity;
using System.Net;
using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;

namespace SureStacks.O365Logs2LA
{
    public class Subscribe
    {
        private readonly ILogger _logger;
        private const string ProviderUUID = "62dcdf32-a5d8-4797-8025-182c4bc08771";

        public Subscribe(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Subscribe>();
        }

        // get token from managed identity
        public static async Task<string> GetToken() {
            var defaultAzureCredential = new DefaultAzureCredential();
            var Token = await defaultAzureCredential.GetTokenAsync(new TokenRequestContext(new[] { "https://manage.office.com/.default" }));
            return Token.Token;
        }

        public static async Task<bool> IsSubscribed()
        {
            // get website hostname from environment variable
            var hostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
            // get token from managed identity
            var token = await GetToken();
            // list current subscriptions from Office 365 Management API and this provider
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync($"https://manage.office.com/api/v1.0/{hostname}/activity/feed/subscriptions/list?PublisherIdentifier={ProviderUUID}");
            return true;
        }
 
        [Function("Subscribe")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Subscribe Request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Subscribed to O365 Webhook");

            return response;
        }
    }
}
