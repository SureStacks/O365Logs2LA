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

    public class Content {
        public required string TenantId { get; set; }
        public required string ClientId { get; set; }
        public required string ContentType { get; set; }
        public required string ContentId { get; set; }
        public required string ContentUri { get; set; }
        public required DateTime ContentCreated { get; set; }
        public required DateTime ContentExpiration { get; set; }
    }

    public class ContentWebhook
    {
        private readonly ILogger _logger;
        private readonly IOffice365ManagementApiService _office365ManagementApiService;
        private readonly ILogAnalyticsService _logAnalyticsService;

        public ContentWebhook(ILoggerFactory loggerFactory, IOffice365ManagementApiService office365ManagementApiService, ILogAnalyticsService logAnalyticsService)
        {
            _logger = loggerFactory.CreateLogger<ContentWebhook>();
            _office365ManagementApiService = office365ManagementApiService;
            _logAnalyticsService = logAnalyticsService;
        }

        [Function("Content")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "content")] HttpRequestData req)
        {
            _logger.LogInformation("Subscription Content Webhook.");

            // if there is a validation code this is a webhook validation
            if (req.Headers.TryGetValues("Webhook-ValidationCode", out var validationCodes))
            {
                _logger.LogInformation("Webhook validation request.");
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

            // if there is no validation code this is a content notification
            _logger.LogInformation("Content notification request.");
            var content = await req.ReadFromJsonAsync<List<Content>>();

            // check that content is not empty
            if (content is null)
            {
                _logger.LogError("Content is empty.");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            // log content notification with count of content and their content types
            _logger.LogInformation($"Content notification with {content.Count} content(s) received.");

            // use office management api service to retrieve and send logs to log analytics for each content in parallel
            try {
                await Task.WhenAll(content.Select(async c =>
                {
                    _logger.LogInformation($"Retrieving logs for content {c.ContentId}.");
                    var logs = await _office365ManagementApiService.RetrieveLogs(c.ContentId);
                    _logger.LogInformation($"Sending logs for content {c.ContentId} to log analytics.");
                    await _logAnalyticsService.SendLogs(logs, c.ContentType);
                    _logger.LogInformation($"Logs for content {c.ContentId} sent to log analytics.");
                }));
            } catch (Exception e) {
                _logger.LogError(e, "Error sending logs to log analytics.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            // log content notification processed
            _logger.LogInformation("Content notification processed.");
            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
