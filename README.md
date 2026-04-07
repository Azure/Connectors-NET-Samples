# Azure Connectors .NET SDK — Samples

Sample Azure Functions applications demonstrating [Azure Connectors .NET SDK](https://github.com/Azure/Connectors-NET-SDK) usage with various connectors (Office 365, SharePoint, Teams, and more).

## DirectConnector Sample

The `DirectConnector/` project is an Azure Functions (isolated worker) app that calls managed connectors directly — **without a workflow engine**. It demonstrates:

| Endpoint | Connector | Pattern |
|----------|-----------|---------|
| `POST /api/email` | Office 365 | Void return, JSON input, `CancellationToken` propagation |
| `GET /api/categories` | Office 365 | JSON deserialization of structured response |
| `GET /api/email/export?messageId=...` | Office 365 | Binary (`byte[]`) response — RFC822 `.eml` export |
| `POST /api/calendar/event` | Office 365 | Calendar event creation with typed input/output |
| `GET /api/sharepoint/lists?site=...` | SharePoint | Collection response with projected fields |
| `GET /api/sharepoint/files?site=...` | SharePoint | Folder browsing with `BlobMetadata` |
| `GET /api/sharepoint/download?site=...&path=...` | SharePoint | Binary content (`byte[]`) download |
| `POST /api/sharepoint/upload` | SharePoint | Binary content (`byte[]`) upload |
| `GET /api/teams/teams` | Teams | List all teams |
| `GET /api/teams/channels?teamId=...` | Teams | List channels for a team |
| `POST /api/teams/message` | Teams | Post message to a channel (dynamic schema) |
| `POST /api/triggerCallback` | Office 365 | Trigger callback with typed payload deserialization |

### Key patterns demonstrated

- **DI-based lifetime management** — connector clients registered as singletons, disposed by the host
- **Managed Identity support** — `DefaultAzureCredential`, system-assigned MSI, or user-assigned MSI
- **Binary content handling** — `byte[]` responses use `ReadAsByteArrayAsync` instead of JSON deserialization
- **Connector-specific exceptions** — `Office365ConnectorException`, `SharepointonlineConnectorException`, `TeamsConnectorException`
- **Dynamic schema** — `DynamicPostMessageRequest` with `[JsonExtensionData]` for runtime-determined properties
- **Trigger callbacks** — typed deserialization of AI Gateway trigger payloads via `Office365OnNewEmailV3TriggerPayload`

## Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) or Azure Storage account
- Azure subscription with API Connections created (Office 365, SharePoint, Teams)

### 1. Clone and restore

The SDK preview package is hosted on [GitHub Packages](https://github.com/Azure/Connectors-NET-SDK/packages), which requires authentication.
Set a `GH_TOKEN` environment variable with a [personal access token](https://github.com/settings/tokens) that has `read:packages` scope:

```shell
# Set your GitHub token (Linux/macOS)
export GH_TOKEN=$(gh auth token)

# Or on Windows PowerShell
$env:GH_TOKEN = (gh auth token)
```

Then clone and restore:

```shell
git clone https://github.com/Azure/Connectors-NET-Samples.git
cd Connectors-NET-Samples/DirectConnector
dotnet restore
```

### 2. Create API Connections in Azure

For each connector you want to use:

1. Go to **Azure Portal** → **Create Resource** → **API Connection**
2. Select the connector (e.g., "Office 365 Outlook")
3. Authorize with your account
4. Note the **Connection Runtime URL** from the connection's properties

### 3. Grant access to your identity

Add your user or app identity to each connection's access policies:

```powershell
# Example: add your user to the Office 365 connection
az rest --method PUT `
  --uri "https://management.azure.com/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Web/connections/{connection}/accessPolicies/my-policy?api-version=2018-07-01-preview" `
  --body '{"properties":{"principal":{"type":"ActiveDirectory","identity":{"objectId":"{your-object-id}","tenantId":"{your-tenant-id}"}}}}'
```

### 4. Configure and run

```powershell
cp local.settings.json.template local.settings.json
# Edit local.settings.json with your connection runtime URLs

dotnet build
func start
```

### 5. Test

```powershell
# Send email
Invoke-RestMethod -Uri "http://localhost:7071/api/email" -Method POST `
  -ContentType "application/json" `
  -Body '{"to":"you@example.com","subject":"Test","body":"<p>Hello from SDK!</p>"}'

# Get Outlook categories
Invoke-RestMethod -Uri "http://localhost:7071/api/categories"
```

See [DirectConnector/README.md](DirectConnector/README.md) for full endpoint documentation, authentication modes, and troubleshooting.

## Related Repositories

| Repository | Description |
|------------|-------------|
| [Azure/Connectors-NET-SDK](https://github.com/Azure/Connectors-NET-SDK) | The SDK package — generated connector clients and core abstractions |

## Contributing

This project welcomes contributions and suggestions. See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## License

[MIT](LICENSE)
