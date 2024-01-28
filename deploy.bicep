param UnifiedLogingName string
param location string = resourceGroup().location

var keyVaultName = '${UnifiedLogingName}-kv'
var logAnalyticsName = '${UnifiedLogingName}-la'
var functionAppName = '${UnifiedLogingName}-fn'
var storageAccountName = toLower('${UnifiedLogingName}${substring(uniqueString(resourceGroup().id),0,3)}sa')
var hostingPlanName = '${UnifiedLogingName}-plan'
var applicationInsightsName = '${UnifiedLogingName}-ai'
var secretName = '${toLower(UnifiedLogingName)}-lakey'

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      name: 'standard'
      family: 'A'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'Storage'
  properties: {
    supportsHttpsTrafficOnly: true
    defaultToOAuthAuthentication: true
  }
}

resource hostingPlan 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
  }
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {}
}

resource functionApp 'Microsoft.Web/sites@2020-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: applicationInsights.properties.InstrumentationKey
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'LogAnalyticsWorkspaceKey'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=${secretName})'
        }
        {
          name: 'LogAnalyticsWorkspace'
          value: logAnalytics.properties.customerId
        }
        {
          name: 'LogTypes'
          value: 'Audit.General,Audit.SharePoint'
        }
      ]
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      netFrameworkVersion: 'v8.0'
      use32BitWorkerProcess: false
      scmType: 'None'
    }
  }
}

resource functionAppZipDeploy 'Microsoft.Web/sites/extensions@2021-02-01' = {
  parent: functionApp
  name: 'MSDeploy'
  properties: { 
    packageUri: '<public URL of the zip file>'
  }
}

resource keyvaultSecretUser 'Microsoft.Authorization/roleDefinitions@2015-07-01' existing = {
  scope: tenant()
  name: '4633458b-17de-408a-b874-0445c86b69e6'
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid('myRoleAssignment')
  scope: keyVault
  properties: {
    principalId: functionApp.identity.principalId
    roleDefinitionId: keyvaultSecretUser.id
  }
}

resource keyVaultSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: secretName
  properties: {
    value: logAnalytics.listKeys().primarySharedKey
  }
}
