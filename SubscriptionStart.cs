using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace SureStacks.O365Logs2LA
{
    public class SubscriptionValidation
    {
        public required string ValidationCode { get; set; }
    }

    public class SubscriptionStart
    {
        private readonly ILogger _logger;

        public SubscriptionStart(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SubscriptionStart>();
        }

        [Function("SubscriptionStart")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "/start")] HttpRequestData req)
        {
            _logger.LogInformation("Subscription Start Request.");

            // get webhook validation code from header
            if (!req.Headers.TryGetValues("Webhook-ValidationCode", out var validationCodes))
            {
                _logger.LogError("Webhook-ValidationCode header not found on request.");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }
            var validationCode = validationCodes.First();
            
            // check that validation code is not empty
            if (string.IsNullOrEmpty(validationCode))
            {
                _logger.LogError("Webhook-ValidationCode header is empty.");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            // get validation code from post body json
            var subscriptionValidation = await req.ReadFromJsonAsync<SubscriptionValidation>();

            // check that validation codes match
            if (validationCode != subscriptionValidation?.ValidationCode)
            {
                _logger.LogError("Validation codes do not match.");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            // log webhook validation passed
            _logger.LogInformation("Webhook validation passed.");
            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
