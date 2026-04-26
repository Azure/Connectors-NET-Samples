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
- **Trigger callbacks** — typed deserialization of Connector Gateway trigger payloads via `Office365OnNewEmailV3TriggerPayload`

## Quick Start

See [CONTRIBUTING.md](CONTRIBUTING.md) for prerequisites and build/run instructions.

```shell
git clone https://github.com/Azure/Connectors-NET-Samples.git
cd Connectors-NET-Samples/DirectConnector
dotnet build
```

For connector setup and authentication, see [DirectConnector/README.md](DirectConnector/README.md).

## Related Repositories

| Repository | Description |
|------------|-------------|
| [Azure/Connectors-NET-SDK](https://github.com/Azure/Connectors-NET-SDK) | The SDK package — generated connector clients and core abstractions |
| [Azure/Connectors-NET-LSP](https://github.com/Azure/Connectors-NET-LSP) | Language Server Protocol server and VS Code extension for SDK IntelliSense |

## Contributing

This project welcomes contributions and suggestions. See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## License

[MIT](LICENSE)
