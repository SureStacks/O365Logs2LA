using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace SureStacks.O365Logs2LA
{
    public class CheckSubscription
    {
        private readonly ILogger _logger;
        private readonly IOffice365ManagementApiService _office365ManagementApiService;
        private readonly List<ContentTypes.ContentType> _logTypes;

        public CheckSubscription(ILoggerFactory loggerFactory, IOffice365ManagementApiService office365ManagementApiService)
        {
            _logger = loggerFactory.CreateLogger<CheckSubscription>();
            _office365ManagementApiService = office365ManagementApiService;
            // check the environeent variable LogTypes (csv to list the content types to subscribe to
            var logTypes = Environment.GetEnvironmentVariable("LogTypes");
            // check that logTypes is not empty
            if (string.IsNullOrEmpty(logTypes))
            {
                _logger.LogError("LogTypes environment variable is empty.");
                throw new Exception("LogTypes environment variable is empty.");
            }
            // split logTypes into a list
            var logTypesList = logTypes.Split(",").ToList();
            // check that logTypes are in ContentTypes.ContentType
            _logTypes = new List<ContentTypes.ContentType>();
            foreach (var logType in logTypesList)
            {  
                switch (logType)
                {
                    case "Audit.AzureActiveDirectory":
                        _logTypes.Add(ContentTypes.ContentType.Audit_AzureActiveDirectory);
                        break;
                    case "Audit.Exchange":
                        _logTypes.Add(ContentTypes.ContentType.Audit_Exchange);
                        break;
                    case "Audit.SharePoint":
                        _logTypes.Add(ContentTypes.ContentType.Audit_SharePoint);
                        break;
                    case "Audit.General":
                        _logTypes.Add(ContentTypes.ContentType.Audit_General);
                        break;
                    case "DLP.All":
                        _logTypes.Add(ContentTypes.ContentType.DLP_All);
                        break;
                    default:
                        _logger.LogError($"Invalid log type: {logType}");
                        break;
                }
            }
        }

        public async Task<List<Subscription>> CheckSubscriptionFunc()
        {
            _logger.LogInformation($"Checking Subscriptions.");

            // initialize subscriptions
            var newSubscriptions = new List<Subscription>();

            // get subscriptions
            var subscriptions = await _office365ManagementApiService.GetSubscriptions();

            // for each log types verify if there is an enabled subscription
            foreach (var logType in _logTypes)
            {
                // check if there is a subscription for the log type
                var subscription = subscriptions.FirstOrDefault(s => s.ContentType == ContentTypes.GetContentTypeString(logType));
                // if there is no subscription for the log type create one
                if ((subscription is null) || (string.Compare(subscription.Status,"enabled",true) != 0))
                {
                    _logger.LogInformation($"Subscribing to {logType}");
                    newSubscriptions.Add(await _office365ManagementApiService.StartSubscription(logType));
                }
            }

            // return new subscriptions
            return newSubscriptions;
        }

        [Function("CheckSubscription")]
        public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo myTimer)
        {
            await CheckSubscriptionFunc();

            
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next SubscriptionCheck at: {myTimer.ScheduleStatus.Next}");
            }
        }

        [Function("Subscribe")]
        public async Task<HttpResponseData> Subscribe([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            // get new subscriptions
            var newSubscriptions = await CheckSubscriptionFunc();

            // put subscriptionsSimple into a json string
            var subscriptionsJson = JsonSerializer.Serialize(newSubscriptions);
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(subscriptionsJson);
            return response;
        }
    }
}
