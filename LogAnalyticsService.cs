using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SureStacks.O365Logs2LA {
    public class LAError {
        public string? Message { get; set; }
        public string? Error { get; set; }
    }

    public class LogAnalyticsService : ILogAnalyticsService
    {
        private readonly IManagedIdentityTokenService _tokenService;
        private readonly ILogger<LogAnalyticsService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _logAnalyticsUrl;
        private readonly string _logAnalyticsWorkspace;
        private readonly string _logAnalyticsWorkspaceKey;
        private readonly bool _debug;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web){
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public LogAnalyticsService(IManagedIdentityTokenService tokenService, ILogger<LogAnalyticsService> logger, IHttpClientFactory httpClientFactory)
        {
            // get debug from environment variabl
            var debug = Environment.GetEnvironmentVariable("Debug");
            if (debug is not null) {
                _debug = true;
            }
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
            _logAnalyticsWorkspace = logAnalyticsWorkspace;
            // get log analytics workspace key from environment variable
            var logAnalyticsWorkspaceKey = Environment.GetEnvironmentVariable("LogAnalyticsWorkspaceKey");
            // check that log analytics workspace key is not empty
            if (string.IsNullOrEmpty(logAnalyticsWorkspaceKey))
            {
                _logger.LogInformation("LA: /!\\ LogAnalyticsWorkspaceKey environment variable is empty.");
                throw new Exception("LogAnalyticsWorkspaceKey environment variable is empty.");
            }
            _logAnalyticsWorkspaceKey = logAnalyticsWorkspaceKey;
            // create log analytics url
            _logAnalyticsUrl = $"https://{logAnalyticsWorkspace}.ods.opinsights.azure.com/api/logs?api-version=2016-04-01";
        }

        // create log analytics signature
        private string CreateSignature(long length, string date)
        {
            // create signature string
            var signature = $"POST\n{length}\napplication/json\nx-ms-date:{date}\n/api/logs";
            // create key
            var key = Convert.FromBase64String(_logAnalyticsWorkspaceKey);
            // create hmac
            using var hmac = new System.Security.Cryptography.HMACSHA256(key);
            // compute hash
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(signature));
            // convert hash to base64
            return Convert.ToBase64String(hash);
        }

        public async Task SendLogs(List<dynamic> logs, string contentType)
        {
            _logger.LogInformation($"LA: Sending {logs.Count} logs to Log Analytics.");
            
            // create http request
            HttpRequestMessage request = new(HttpMethod.Post, _logAnalyticsUrl);

            // set x-ms-date header
            var xmsdate = DateTime.UtcNow.ToString("r");
            request.Headers.Add("x-ms-date", xmsdate);
            // set log-type header from contentType, replace "." with "_" and add "_CL"
            request.Headers.Add("Log-Type", contentType.Replace(".", "_") + "_CL");
            

            // create string content from logs
            var logsJson = JsonSerializer.Serialize(logs, _jsonOptions);
            // log content if debug
            if (_debug) _logger.LogInformation($"LA: Request payload: {logsJson}");
            // create content
            var content = new StringContent(logsJson);
            // set request content
            request.Content = content;
            // set content type header
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // create signature
            var length = content.Headers.ContentLength ?? 0;
            var signature = CreateSignature(length, xmsdate);
            // set authorization header
            request.Headers.Add("Authorization", $"SharedKey {_logAnalyticsWorkspace}:{signature}");

            HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode) {
                // if unauthorized invalidate the token
                if (response.StatusCode == HttpStatusCode.Unauthorized) {
                    await _tokenService.InvalidateToken("monitor.azure.com").ConfigureAwait(false);
                }
                // if _debug and response has content, log response content
                if (_debug && response.Content is not null) {
                    // get content as string
                    var responseContent = await response.Content.ReadAsStringAsync();
                    // parse content as json
                    try {
                        var responseJson = JsonSerializer.Deserialize<LAError>(responseContent);
                        if (string.IsNullOrEmpty(responseJson?.Message)) {
                            _logger.LogInformation($"LA: /!\\ Failed to send log: {responseJson?.Message} - {response.StatusCode}");
                            throw new Exception($"Failed to send log: {responseJson?.Message} - {response.StatusCode}");
                        } 
                    } catch {
                        // do nothing
                    }
                }
                _logger.LogInformation($"LA: /!\\ Failed to send log. Status code: {response.StatusCode}");
                throw new Exception($"Failed to send log. Status code: {response.StatusCode}");
            }
        }
    }
}