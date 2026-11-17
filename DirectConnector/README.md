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

## Expanded SDK operation coverage

> Draft-only expansion: [PR #89](https://github.com/Azure/Connectors-NET-Samples/pull/89) carries the proven E2E subset. This follow-up preserves blocked or unverified operations; callable-method coverage below is not a claim of successful E2E validation. Keep the draft until the [raw-string SDK fix](https://github.com/Azure/Connectors-NET-SDK/pull/289), entity-deletion failure and Word selector/live-validation gaps are resolved.

The samples use SDK `0.15.0-preview.1`. This expansion exposes every callable
operation in the Word Online Business (6), Azure Queues (6), and Azure Tables
(13) clients. OneDrive also includes file metadata and deletion, so test files
can be discovered and cleaned up through typed SDK operations.

These are sample-method coverage counts, not claims that every operation has
succeeded against a live service. The 36 configured client types expose 680
named SDK methods; the sample code currently calls 122 of them. Trigger constants
and overloads are not counted as additional operations. Full coverage of all
connected clients remains broader than this expansion.

### Word Online Business

| Route | Generated SDK method | Input / output |
|-------|----------------------|----------------|
| `GET /api/wordonline/sources` | `GetSourcesAsync` | Source discovery |
| `GET /api/wordonline/drives` | `GetDrivesAsync` | Personal-drive discovery |
| `GET /api/wordonline/templates/schema` | `GetFileSchemaAsync` | Required `source`, `drive`, `file` queries; schema JSON |
| `POST /api/wordonline/templates/populate` | `CreateFileItemAsync` | Same selectors and a JSON object of template fields; DOCX bytes |
| `POST /api/wordonline/files/content` | `CreateWordFileWithContentAsync` | JSON `{"content":"..."}`; optional `fileName`; result JSON |
| `GET /api/wordonline/files/pdf` | `GetFilePDFAsync` | Required selectors; optional Boolean `extractSensitivityLabel` and `sensitivityLabelMetadata`; PDF bytes |

Use identifiers returned by the authenticated service. OneDrive connection file
IDs and Word drive-item selectors are not assumed interchangeable. Template
operations require an accessible Word document with supported content controls.
Creating a Word file can write to the connected user's root folder; use a unique
temporary name and verify deletion afterward.

### Azure Queues

| Route | Generated SDK method | Input / output |
|-------|----------------------|----------------|
| `GET /api/azurequeues/storageaccounts` | `GetStorageAccountsAsync` | Storage-account discovery |
| `GET /api/azurequeues/queues` | `ListQueuesAsync` | Required `storageAccount` query |
| `PUT /api/azurequeues/queues` | `PutQueueAsync` | Required `storageAccount`, `queueName` queries |
| `POST /api/azurequeues/messages/send` | `PutMessageAsync` | Same selectors and a non-empty text body; each call appends a message |
| `POST /api/azurequeues/messages` | `GetMessagesAsync` | Same selectors; optional `numberOfMessages` and `visibilityTimeout` strings |
| `DELETE /api/azurequeues/messages` | `DeleteMessageAsync` | Same selectors plus returned `messageId` and `popReceipt` |

The managed connector publishes both optional message options as strings, with
no numeric range or format constraints. The sample forwards supplied values
unchanged and preserves downstream validation errors; it does not infer extra
client-side limits from the underlying Storage REST API.

Sending appends a message and receiving changes its visibility, so both use
POST on distinct routes. Do not automatically retry a send after an uncertain
response; first check whether the original message was accepted. Use only owned
queues/messages for lifecycle tests. SDK 0.15 adds JSON quotes to string bodies;
correct plain-text sending remains gated on a released fix and live revalidation.
Queue deletion is not exposed by the SDK; arrange independent cleanup for an
owned test queue.

### Azure Tables

Discovery, entity creation, and single-entity operations take different selectors.
Creation reads `PartitionKey` and `RowKey` from the JSON body, not the query.
Single-entity reads, merge/replacement, and upserts require `partitionKey` and
`rowKey` queries; their update bodies do not require duplicate key fields.
The [minimal-input dispatch tests](../DirectConnector.Tests/AzureTablesFunctionsTests.cs)
exercise these distinctions through the actual handlers and generated client.

| Route | Generated SDK method | Required queries | Body / options |
|---|---|---|---|
| `GET /api/azuretables/storageaccounts` | `GetStorageAccountsAsync` | None | Account discovery |
| `GET /api/azuretables/tables` | `GetTablesAsync` | `storageAccount` | Table discovery |
| `POST /api/azuretables/tables` | `CreateTableAsync` | `storageAccount`, `table` | Deferred string-body operation; requires a released SDK fix |
| `GET /api/azuretables/table` | `GetTableAsync` | `storageAccount`, `table` | Table metadata |
| `DELETE /api/azuretables/table` | `DeleteTableAsync` | `storageAccount`, `table` | Explicit deletion result |
| `GET /api/azuretables/entities` | `GetEntitiesAsync` | `storageAccount`, `table` | Optional `$filter`, `$select` queries |
| `GET /api/azuretables/entity` | `GetEntityAsync` | `storageAccount`, `table`, `partitionKey`, `rowKey` | Optional `$select` query |
| `POST /api/azuretables/entities` | `CreateEntityAsync` | `storageAccount`, `table` | JSON object with nonempty `PartitionKey`, `RowKey` strings and entity columns |
| `PATCH /api/azuretables/entity` | `MergeEntityAsync` | `storageAccount`, `table`, `partitionKey`, `rowKey` | JSON entity columns |
| `PUT /api/azuretables/entity` | `ReplaceEntityAsync` | `storageAccount`, `table`, `partitionKey`, `rowKey` | JSON replacement entity |
| `PATCH /api/azuretables/entity/upsert` | `InsertMergeEntityAsync` | `storageAccount`, `table`, `partitionKey`, `rowKey` | JSON merge columns; body keys not required |
| `PUT /api/azuretables/entity/upsert` | `InsertReplaceEntityAsync` | `storageAccount`, `table`, `partitionKey`, `rowKey` | JSON replacement columns; body keys not required |
| `DELETE /api/azuretables/entity` | `DeleteEntityAsync` | `storageAccount`, `table`, `partitionKey`, `rowKey` | Deferred operation; live 404 remains unresolved |

SDK 0.15's string-body table-create call was rejected with
`InvalidResourceName` for a valid alphanumeric test name. The SDK serializes its
string argument as JSON. Keep this negative result visible; do not report a
successful SDK table creation when an ARM-created fixture was used instead.
A live `DeleteEntityAsync` call also returned 404 immediately after `GetEntityAsync`
confirmed the same entity keys. Treat that rejection separately from successful
reads and writes; table cleanup was verified independently. These observations
are limits of the live validation, not reasons to skip error handling.
For mutation tests, use a unique table and entity, verify each postcondition,
and remove the owned table in a `finally` path.

### OneDrive metadata and cleanup

| Route | Generated SDK method | Input / output |
|-------|----------------------|----------------|
| `GET /api/onedrive/metadata?file=...` | `GetFileMetadataAsync` | Full typed file metadata, including any returned file locator |
| `DELETE /api/onedrive/files?file=...` | `DeleteFileAsync` | Explicit deletion result |

### Reproducible discovery smoke tests

Configure all connection URLs privately as described below. The runner targets
an already-running local Functions host and performs 38 discovery/read operations
across connected connectors without consuming messages or mutating data.

```powershell
# List the exact Functions allowlist without issuing requests.
$functions = .\Invoke-ConnectorSmoke.ps1 -ListFunctions
func start --port 7071 --functions $functions
```

Run the checks from another PowerShell 7 terminal:

```powershell
.\Invoke-ConnectorSmoke.ps1 `
    -BaseUri http://127.0.0.1:7071 `
    -OutputPath "$env:TEMP\connector-smoke-results.json"
```

The output distinguishes `Succeeded`, `ExpectedPermissionFailure`, and
`Failed`. The exact Defender rejection confirms SDK dispatch/error handling,
not successful alert-data access. Every other failure makes the runner throw.
Function responses and connector error details must have JSON object roots;
empty, singleton, and multiple-item root arrays are rejected without unwrapping.
All 17 nested collection paths require arrays, allowing empty/nonempty arrays;
Graph users and Teams membership use the Function's projected flat arrays.
The runner never saves service payloads, runtime URLs, or credentials.

Run `Invoke-ConnectorSmoke.Tests.ps1` for the 24 deterministic regression
scenarios, which run in ordinary PR CI targeting `main`. Stacked PRs run these
checks locally until their prerequisite merges and they are retargeted to `main`;
do not add branch-specific CI exceptions. Live checks require private credentials.
Stop the local host after validation.

### Review regression coverage

The original expansion was reviewed at commit `5861eb3`. Its tests covered
queue dispatch, successful flat collections, and connector HTTP rejections,
but missed the following contracts. The regression checks now exercise the
shipped function bindings and runner rather than reimplementing their decisions.

| Review finding | Previous gap | Regression and controls |
|---|---|---|
| [Send semantics](https://github.com/Azure/Connectors-NET-Samples/pull/89#discussion_r4459327098) | The append operation was exposed as PUT. | The binding test requires POST on the distinct send route; queue creation and message deletion remain unchanged. |
| [Discovery collections](https://github.com/Azure/Connectors-NET-Samples/pull/89#discussion_r4459327213) | Flat-array self-tests did not exercise nested response contracts; SDK types alone do not prove Function response shape. | Runner scenarios reject missing, null, error-shaped, object, string, and array envelopes across all 17 nested routes; empty and nonempty collections and projected flat Graph/Teams arrays pass. Live response-shape checks exposed and corrected the two projection mismatches. |
| [Word binary failures](https://github.com/Azure/Connectors-NET-Samples/pull/89#discussion_r4459327304) | HTTP rejection tests did not exercise transport or token-acquisition failure. | Both population and PDF routes return structured HTTP 500 for each failure; successful bytes and connector HTTP 502 tests remain. |

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
