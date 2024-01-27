using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SureStacks.O365Logs2LA {
    public class LogAnalyticsService : ILogAnalyticsService
    {
        private readonly IManagedIdentityTokenService _tokenService;
        private readonly ILogger<LogAnalyticsService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _logAnalyticsUrl;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web){
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public LogAnalyticsService(IManagedIdentityTokenService tokenService, ILogger<LogAnalyticsService> logger, IHttpClientFactory httpClientFactory)
        {
            _tokenService = tokenService;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.DefaultRequestHeaders.Add("time-generated-field", "CreationTime");
            // get log analytics workspace id from environment variable
            var logAnalyticsWorkspace = Environment.GetEnvironmentVariable("LogAnalyticsWorkspace");
            // check that log analytics workspace id is not empty
            if (string.IsNullOrEmpty(logAnalyticsWorkspace))
            {
                _logger.LogInformation("LA: /!\\ LogAnalyticsWorkspace environment variable is empty.");
                throw new Exception("LogAnalyticsWorkspace environment variable is empty.");
            }
            _logAnalyticsUrl = $"https://{logAnalyticsWorkspace}.ods.opinsights.azure.com/api/logs?api-version=2016-04-01";
        }

        public async Task SendLogs(List<dynamic> logs, string contentType)
        {
            _logger.LogInformation($"LA: Sending {logs.Count} logs to Log Analytics.");

            // get token
            string token = await _tokenService.GetToken("monitor.azure.com");
            // set authorization header
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // set x-ms-date header
            _httpClient.DefaultRequestHeaders.Add("x-ms-date", DateTime.UtcNow.ToString("r"));
            // set log-type header from contentType, replace "." with "_"
            _httpClient.DefaultRequestHeaders.Add("Log-Type", contentType.Replace(".", "_") + "_CL");

            // create string content from logs
            var content = new StringContent(JsonSerializer.Serialize(logs, _jsonOptions));

            HttpResponseMessage response = await _httpClient.PostAsync(_logAnalyticsUrl, content);

            if (!response.IsSuccessStatusCode) {
                // if unauthorized invalidate the token
                if (response.StatusCode == HttpStatusCode.Unauthorized) {
                    await _tokenService.InvalidateToken("monitor.azure.com").ConfigureAwait(false);
                }
                _logger.LogInformation($"LA: /!\\ Failed to send log. Status code: {response.StatusCode}");
                throw new Exception($"Failed to send log. Status code: {response.StatusCode}");
            }
        }
    }
}