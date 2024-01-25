using System.Net.Mime;

namespace SureStacks.O365Logs2LA {
    public class Webhook {
        public string? Status { get; set; }
        public required string Address { get; set; }
        public string? AuthId { get; set; }
        public DateTime? Expiration { get; set; }
    }

    public class Subscription {
        public string? ContentType { get; set; }
        public string? Status { get; set; }
        public required Webhook Webhook { get; set; }
    }

    public class Logs {
        public required string Id { get; set; }
        public required string RecordType { get; set; }
        public required DateTime CreationTime { get; set; }
        public required string ContentType { get; set; }
        public required string Operation { get; set; }
        public required string OrganizationId { get; set; }
        public required string ResultStatus { get; set; }
        public required string UserKey { get; set; }
        public required string Workload { get; set; }
    }

    public class Error {
        public required string Code { get; set; }
        public string? Message { get; set; }
    }

    public class ErrorResponse {
        public required Error Error { get; set; }
    }

    public interface IOffice365ManagementApiService {
        public Task<Subscription> StartSubscription(ContentTypes.ContentType contentType);
        public Task StopSubscription(ContentTypes.ContentType contentType);
        public Task<List<Subscription>> GetSubscriptions();
        public Task<List<dynamic>> RetrieveLogs(string contentId);
    }
}