using System.Net;
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
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web){
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };


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
                _logger.LogInformation("OMAPI: /!\\ WEBSITE_HOSTNAME environment variable not found.");
                throw new Exception("OMAPI: WEBSITE_HOSTNAME environment variable not found.");
            }
            _hostname = hostname;
            // log hostname
            _logger.LogInformation($"OMAPI: Hostname: {_hostname}");
            // get token service
            _managedIdentityTokenService = managedIdentityTokenService;
            // get http client
            _httpClient = httpClientFactory.CreateClient();
        }

        private async Task CheckAuth() {
            // get token from managed identity
            var token = await _managedIdentityTokenService.GetToken();
            if (_debug) _logger.LogInformation($"OMAPI: Token: {token}");
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
                    _logger.LogInformation("OMAPI: /!\\ Tenant ID not found in token.");
                    throw new Exception("Tenant ID not found in token.");
                }
                // log tenant id
                _logger.LogInformation($"OMAPI: Tenant ID: {_tenantId}");
            }
        }

        public async Task<List<Subscription>> GetSubscriptions()
        {
            // check auth
            await CheckAuth();
            // get subscriptions for tenant and provider using Office 365 Management API
            var URI = $"https://manage.office.com/api/v1.0/{_tenantId}/activity/feed/subscriptions/list?PublisherIdentifier={ProviderUUID}";
            if (_debug) _logger.LogInformation($"OMAPI Get: Get URI: {URI}");
            var response = await _httpClient.GetAsync(URI);
            // check response
            if (!response.IsSuccessStatusCode) {
                // if unauthorized invalidate the token
                if (response.StatusCode == HttpStatusCode.Unauthorized) {
                    await _managedIdentityTokenService.InvalidateToken().ConfigureAwait(false);
                }
                // check if content and get error from json ErrorResult object
                if (response.Content is not null) {
                    var content = await response.Content.ReadAsStringAsync();
                    if (_debug) _logger.LogInformation($"OMAPI Get: Response payload: {content}");
                    try {
                        var error = JsonSerializer.Deserialize<ErrorResponse>(content);
                        if (error is not null) {
                            _logger.LogInformation($"OMAPI Get: /!\\ Error getting subscriptions: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                            throw new Exception($"Error getting subscriptions: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                        }
                    } catch {
                        _logger.LogInformation($"OMAPI Get: /!\\ Error getting subscriptions: {response.StatusCode}");
                        throw new Exception($"Error getting subscriptions: {response.StatusCode}");
                    }
                } 
                _logger.LogInformation($"OMAPI Get: /!\\ Error getting subscriptions: {response.StatusCode}");
                throw new Exception($"Error getting subscriptions: {response.StatusCode}");
            }
            // return subscriptions from json
            List<Subscription>? subscriptions = null;
            try {
                var content = await response.Content.ReadAsStringAsync();
                if (_debug) _logger.LogInformation($"OMAPI Get: Response payload: {content}");
                subscriptions = JsonSerializer.Deserialize<List<Subscription>>(content, _jsonOptions);
            } catch (Exception e){
                _logger.LogInformation($"OMAPI Get: /!\\ Error deserializing subscriptions: {e.Message}");
                throw new Exception("Error deserializing subscriptions: {e.Message}");
            }
            if (subscriptions is null) {
                _logger.LogInformation("OMAPI Get: /!\\ Error deserializing subscriptions.");
                throw new Exception("Error deserializing subscriptions.");
            }
            // log subscriptions
            if (_debug) foreach (var subscription in subscriptions)
            {
                _logger.LogInformation($"OMAPI Get: Subscription: {subscription}");
            }
            _logger.LogInformation($"OMAPI Get: Retrieved {subscriptions.Count} subscription(s).");
            return subscriptions;
        }

        public async Task<List<dynamic>> RetrieveLogs(string contentId)
        {
            // check auth
            await CheckAuth();
            // get logs for tenant and content id using Office 365 Management API
            var URI = $"https://manage.office.com/api/v1.0/{_tenantId}/activity/feed/audit/{contentId}";
            if (_debug) _logger.LogInformation($"OMAPI Content: Get URI: {URI}");
            var response = await _httpClient.GetAsync(URI);
            // check response
            if (!response.IsSuccessStatusCode) {
                // if unauthorized invalidate the token
                if (response.StatusCode == HttpStatusCode.Unauthorized) {
                    await _managedIdentityTokenService.InvalidateToken().ConfigureAwait(false);
                }
                // check if content and get error from json ErrorResult object
                if (response.Content is not null) {
                    var content = await response.Content.ReadAsStringAsync();
                    try {
                        var error = JsonSerializer.Deserialize<ErrorResponse>(content);
                        if (error is not null) {
                            _logger.LogInformation($"OMAPI Content: /!\\ Error getting logs: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                            throw new Exception($"Error getting logs: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                        }
                    } catch {
                        _logger.LogInformation($"OMAPI Content: /!\\ Error getting logs: {response.StatusCode}");
                        throw new Exception($"Error getting logs: {response.StatusCode}");
                    }
                } 
                _logger.LogInformation($"OMAPI Content: /!\\ Error getting logs: {response.StatusCode}");
                throw new Exception($"Error getting logs: {response.StatusCode}");
            }
            // return logs from json
            var logs = JsonSerializer.Deserialize<List<dynamic>>(await response.Content.ReadAsStringAsync());
            if (logs is null) {
                _logger.LogInformation("OMAPI Content: /!\\ Error deserializing logs.");
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
                ContentType = ContentTypes.GetContentTypeString(contentType),
                Webhook = new Webhook {
                    Address = $"https://{_hostname}/api/content"
                }
            };
            // serialize webhook to json
            var newSubscriptionJson = JsonSerializer.Serialize(createSub, _jsonOptions);
            // start a subscription for tenant, provider and content type using Office 365 Management API and webhook as body
            var URI = $"https://manage.office.com/api/v1.0/{_tenantId}/activity/feed/subscriptions/start?contentType={ContentTypes.GetContentTypeString(contentType)}&PublisherIdentifier={ProviderUUID}";
            if (_debug) _logger.LogInformation($"OMAPI Start: Post URI: {URI}");
            var response = await _httpClient.PostAsync(URI, new StringContent(newSubscriptionJson, Encoding.UTF8, "application/json"));
            if (_debug) _logger.LogInformation($"OMAPI Start: Request payload: {newSubscriptionJson}");
            // check response
            if (!response.IsSuccessStatusCode) {
                // if unauthorized invalidate the token
                if (response.StatusCode == HttpStatusCode.Unauthorized) {
                    await _managedIdentityTokenService.InvalidateToken().ConfigureAwait(false);
                }
                // check if content and get error from json ErrorResult object
                if (response.Content is not null) {
                    var content = await response.Content.ReadAsStringAsync();
                    if (_debug) _logger.LogInformation($"OMAPI Start: Response payload: {content}");
                    var error = JsonSerializer.Deserialize<ErrorResponse>(content);
                    if (error is not null) {
                        _logger.LogInformation($"OMAPI Start: /!\\ Error starting subscription: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                        throw new Exception($"Error starting subscription: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                    }
                } 
                _logger.LogInformation($"OMAPI Start: /!\\ Error starting subscription: {response.StatusCode}");
                throw new Exception($"Error starting subscription: {response.StatusCode}");
            }
            // get subscription from json
            var subcontent = await response.Content.ReadAsStringAsync();
            if (_debug) _logger.LogInformation($"OMAPI Start: Response payload: {subcontent}");
            var subscription = JsonSerializer.Deserialize<Subscription>(subcontent);
            if (subscription is null) {
                _logger.LogInformation("OMAPI Start: /!\\ Error deserializing subscription.");
                throw new Exception("Error deserializing subscription.");
            }
            if (subscription.Status != "enabled") {
                _logger.LogInformation($"OMAPI Start: /!\\ Warning new subscription not enabled: '{subscription.Status}'");
            }
            // log subscription started with current status, content type, webhook and webhook status
            _logger.LogInformation($"OMAPI Start: Subscription started: {subscription}");
            return subscription;
        }

        public async Task StopSubscription(ContentTypes.ContentType contentType)
        {
            // check auth
            await CheckAuth();
            // stop a subscription for tenant, provider and content type using Office 365 Management API
            var URI = $"https://manage.office.com/api/v1.0/{_tenantId}/activity/feed/subscriptions/stop?contentType={ContentTypes.GetContentTypeString(contentType)}&PublisherIdentifier={ProviderUUID}";
            if (_debug) _logger.LogInformation($"OMAPI Stop: Post URI: {URI}");
            var response = await _httpClient.PostAsync(URI, null);
            // check response
            if (!response.IsSuccessStatusCode) {
                // if unauthorized invalidate the token
                if (response.StatusCode == HttpStatusCode.Unauthorized) {
                    await _managedIdentityTokenService.InvalidateToken().ConfigureAwait(false);
                }
                // check if content and get error from json ErrorResult object
                if (response.Content is not null) {
                    var content = await response.Content.ReadAsStringAsync();
                    try {
                        var error = JsonSerializer.Deserialize<ErrorResponse>(content);
                        if (error is not null) {
                            _logger.LogInformation($"OMAPI Stop: /!\\ Error stopping subscription: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                            throw new Exception($"Error stopping subscription: {error.Error.Message} - {error.Error.Code} - {response.StatusCode}");
                        }
                    } catch {
                        _logger.LogInformation($"OMAPI Stop: /!\\ Error stopping subscription: {response.StatusCode}");
                        throw new Exception($"Error stopping subscription: {response.StatusCode}");
                    
                    }
                } 
                _logger.LogInformation($"OMAPI Stop: /!\\ Error stopping subscription: {response.StatusCode}");
                throw new Exception($"Error stopping subscription: {response.StatusCode}");
            }
            // log subscription stopped
            _logger.LogInformation($"OMAPI Stop: Subscription to {ContentTypes.GetContentTypeString(contentType)} stopped.");
        }
    }
}