namespace SureStacks.O365Logs2LA {
    public static class ContentTypes {
                
        public enum ContentType
        {
            Audit_AzureActiveDirectory,
            Audit_Exchange,
            Audit_SharePoint,
            Audit_General,
            DLP_All
        }

        public static string GetContentTypeString(ContentType contentType)
        {
            switch (contentType)
            {
                case ContentType.Audit_AzureActiveDirectory:
                    return "Audit.AzureActiveDirectory";
                case ContentType.Audit_Exchange:
                    return "Audit.Exchange";
                case ContentType.Audit_SharePoint:
                    return "Audit.SharePoint";
                case ContentType.Audit_General:
                    return "Audit.General";
                case ContentType.DLP_All:
                    return "DLP.All";
                default:
                    throw new ArgumentException($"Invalid content type: {contentType}");
            }
        }

        public static ContentType GetContentTypeEnum(string contentType)
        {
            switch (contentType)
            {
                case "Audit.AzureActiveDirectory":
                    return ContentType.Audit_AzureActiveDirectory;
                case "Audit.Exchange":
                    return ContentType.Audit_Exchange;
                case "Audit.SharePoint":
                    return ContentType.Audit_SharePoint;
                case "Audit.General":
                    return ContentType.Audit_General;
                case "DLP.All":
                    return ContentType.DLP_All;
                default:
                    throw new ArgumentException($"Invalid content type: {contentType}");
            }
        }
    }
}