# Azure Function for On-Behalf-Of Flow

This repository contains the source code of an Azure Function that acts as the middle-tier API in OAuth 2.0 On-Behalf-Of (OBO) flow. Follow this article for more details.

## Update configuration

- For local development create a local.settings.json file with the below configuration

```json
{
  "IsEncrypted": false,
  "Values": {
    "TenantId": "<tenant_id>",
    "ClientId": "<client_id>",
    "ClientSecret": "<client_secret>",
    "AzureWebJobsStorage": "",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet"
  }
}
```
- For deployment to Azure Function App add the above properties as application settings in the function app
