# DirectConnector Sample

This sample demonstrates calling Azure managed connectors directly from an Azure Function using the [Azure Connectors .NET SDK](https://github.com/Azure/Connectors-NET-SDK) — without the workflow engine.

## What This Demonstrates

| Endpoint | Connector | Pattern Exercised |
|----------|-----------|-------------------|
| `POST /api/email` | Office365 | Void return, JSON input, `CancellationToken` propagation |
| `GET /api/categories` | Office365 | JSON deserialization of structured response |
| `POST /api/triggerCallback` | Office365 | Typed `OnNewEmail` trigger callback deserialization with `ConnectorTriggerPayload` |
| `GET /api/sharepoint/sites` | SharePoint | Discover accessible sites before selecting a site-scoped list or file operation |
| `GET /api/sharepoint/lists?site=...` | SharePoint | JSON wrapper with collection of `{ name, displayName }` items |
| `GET /api/sharepoint/files?site=...&folder=...` | SharePoint | Folder browsing, JSON wrapper with `files` array of projected `BlobMetadata` fields |
| `GET /api/sharepoint/download?site=...&path=...` | SharePoint | **Binary content (`byte[]`) response** via `ReadAsByteArrayAsync` |
| `POST /api/sharepoint/upload` | SharePoint | **Binary content (`byte[]`) input** to create files |
| `GET /api/email/export?messageId=...` | Office365 | **Binary content (`byte[]`) response** — RFC822 `.eml` export |
| `GET /api/onedrive/root` | OneDrive | Root folder listing, JSON wrapper with `files` array of projected `BlobMetadata` fields |
| `GET /api/onedrive/files?folder=...` | OneDrive | Folder browsing with paginated `BlobMetadataPage` (includes `nextLink`) |
| `GET /api/onedrive/download?path=...` | OneDrive | **Binary content (`byte[]`) response** via `ReadAsByteArrayAsync` |
| `POST /api/onedrive/upload` | OneDrive | **Binary content (`byte[]`) input** to create files |
| `GET /api/onedrive/search?query=...` | OneDrive | File search with `FindFilesByPathAsync` |
| `POST /api/onedrive/share` | OneDrive | Create sharing link (`view` or `edit`) |
| `POST /api/onedriveTriggerCallback` | OneDrive | **Trigger callback** — handles both JSON metadata (OnNewFilesV2) and binary file-content (OnNewFileV2) payloads |
| `GET /api/excel/tables` | Excel Online | List tables in a workbook |
| `GET /api/eventgrid/topictypes` | Azure Event Grid | List available topic types |
| `GET /api/eventgrid/subscriptions` | Azure Event Grid | List event subscriptions |
| `GET /api/yammer/networks` | Yammer (Viva Engage) | List networks for the authenticated user |
| `GET /api/wdatp/alerts` | Microsoft Defender ATP | List alerts (paginated via `await foreach`) |
| `GET /api/universalprint/shares` | Universal Print | List recent printer shares |
| `GET /api/dataverse/environments` | Microsoft Dataverse | Discover accessible Dataverse environments |
| `GET /api/dataverse/tables?environment=...` | Microsoft Dataverse | List tables in an environment |
| `GET /api/dataverse/items?environment=...&tableName=...` | Microsoft Dataverse | List records across all connector pages; supports `$filter`, `$orderby`, and `$top` |
| `GET /api/dataverse/nextpage?nextLink=...` | Microsoft Dataverse | Follow a connector next-link value to retrieve the next page |
| `GET /api/dataverse/items/{itemIdentifier}?environment=...&tableName=...` | Microsoft Dataverse | Read a record by ID |
| `POST /api/dataverse/items?environment=...&tableName=...` | Microsoft Dataverse | Create a record from a JSON body |
| `PATCH /api/dataverse/items/{itemIdentifier}?environment=...&tableName=...` | Microsoft Dataverse | Update record fields from a JSON body |
| `POST /api/dataverse/items/{itemIdentifier}/attachments?environment=...&tableName=...&fileName=...` | Microsoft Dataverse | Create a note attachment from binary request content |
| `DELETE /api/dataverse/items/{itemIdentifier}?environment=...&tableName=...` | Microsoft Dataverse | Delete a record |
| `POST /api/dataverse/trigger/newitems` | Microsoft Dataverse | Typed `OnNewItems` Connector Gateway callback deserialization with `ConnectorTriggerPayload` |
| `GET /api/loganalytics/subscriptions` | Azure Monitor Logs | Paginated subscription discovery |
| `GET /api/loganalytics/workspaces?subscription=...&resourceGroup=...` | Azure Monitor Logs | List Log Analytics workspaces in a resource group |
| `POST /api/loganalytics/query?subscription=...&resourceGroup=...&resourceType=...&resourceName=...` | Azure Monitor Logs | Dynamic-schema query results (`Row.AdditionalProperties`) |
| `POST /api/loganalytics/queryschema?subscription=...&resourceGroup=...&resourceType=...&resourceName=...` | Azure Monitor Logs | Query-schema discovery from a plain-text KQL request body |
| `GET /api/arm/subscriptions` | Azure Resource Manager | Paginated subscription discovery |
| `GET /api/arm/subscriptions/{subscriptionId}/resourcegroups` | Azure Resource Manager | Paginated resource-group discovery |
| `GET /api/arm/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}` | Azure Resource Manager | Read a resource group |
| `PUT /api/arm/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}` | Azure Resource Manager | Create or update a resource group from a JSON body with `location` |
| `DELETE /api/arm/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}` | Azure Resource Manager | Delete a resource group |
| `GET /api/arm/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/resources` | Azure Resource Manager | List resources in a resource group |

### Key Patterns

- **DI-based lifetime management** — Connector clients are registered as singletons in the DI container. The host owns their lifetime and calls `Dispose()`, which disposes internally-created `HttpClient` and `DefaultAzureCredential` (ownership-based disposal).
- **Managed Identity support** — Clients support three authentication modes via configuration: `DefaultAzureCredential` chain (default), system-assigned managed identity (set `ManagedIdentityClientId` to empty string), or user-assigned managed identity (set `ManagedIdentityClientId` to the client ID).
- **Binary content handling** — The generated `CallConnectorAsync<byte[]>` detects `byte[]` as the response type and uses `ReadAsByteArrayAsync` instead of JSON deserialization.
- **Connector-specific exceptions** — Each connector generates its own exception type (`Office365ConnectorException`, `SharepointonlineConnectorException`) with `StatusCode` and `ResponseBody` for diagnostics.
- **CancellationToken propagation** — Tokens from the Azure Functions isolated worker are piped through to all SDK calls for graceful shutdown.

## How it Works

1. **Azure Function** - A simple HTTP-triggered function
2. **Azure Identity** - Uses `DefaultAzureCredential` to authenticate
3. **Direct API Call** - Calls the connector's REST API endpoint directly
4. **API Connection** - Uses an existing API Connection you've created in Azure

## E2E-validated operation expansion

The samples use SDK `0.15.0-preview.1`. This change includes only additions
already exercised successfully end to end: queue creation and receipt-based
message deletion, table/entity reads and writes listed below, and OneDrive
metadata/deletion. Existing Word source and drive discovery remains unchanged.

The selected clients expose five Azure Queues methods and eleven Azure Tables
methods. Across the 36 configured client types, sample code calls 115 of 680
named SDK methods. These are code coverage counts, not a claim that every
pre-existing sample operation is E2E validated.

### Azure Queues

| Route | Generated SDK method | Input / output |
|---|---|---|
| `GET /api/azurequeues/storageaccounts` | `GetStorageAccountsAsync` | Storage-account discovery |
| `GET /api/azurequeues/queues` | `ListQueuesAsync` | Required `storageAccount` query |
| `PUT /api/azurequeues/queues` | `PutQueueAsync` | Required `storageAccount`, `queueName` queries |
| `POST /api/azurequeues/messages` | `GetMessagesAsync` | Same selectors; optional `numberOfMessages` (1-32) and `visibilityTimeout` (0-604800 seconds) |
| `DELETE /api/azurequeues/messages` | `DeleteMessageAsync` | Same selectors plus returned `messageId` and `popReceipt` |

Receiving changes message visibility, so it remains POST rather than a
read-only GET. Use only owned queues/messages for lifecycle tests. Sending is
excluded from this change because SDK 0.15 adds JSON quotes to its string body;
that operation belongs with the pending serialization fix, not the validated
plain-text send examples. Queue deletion is not exposed by the SDK; arrange
independent cleanup for an owned test queue.

### Azure Tables

Use required `storageAccount` and `table` query parameters for table/entity
operations. Single-entity reads and writes also require `partitionKey` and
`rowKey`. Merge/replacement bodies contain entity columns; upsert bodies must
include nonempty `PartitionKey` and `RowKey` strings.

| Route | Generated SDK method | Input / output |
|---|---|---|
| `GET /api/azuretables/storageaccounts` | `GetStorageAccountsAsync` | Account discovery |
| `GET /api/azuretables/tables` | `GetTablesAsync` | Table discovery |
| `GET /api/azuretables/table` | `GetTableAsync` | Table metadata |
| `DELETE /api/azuretables/table` | `DeleteTableAsync` | Explicit deletion result |
| `GET /api/azuretables/entities` | `GetEntitiesAsync` | Optional `$filter`, `$select` queries |
| `GET /api/azuretables/entity` | `GetEntityAsync` | Optional `$select` query |
| `POST /api/azuretables/entities` | `CreateEntityAsync` | JSON object containing non-empty `PartitionKey` and `RowKey` strings plus entity columns |
| `PATCH /api/azuretables/entity` | `MergeEntityAsync` | JSON entity columns |
| `PUT /api/azuretables/entity` | `ReplaceEntityAsync` | JSON replacement entity |
| `PATCH /api/azuretables/entity/upsert` | `InsertMergeEntityAsync` | JSON merge entity |
| `PUT /api/azuretables/entity/upsert` | `InsertReplaceEntityAsync` | JSON replacement entity |

Provision a disposable table independently before testing these operations.
ARM/portal provisioning is setup, not proof of SDK table creation. SDK table
creation and entity deletion are excluded until their observed failures are
resolved. Delete only an owned table; parent-table deletion cleans up its test
entities without relying on the excluded entity-delete route.

### OneDrive for Business

| Route | Generated SDK method | Input / output |
|---|---|---|
| `GET /api/onedrive/metadata?file=...` | `GetFileMetadataAsync` | Full typed file metadata |
| `DELETE /api/onedrive/files?file=...` | `DeleteFileAsync` | Explicit deletion result |

Use identifiers returned by this connection and uniquely owned temporary files.
The metadata/deletion paths were validated with synthetic DOCX upload and
verified cleanup. No Word-template selector mapping is assumed.

### Reproducible validated discovery smoke tests

Configure connection URLs privately as described below. The runner performs 34
non-mutating discovery/read checks: 33 previously successful data reads plus
the explicitly accepted Defender `ViewData` permission rejection. It excludes
Event Hubs hub listing, Service Bus queue/topic listing, and Office 365 Groups
listing because those calls still fail authorization or time out.

```powershell
$functions = .\Invoke-ConnectorSmoke.ps1 -ListFunctions
func start --port 7071 --functions $functions
```

From another PowerShell 7 terminal:

```powershell
.\Invoke-ConnectorSmoke.ps1 -BaseUri http://127.0.0.1:7071 -OutputPath "$env:TEMP\connector-smoke-results.json"
```

The output distinguishes `Succeeded`, `ExpectedPermissionFailure`, and
`Failed`. The exact Defender rejection confirms SDK dispatch/error handling,
not successful alert-data access. Every other failure makes the runner throw.
All 17 nested collection paths require arrays, allowing empty/nonempty arrays;
Graph users and Teams membership use the Function's projected flat arrays.
The runner never saves service payloads, runtime URLs, or credentials.

Run `Invoke-ConnectorSmoke.Tests.ps1` for the 19 deterministic regression
scenarios, which also run in CI. CI is restricted to `main` and the explicitly
stacked prerequisite/sample branches. Live checks require private credentials.
Stop the local host after validation.

### Deferred operations

A separate draft preserves Word template schema/population, content-based Word
creation and PDF conversion; Azure Tables creation/entity deletion; queue
sending; and the four failing discovery calls. These are not shipped here as
working E2E examples. The draft records the individual failures or missing
validation and the generator/SDK fix dependency.

## Setup

### 1. Create an API Connection in Azure

Create an Office365 (or other) connection in the Azure Portal:

1. Go to Azure Portal → Create Resource → API Connection
2. Choose "Office 365 Outlook"
3. Authorize with your account
4. Note the connection's Runtime URL (available via ARM or portal)

### 2. Grant Access to Your Identity

**Important**: The API connection has access control. You need to add your identity:

1. Go to Azure Portal → Your API Connection → Access policies
2. Add your user identity or the Azure CLI app ID: `1950a258-227b-4e31-a9cf-717495945fc2`
3. Grant appropriate permissions (Read, Write, or both)

Alternatively, for production scenarios:

- Create a Managed Identity for your Function App
- Add that Managed Identity to the connection's access policies

### 3. Configure Authentication Mode

The SDK supports three authentication modes, configured per connector via the `ManagedIdentityClientId` setting:

| Mode | Config | When to Use |
|------|--------|-------------|
| **DefaultAzureCredential** (default) | Omit `ManagedIdentityClientId` entirely | Local development (uses Azure CLI, env vars, etc.) |
| **System-assigned MSI** | Set `ManagedIdentityClientId` to `""` (empty string) | Production: Function App with system-assigned identity enabled |
| **User-assigned MSI** | Set `ManagedIdentityClientId` to the client ID GUID | Production: shared identity across multiple resources |

**Example — system-assigned MSI in Azure:**

```json
{
  "Connectors:Office365:ManagedIdentityClientId": "",
  "Connectors:SharePoint:ManagedIdentityClientId": ""
}
```

**Example — user-assigned MSI:**

```json
{
  "Connectors:Office365:ManagedIdentityClientId": "12345678-abcd-4abc-9def-1234567890ab",
  "Connectors:SharePoint:ManagedIdentityClientId": "12345678-abcd-4abc-9def-1234567890ab"
}
```

**Access policy requirements for MSI:**

1. Go to Azure Portal → Your API Connection → Access policies
2. Add the managed identity's Object ID (for system-assigned: the Function App's identity; for user-assigned: the identity resource's principal ID)
3. The identity also needs the `Microsoft.Web/connections/*/read` permission on the connection resource

### 4. Configure the Connection URLs

Add your connection runtime URLs to `local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Connectors:Office365:ConnectionRuntimeUrl": "https://YOUR-INSTANCE.azure-apihub.net/apim/office365/YOUR-CONNECTION-ID",
    "Connectors:SharePoint:ConnectionRuntimeUrl": "https://YOUR-INSTANCE.azure-apihub.net/apim/sharepointonline/YOUR-CONNECTION-ID",
    "Connectors:OneDrive:ConnectionRuntimeUrl": "https://YOUR-INSTANCE.azure-apihub.net/apim/onedriveforbusiness/YOUR-CONNECTION-ID",
    "Connectors:Dataverse:ConnectionRuntimeUrl": "https://YOUR-INSTANCE.azure-apihub.net/apim/commondataservice/YOUR-CONNECTION-ID"
  }
}
```

### 5. Build and Run

```powershell
cd DirectConnector
dotnet build
cd bin\Debug\net8.0
func start --no-build
```

### 6. Test

```powershell
# Send email
Invoke-RestMethod -Uri "http://localhost:7071/api/email" -Method POST `
    -Body '{"to":"someone@example.com","subject":"Test","body":"<p>Hello</p>"}' `
    -ContentType "application/json"

# Get Outlook categories
Invoke-RestMethod -Uri "http://localhost:7071/api/categories"

# List SharePoint libraries
Invoke-RestMethod -Uri "http://localhost:7071/api/sharepoint/lists?site=https://contoso.sharepoint.com/sites/mysite"

# Discover accessible SharePoint sites before using a site-scoped operation.
Invoke-RestMethod -Uri "http://localhost:7071/api/sharepoint/sites"

# Browse files in root folder
Invoke-RestMethod -Uri "http://localhost:7071/api/sharepoint/files?site=https://contoso.sharepoint.com/sites/mysite"

# Browse files in a specific folder (use file ID from previous response)
Invoke-RestMethod -Uri "http://localhost:7071/api/sharepoint/files?site=https://contoso.sharepoint.com/sites/mysite&folder=FOLDER_ID"

# Download file content (binary byte[] response)
Invoke-RestMethod -Uri "http://localhost:7071/api/sharepoint/download?site=https://contoso.sharepoint.com/sites/mysite&path=/Shared%20Documents/test.txt" -OutFile "downloaded.txt"

# Upload a text file to SharePoint (byte[] input)
Invoke-RestMethod -Uri "http://localhost:7071/api/sharepoint/upload" -Method POST `
    -Body '{"site":"https://contoso.sharepoint.com/sites/mysite","folderPath":"/Shared Documents","fileName":"hello.txt","content":"Hello from DirectClient SDK!"}' `
    -ContentType "application/json"

# Upload a binary file (base64-encoded)
$base64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes("local-file.docx"))
$body = @{site="https://contoso.sharepoint.com/sites/mysite";folderPath="/Shared Documents";fileName="uploaded.docx";contentBase64=$base64} | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:7071/api/sharepoint/upload" -Method POST -Body $body -ContentType "application/json"

# List OneDrive root folder
Invoke-RestMethod -Uri "http://localhost:7071/api/onedrive/root"

# Browse files in a specific OneDrive folder (use file ID from previous response)
Invoke-RestMethod -Uri "http://localhost:7071/api/onedrive/files?folder=FOLDER_ID"

# Download file from OneDrive (binary byte[] response)
Invoke-RestMethod -Uri "http://localhost:7071/api/onedrive/download?path=/Documents/test.txt" -OutFile "downloaded.txt"

# Upload a text file to OneDrive
Invoke-RestMethod -Uri "http://localhost:7071/api/onedrive/upload" -Method POST `
    -Body '{"folderPath":"/Documents","fileName":"hello.txt","content":"Hello from DirectClient SDK!"}' `
    -ContentType "application/json"

# Upload a binary file to OneDrive (base64-encoded)
$base64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes("local-file.docx"))
$body = @{folderPath="/Documents";fileName="uploaded.docx";contentBase64=$base64} | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:7071/api/onedrive/upload" -Method POST -Body $body -ContentType "application/json"

# Search for files in OneDrive
Invoke-RestMethod -Uri "http://localhost:7071/api/onedrive/search?query=report"

# Search in a specific folder
Invoke-RestMethod -Uri "http://localhost:7071/api/onedrive/search?query=report&folder=/Documents"

# Create a view-only sharing link
Invoke-RestMethod -Uri "http://localhost:7071/api/onedrive/share" -Method POST `
    -Body '{"fileId":"FILE_ID","linkType":"view"}' `
    -ContentType "application/json"

# Create an edit sharing link with organization scope
Invoke-RestMethod -Uri "http://localhost:7071/api/onedrive/share" -Method POST `
    -Body '{"fileId":"FILE_ID","linkType":"edit","linkScope":"organization"}' `
    -ContentType "application/json"

# Discover Dataverse environments. Use a returned environment URL in later calls.
Invoke-RestMethod -Uri "http://localhost:7071/api/dataverse/environments"

# List tables in an environment. The URL is encoded as a query parameter.
$environment = [uri]::EscapeDataString("https://contoso.crm.dynamics.com")
Invoke-RestMethod -Uri "http://localhost:7071/api/dataverse/tables?environment=$environment"

# Create a record in a writable table, then use the identifier from its response for CRUD operations.
$tableName = "accounts"
$created = Invoke-RestMethod -Uri "http://localhost:7071/api/dataverse/items?environment=$environment&tableName=$tableName" -Method POST `
  -Body '{"name":"Connector SDK sample account"}' `
  -ContentType "application/json"
$itemIdentifier = $created.accountid
Invoke-RestMethod -Uri "http://localhost:7071/api/dataverse/items/$itemIdentifier?environment=$environment&tableName=$tableName"
Invoke-RestMethod -Uri "http://localhost:7071/api/dataverse/items/$itemIdentifier?environment=$environment&tableName=$tableName" -Method PATCH `
  -Body '{"name":"Connector SDK sample account (updated)"}' `
  -ContentType "application/json"

# Attach a text file to the created record. The request body is passed as binary content.
$attachmentName = [uri]::EscapeDataString("sample-note.txt")
Invoke-RestMethod -Uri "http://localhost:7071/api/dataverse/items/$itemIdentifier/attachments?environment=$environment&tableName=$tableName&fileName=$attachmentName" -Method POST `
  -Body "Connector SDK attachment sample" `
  -ContentType "text/plain"

# When a Dataverse list response supplies a next-link value, encode it and follow it with the generated helper.
$nextLink = [uri]::EscapeDataString("NEXT_LINK_VALUE")
Invoke-RestMethod -Uri "http://localhost:7071/api/dataverse/nextpage?nextLink=$nextLink"
Invoke-RestMethod -Uri "http://localhost:7071/api/dataverse/items/$itemIdentifier?environment=$environment&tableName=$tableName" -Method DELETE
```

### Dataverse Trigger Callback

`POST /api/dataverse/trigger/newitems` accepts the typed payload for the generated `OnNewItems` trigger operation. Configure a Connector Namespace trigger config with `CommondataserviceTriggerOperations.OnNewItems` and use this Function endpoint as its callback URL. The route uses function-level authorization, so the callback URL must include a Function key, for example `https://<function-app>.azurewebsites.net/api/dataverse/trigger/newitems?code=<function-key>`.

The currently generated Dataverse row triggers are marked **Admin Only (Deprecated)** by the connector. The callback sample is useful for typed payload handling and future trigger support, but it does not create a tenant-wide trigger configuration automatically.

## Architecture

```text
┌──────────────────────┐     ┌─────────────────────────────┐     ┌──────────────────┐
│   Azure Function     │────>│   API Hub (Connector)       │────>│   Office 365     │
│   (Your Code)        │     │   (Connection Runtime URL)  │     │   (Send Email)   │
└──────────────────────┘     └─────────────────────────────┘     └──────────────────┘
         │                              │
         │                              │
         v                              v
┌──────────────────────┐     ┌─────────────────────────────┐
│  Azure Identity      │     │   Connection Access Policy  │
│  (Your credentials)  │     │   (Must include your ID)    │
└──────────────────────┘     └─────────────────────────────┘
```

## Token Audience

The API Hub accepts tokens for these audiences:

- `https://apihub.azure.com`
- `https://management.core.windows.net/`
- `https://service.flow.microsoft.com/`

## Troubleshooting

### "Audience validation failed"

Update the token scope in `GetApiHubTokenAsync()` to use one of the valid audiences.

### "Permission denied due to missing connection ACL"

Add your identity (user or app) to the API Connection's access policies in Azure Portal.

### "The requested identity has not been assigned"

Make sure you're logged into Azure CLI: `az login`
