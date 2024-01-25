using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SureStacks.O365Logs2LA {
    public class Office365ManagementApiService : IOffice365ManagementApiService {
        private const string ProviderUUID = "62dcdf32-a5d8-4797-8025-182c4bc08771";
        private readonly IManagedIdentityTokenService _managedIdentityTokenService;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly string _hostname;
        private string? _tenantId;
        private readonly bool _debug;


        public Office365ManagementApiService(IManagedIdentityTokenService managedIdentityTokenService, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory) {
            var debug = Environment.GetEnvironmentVariable("Debug");
            if (debug is not null) {
                _debug = true;
            }
            // get logger
            _logger = loggerFactory.CreateLogger<Office365ManagementApiService>();
            // get website hostname from environment variable
            var hostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
            if (string.IsNullOrEmpty(hostname)) {
                _logger.LogInformation("/!\\ WEBSITE_HOSTNAME environment variable not found.");
                throw new Exception("WEBSITE_HOSTNAME environment variable not found.");
            }
            _hostname = hostname;
            // log hostname
            _logger.LogInformation($"Hostname: {_hostname}");
            // get token service
            _managedIdentityTokenService = managedIdentityTokenService;
            // get http client
            _httpClient = httpClientFactory.CreateClient();
        }

        private async Task CheckAuth() {
            // get token from managed identity
            var token = await _managedIdentityTokenService.GetToken();
            if (_debug) _logger.LogInformation($"Token: {token}");
            // list current subscriptions from Office 365 Management API and this provider
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            // if tenant if is empty fill it from the token (jwt)
            if (string.IsNullOrEmpty(_tenantId)) {
                // Decode the JWT
                var handler = new JsonWebTokenHandler();
                var jsonWebToken = handler.ReadJsonWebToken(token);
                // Access the claims
                _tenantId = jsonWebToken.Claims.First(c => c.Type == "tid").Value;
                // check that tenant id is not empty
                if (string.IsNullOrEmpty(_tenantId)) {
                    _logger.LogInformation("/!\\ Tenant ID not found in token.");
                    throw new Exception("Tenant ID not found in token.");
                }
                // log tenant id
                _logger.LogInformation($"Tenant ID: {_tenantId}");
            }
        }

        public async Task<List<Subscription>> GetSubscriptions()
        {
            // check auth
            await CheckAuth();
            // get subscriptions for tenant and provider using Office 365 Management API
            var response = await _httpClient.GetAsync($"https://manage.office.com/api/v1.0/{_tenantId}/activity/feed/subscriptions/list?PublisherIdentifier={ProviderUUID}");
            // check response
            if (!response.IsSuccessStatusCode) {
                // check if content and get error from json ErrorResult object
                if (response.Content is not null) {
                    var content = await response.Content.ReadAsStringAsync();
                    var error = JsonSerializer.Deserialize<ErrorResponse>(content);
                    if (error is not null) {
                        _logger.LogInformation($"/!\\ Error getting subscriptions: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                        throw new Exception($"Error getting subscriptions: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                    }
                } 
                _logger.LogInformation($"/!\\ Error getting subscriptions: {response.StatusCode}");
                throw new Exception($"Error getting subscriptions: {response.StatusCode}");
            }
            // return subscriptions from json
            var subscriptions = JsonSerializer.Deserialize<List<Subscription>>(await response.Content.ReadAsStringAsync());
            if (subscriptions is null) {
                _logger.LogInformation("/!\\ Error deserializing subscriptions.");
                throw new Exception("Error deserializing subscriptions.");
            }
            _logger.LogInformation($"Retrieved {subscriptions.Count} subscriptions.");
            return subscriptions;
        }

        public async Task<List<dynamic>> RetrieveLogs(string contentId)
        {
            // check auth
            await CheckAuth();
            // get logs for tenant and content id using Office 365 Management API
            var response = await _httpClient.GetAsync($"https://manage.office.com/api/v1.0/{_tenantId}/activity/feed/audit/{contentId}");
            // check response
            if (!response.IsSuccessStatusCode) {
                // check if content and get error from json ErrorResult object
                if (response.Content is not null) {
                    var content = await response.Content.ReadAsStringAsync();
                    var error = JsonSerializer.Deserialize<ErrorResponse>(content);
                    if (error is not null) {
                        _logger.LogInformation($"/!\\ Error getting logs: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                        throw new Exception($"Error getting logs: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                    }
                } 
                _logger.LogInformation($"/!\\ Error getting logs: {response.StatusCode}");
                throw new Exception($"Error getting logs: {response.StatusCode}");
            }
            // return logs from json
            var logs = JsonSerializer.Deserialize<List<dynamic>>(await response.Content.ReadAsStringAsync());
            if (logs is null) {
                _logger.LogInformation("/!\\ Error deserializing logs.");
                throw new Exception("Error deserializing logs.");
            }
            return logs;
        }

        public async Task<Subscription> StartSubscription(ContentTypes.ContentType contentType)
        {
            // check auth
            await CheckAuth();
            // create a new webhook for the subscription
            var createSub = new Subscription {
                Webhook = new Webhook {
                    Address = $"https://{_hostname}/api/content"
                }
            };
            // serialize webhook to json
            var webhookJson = JsonSerializer.Serialize(createSub);
            // start a subscription for tenant, provider and content type using Office 365 Management API and webhook as body
            var response = await _httpClient.PostAsync($"https://manage.office.com/api/v1.0/{_tenantId}/activity/feed/subscriptions/start?contentType={ContentTypes.GetContentTypeString(contentType)}&PublisherIdentifier={ProviderUUID}", new StringContent(webhookJson, Encoding.UTF8, "application/json"));
            if (_debug) _logger.LogInformation($"Request payload: {webhookJson}");
            // check response
            if (!response.IsSuccessStatusCode) {
                // check if content and get error from json ErrorResult object
                if (response.Content is not null) {
                    var content = await response.Content.ReadAsStringAsync();
                    if (_debug) _logger.LogInformation($"Response payload: {content}");
                    var error = JsonSerializer.Deserialize<ErrorResponse>(content);
                    if (error is not null) {
                        _logger.LogInformation($"/!\\ Error starting subscription: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                        throw new Exception($"Error starting subscription: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                    }
                } 
                _logger.LogInformation($"/!\\ Error starting subscription: {response.StatusCode}");
                throw new Exception($"Error starting subscription: {response.StatusCode}");
            }
            // get subscription from json
            var subscription = JsonSerializer.Deserialize<Subscription>(await response.Content.ReadAsStringAsync());
            if (subscription is null) {
                _logger.LogInformation("/!\\ Error deserializing subscription.");
                throw new Exception("Error deserializing subscription.");
            }
            if (subscription.Status != "enabled") {
                _logger.LogInformation($"/!\\ Error new subscription not enabled: {subscription.Status}");
                throw new Exception($"Error new subscription not enabled: {subscription.Status}");
            }
            // log subscription started with current status, content type, webhook and webhook status
            _logger.LogInformation($"Subscription started: {subscription.Status} - {subscription.ContentType} - {subscription.Webhook.Address} - {subscription.Webhook.Status}");
            return subscription;
        }

        public async Task StopSubscription(ContentTypes.ContentType contentType)
        {
            // check auth
            await CheckAuth();
            // stop a subscription for tenant, provider and content type using Office 365 Management API
            var response = await _httpClient.PostAsync($"https://manage.office.com/api/v1.0/{_tenantId}/activity/feed/subscriptions/stop?contentType={ContentTypes.GetContentTypeString(contentType)}&PublisherIdentifier={ProviderUUID}", null);
            // check response
            if (!response.IsSuccessStatusCode) {
                // check if content and get error from json ErrorResult object
                if (response.Content is not null) {
                    var content = await response.Content.ReadAsStringAsync();
                    var error = JsonSerializer.Deserialize<ErrorResponse>(content);
                    if (error is not null) {
                        _logger.LogInformation($"/!\\ Error stopping subscription: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                        throw new Exception($"Error stopping subscription: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                    }
                } 
                _logger.LogInformation($"/!\\ Error stopping subscription: {response.StatusCode}");
                throw new Exception($"Error stopping subscription: {response.StatusCode}");
            }
        }
    }
}