using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace SureStacks.O365Logs2LA
{
    public class GetSubscriptions
    {
        private readonly ILogger _logger;
        private readonly IOffice365ManagementApiService _office365ManagementApiService;

        public GetSubscriptions(ILoggerFactory loggerFactory, IOffice365ManagementApiService office365ManagementApiService)
        {
            _logger = loggerFactory.CreateLogger<GetSubscriptions>();
            _office365ManagementApiService = office365ManagementApiService;
        }

        [Function("GetSubscriptions")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting Subscriptions.");

            var subscriptions = await _office365ManagementApiService.GetSubscriptions();

            // check that subscriptions is not empty
            if (subscriptions == null)
            {
                _logger.LogError("Subscriptions is empty.");
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            // put subscriptionsSimple into a json string
            var subscriptionsJson = JsonSerializer.Serialize(subscriptions);
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(subscriptionsJson);
            return response;
        }
    }
}
