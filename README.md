# O365Logs2LA

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FSureStacks2FO365Logs2LA%2Fmaster%2Fazuredeploy.json)



Send Office 365 unified logs to log analytics with .Net and Managed Identities and optimize resources and security.

There are already available samples and connectors to send Office 365 Unifed Logs to Log Analytics/Sentinel:

 * [Sentinel DataConnector for 0365](https://github.com/Azure/Azure-Sentinel/blob/master/DataConnectors/O365%20Data/readme.md)

 * [Sample AIP Audit Export](https://github.com/Azure-Samples/Azure-Information-Protection-Samples/blob/master/AIP-Audit-Export/Export-AIPAuditLogOperations.ps1) 
 
Both are based on PowerShell and will use Shared Key. 

This project has two objectives:

* Optimise resource usage by using a compiled language (.net)
* Leverage modern authentication with Managed Identities and avoid Shared Key

> ⚠️ Remember that you'll unified audit logs enabled on your tenant to retrieve those.

## High-level Overview

The function app will register a subscription to Office 365 Management API and be notified of log presence via a webhook.


```ascii  
                      | Key Vault (Secret)     |           
                      | - Log Analytics        |<- - - - - - - - +
                      |   Workspace Shared Key |                 
                      +------------------------+                 |
                                  ^
                                  | App Setting                  | 
                                  |
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
 * Log Analytics Workspace
   * Access the secret in the "Key Vault" holding the shared key

## Granting Permissions to Office 365 Management APIs via PowerShell

```PowerShell
# Install the required module
Install-Module -Name PowerShellGet -Force -AllowClobber
Install-Module -Name MSAL.PS, Microsoft.Graph.Authentication -Force

# Connect to Azure AD
$TenantID = "<Your Tenant ID>"
Connect-MgGraph -TenantId $TenantID -Scopes "Application.ReadWrite.All", "DelegatedPermissionGrant.ReadWrite.All"

# Get the service principal for the managed identity
$ManagedIdentityId = "<Your Managed Identity ID>"
$ServicePrincipal = Get-MgServicePrincipal -Filter "id eq '$ManagedIdentityId'"

# Get the service principal for the Office 365 Management APIs
$Office365APIId = "c5393580-f805-4401-95e8-94b7a6ef2fc2" # This is the standard Application ID for the Office 365 Management APIs
$Office365API = Get-MgServicePrincipal -Filter "appId eq '$Office365APIId'"

# Get the specific permissions from the Office 365 Management APIs service principal
$Permissions = $Office365API.AppRoles | Where-Object { $_.Value -in ('ActivityFeed.Read','ActivityFeed.ReadDlp') }

# Assign the permissions to the managed identity
$Permissions | ForEach-Object {
     New-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $ServicePrincipal.Id -AppRoleId $_.Id -PrincipalId $ServicePrincipal.Id -ResourceId $Office365API.Id
}
```

## Sample query to list Sensitivity Label downgrades

This query will require to receive the following content types:
* Audit.General
* Audit.SharePoint

They should cover modifications made in Office desktop, SharePoint and OneDrive (Office on the web).

> ℹ️ This has currenty been tested with OneDrive and Word desktop.

```KQL
Audit_SharePoint_CL 
| where SensitivityLabelEventData_LabelEventType_d == 2 
| project TimeGenerated, Workload=Workload_s, Application=AppAccessContext_ClientAppName_s, ClientIP=ClientIP_s, FileID=ObjectId_s, OldLabel=SensitivityLabelEventData_OldSensitivityLabelId_g, NewLabel=SensitivityLabelEventData_SensitivityLabelId_g, Justification=SensitivityLabelJustificationText_s
| union (
    Audit_General_CL
    | where SensitivityLabelEventData_LabelEventType_d == 2
    | project TimeGenerated, Workload=Workload_s, Application=Application_s, ClientIP=ClientIP_s, FileID=ObjectId_s, OldLabel = SensitivityLabelEventData_OldSensitivityLabelId_g, NewLabel = SensitivityLabelEventData_SensitivityLabelId_g, Justification=SensitivityLabelEventData_JustificationText_s
)
```