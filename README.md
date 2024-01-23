# O365Logs2LA

Send Office 365 unified logs to log analytics with .Net and Managed Identities and optimise resources and security.

There are already available samples and connectors to send Office 365 Unifed Logs to Log Analytics/Sentinel:

 * [Sentinel DataConnector for 0365](https://github.com/Azure/Azure-Sentinel/blob/master/DataConnectors/O365%20Data/readme.md)

 * [Sample AIP Audit Export](https://github.com/Azure-Samples/Azure-Information-Protection-Samples/blob/master/AIP-Audit-Export/Export-AIPAuditLogOperations.ps1) 
 
Both are based on PowerShell and will use Shared Key. 

This project has two objectives:

* Optimise resource usage by using a compiled language (.net)
* Leverage modern authentication with Managed Identities and avoid Shared Key

## High level Overview

The function app will collect logs every 5 minutes and send the logs to log Analytics.

The DataConnector does use a storage account to store the last time logs where collected to handle cases where the function may have failed.

In the present implementation we might rely on requesting the last timestamp of logs to determine an equivalent invormation.

```ascii


-----+        +------------+        +-----
     |        |            |        |
O365 |        |  Function  |        | Log
Log  | <----> |    App     | <----> | Analytics
API  |        | O365Log2LA |        | Workspace
     |        |            |        |
-----+        +------------+        +-----
             Managed Identity
```

The permissions needed for the managed identity are:

 * Office 365 Management APIs
   * ActivityFeed.Read
   * ActivityFeed.ReadDlp
 * Log Analytics Workspace
   * [TO CHECK]
