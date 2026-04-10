# DirectConnector Sample

This sample demonstrates calling Azure managed connectors directly from an Azure Function using the [Azure Connectors .NET SDK](https://github.com/Azure/Connectors-NET-SDK) — without the workflow engine.

## What This Demonstrates

| Endpoint | Connector | Pattern Exercised |
|----------|-----------|-------------------|
| `POST /api/email` | Office365 | Void return, JSON input, `CancellationToken` propagation |
| `GET /api/categories` | Office365 | JSON deserialization of structured response |
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
    "Connectors:OneDrive:ConnectionRuntimeUrl": "https://YOUR-INSTANCE.azure-apihub.net/apim/onedriveforbusiness/YOUR-CONNECTION-ID"
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
```

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
