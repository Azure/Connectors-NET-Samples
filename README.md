# Azure Connectors .NET SDK — Samples

Sample Azure Functions demonstrating the [Azure Connectors .NET SDK](https://github.com/Azure/Connectors-NET-SDK) — calling managed connectors directly from Azure Functions, **without a workflow engine**.

## What's Inside

The `DirectConnector/` project is an Azure Functions (isolated worker) app with 40 sample functions across 10 connectors. Newer connectors have dedicated Functions classes; the original three (Office 365, SharePoint, Teams) share `ConnectorFunctions.cs`:

| File | Connector | Sample Operations |
|------|-----------|-------------------|
| ConnectorFunctions.cs | Office 365 (Mail/Calendar) | Send email, get categories, export email, create calendar event, trigger callbacks |
| ConnectorFunctions.cs | SharePoint Online | List sites, browse folders, download/upload files |
| ConnectorFunctions.cs | Microsoft Teams | List teams/channels, get messages, post messages |
| MsGraphFunctions.cs | MS Graph Groups & Users | List users, search groups, get group properties |
| OneDriveFunctions.cs | OneDrive for Business | Browse folders, download/upload files, search, share links |
| Office365UsersFunctions.cs | Office 365 Users | Get my profile, user lookup, manager/reports chain, search users |
| MqFunctions.cs | IBM MQ | Send, browse, receive, delete messages |
| SmtpFunctions.cs | SMTP | Send email via SMTP |
| AzureBlobFunctions.cs | Azure Blob Storage | Upload, download, get metadata, delete blobs |
| AzureLogAnalyticsFunctions.cs | Azure Log Analytics | List subscriptions, list workspaces |

### Key Patterns Demonstrated

- **DI-based lifetime** — connector clients registered as singletons, disposed by the host
- **Managed Identity** — `DefaultAzureCredential`, system-assigned, or user-assigned MSI
- **Pagination** — `await foreach` over `ConnectorPageable` for auto-paged results
- **Binary content** — `byte[]` download/upload for files and email export
- **Error handling** — connector-specific exceptions surfaced as 502 with structured JSON, generic `IsFatal()` fallback
- **Trigger callbacks** — typed deserialization of Connector Gateway payloads

## Quick Start

```shell
git clone https://github.com/Azure/Connectors-NET-Samples.git
cd Connectors-NET-Samples/DirectConnector
copy local.settings.json.template local.settings.json   # Windows
# cp local.settings.json.template local.settings.json    # macOS/Linux
# Edit local.settings.json with your connection runtime URLs
dotnet build
func start
```

For connector setup (creating connections, OAuth consent, access policies), see the [connection-setup skill](https://github.com/Azure/Connectors-NET-SDK/blob/main/.github/skills/connection-setup/SKILL.md) in the SDK repo.

## Related Repositories

| Repository | Description |
|------------|-------------|
| [Azure/Connectors-NET-SDK](https://github.com/Azure/Connectors-NET-SDK) | The SDK package — generated connector clients and core abstractions |
| [Azure/Connectors-NET-LSP](https://github.com/Azure/Connectors-NET-LSP) | Language Server Protocol server and VS Code extension for SDK IntelliSense |

## Contributing

This project welcomes contributions and suggestions. See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## License

[MIT](LICENSE)
