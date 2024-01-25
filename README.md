# O365Logs2LA

Send Office 365 unified logs to log analytics with .Net and Managed Identities and optimize resources and security.

There are already available samples and connectors to send Office 365 Unifed Logs to Log Analytics/Sentinel:

 * [Sentinel DataConnector for 0365](https://github.com/Azure/Azure-Sentinel/blob/master/DataConnectors/O365%20Data/readme.md)

 * [Sample AIP Audit Export](https://github.com/Azure-Samples/Azure-Information-Protection-Samples/blob/master/AIP-Audit-Export/Export-AIPAuditLogOperations.ps1) 
 
Both are based on PowerShell and will use Shared Key. 

This project has two objectives:

* Optimise resource usage by using a compiled language (.net)
* Leverage modern authentication with Managed Identities and avoid Shared Key

## High-level Overview

The function app will register a subscription to Office 365 Management API and be notified of log presence via a webhook.


```ascii  


-----+                 +----------------------+                +-----
     | <-------------- |                      |                |
O365 |  1. subscribe   |     Function App     |                | Log
Log  | --------------> |                      | =============> | Analytics
API  |   2. webhook    |      O365Log2LA      |  4. send logs  | Workspace
     | <============== |                      |                |
-----+ 3. get content  +----------------------+                +-----
                         🔑 Managed Identity
```

The permissions needed for the managed identity are:

 * Office 365 Management APIs
   * ActivityFeed.Read
   * ActivityFeed.ReadDlp
 * Log Analytics Workspace
   * Log Analytics Contributor
