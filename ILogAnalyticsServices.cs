namespace SureStacks.O365Logs2LA
{
    public interface ILogAnalyticsService
    {
        public Task SendLogs(List<dynamic> logs, string contentType);
        
    }
}