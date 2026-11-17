# Azure Connectors .NET SDK — Samples

[![CI](https://github.com/Azure/Connectors-NET-Samples/actions/workflows/ci.yml/badge.svg)](https://github.com/Azure/Connectors-NET-Samples/actions/workflows/ci.yml)

Sample Azure Functions demonstrating the [Azure Connectors .NET SDK](https://github.com/Azure/Connectors-NET-SDK) — calling managed connectors directly from Azure Functions.

> [!NOTE]
> These samples use [Azure.Connectors.Sdk](https://www.nuget.org/packages/Azure.Connectors.Sdk) 1.0.0, the generally available release of the [Azure Connectors .NET SDK](https://github.com/Azure/Connectors-NET-SDK).
>
> The samples demonstrate SDK usage patterns. Review authentication, error handling, data retention, and scale requirements before you adapt them for production.
>
> We welcome feedback and contributions — please [open an issue](https://github.com/Azure/Connectors-NET-Samples/issues) with questions, suggestions, or bug reports.

## What's Inside

The `DirectConnector/` project is an Azure Functions (isolated worker) app with sample functions across 37 connectors, each in its own Functions class:

| File | Connector | Sample Operations |
|------|-----------|-------------------|
| Office365Functions.cs | Office 365 (Mail/Calendar) | Send email, get categories, export email, create calendar event, trigger callbacks |
| SharePointFunctions.cs | SharePoint Online | Discover sites, list libraries, browse folders, download/upload files |
| TeamsFunctions.cs | Microsoft Teams | List teams/channels, get messages, post messages, typed chat-message webhook callback |
| MSGraphFunctions.cs | MS Graph Groups & Users | List users, search groups, get group properties |
| OneDriveFunctions.cs | OneDrive for Business | Browse folders, download/upload files, search, share links, get file metadata, delete files |
| Office365UsersFunctions.cs | Office 365 Users | Get my profile, user lookup, manager/reports chain, search users |
| MQFunctions.cs | IBM MQ | Send, browse, receive, delete messages |
| SmtpFunctions.cs | SMTP | Send email via SMTP |
| AzureBlobFunctions.cs | Azure Blob Storage | Upload, download, get metadata, delete blobs |
| AzureLogAnalyticsFunctions.cs | Azure Monitor Logs | List subscriptions and resources, run queries, discover query schemas |
| ArmFunctions.cs | Azure Resource Manager (ARM) | List subscriptions/resource groups/resources, read, create or update, and delete resource groups |
| ExcelOnlineFunctions.cs | Excel Online (Business) | Get sources, get drives, get tables |
| AzureEventGridFunctions.cs | Azure Event Grid | List topic types, list subscriptions |
| YammerFunctions.cs | Yammer (Viva Engage) | Get networks |
| WdatpFunctions.cs | Microsoft Defender ATP | Get alerts |
| UniversalPrintFunctions.cs | Universal Print | List shares |
| AzureQueuesFunctions.cs | Azure Storage Queues | List storage accounts and queues, create a queue, send, receive, and delete messages |
| AzureTablesFunctions.cs | Azure Storage Tables | Create, read, and delete tables; create, read, list, merge, replace, and upsert entities |
| DocumentDBFunctions.cs | Azure Cosmos DB | List accounts, list databases |
| EventHubsFunctions.cs | Azure Event Hubs | List hubs, list content types |
| OutlookFunctions.cs | Outlook | List calendars, list contact folders |
| ServiceBusFunctions.cs | Azure Service Bus | List queues, list topics, list entities |
| WordOnlineBusinessFunctions.cs | Word Online (Business) | Get sources and drives, create a document, convert to PDF, populate templates |
| DataverseFunctions.cs | Microsoft Dataverse | Discover environments and tables; create, read, update, and delete records; receive typed row-added callbacks |
| AzureAutomationFunctions.cs | Azure Automation | List subscriptions, create a runbook job, and get job status |
| AzureDigitalTwinsFunctions.cs | Azure Digital Twins | List models |
| AzureVMFunctions.cs | Azure Virtual Machines | List subscriptions and get a virtual machine without changing its power state |
| KeyVaultFunctions.cs | Azure Key Vault | List secret metadata without reading secret values |
| MicrosoftBookingsFunctions.cs | Microsoft Bookings | Discovery only: list booking businesses used as appointment-trigger inputs |
| Office365GroupsFunctions.cs | Microsoft 365 Groups | List the first page of groups |
| Office365GroupsMailFunctions.cs | Microsoft 365 Groups Mail | List groups and group conversations |
| OneNoteFunctions.cs | OneNote | List notebooks |
| PlannerFunctions.cs | Microsoft Planner | List groups and auto-page tasks assigned to the user |
| PowerBIFunctions.cs | Power BI | List workspaces and scorecards |
| ShiftsFunctions.cs | Microsoft Shifts | List teams and cross-team shifts |
| TodoFunctions.cs | Microsoft To Do | List task lists |
| ServiceNowFunctions.cs | ServiceNow | Multipart attachment upload, download, and cleanup on a temporary incident |

### Key Patterns Demonstrated

- **DI-based lifetime** — connector clients registered as singletons, disposed by the host
- **Managed Identity** — `DefaultAzureCredential`, system-assigned, or user-assigned MSI
- **Pagination** — `await foreach` over `AsyncPageable<T>` for auto-paged results
- **Binary content** — `byte[]` download/upload for files and email export
- **Multipart upload** — `MultipartFile` with a file name and content type for ServiceNow attachments
- **Error handling** — connector-specific exceptions surfaced as 502 with structured JSON, generic `IsFatal()` fallback
- **Trigger callbacks** — typed deserialization of Connector Gateway polling and webhook payloads

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
