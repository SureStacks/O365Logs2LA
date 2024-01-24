using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace SureStacks.O365Logs2LA
{
    public class CheckSubscription
    {
        private readonly ILogger _logger;

        public CheckSubscription(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CheckSubscription>();
        }

        [Function("CheckSubscription")]
        public void Run([TimerTrigger("0 0 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"Checking Subscriptions: {DateTime.Now}");
            
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next SubscriptionCheck at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
