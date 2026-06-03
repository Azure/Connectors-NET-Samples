//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Diagnostics;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;

        // NOTE: Register a TokenCredential for connector clients.
        // In Development, use DefaultAzureCredential (supports CLI, env vars, etc.).
        // In Production, the SDK defaults to system-assigned managed identity
        // when no TokenCredential is registered in DI.
        if (hostContext.HostingEnvironment.IsDevelopment())
        {
            services.AddSingleton<Azure.Core.TokenCredential>(
                new Azure.Identity.DefaultAzureCredential());
        }

        services.AddOffice365Client(configuration.GetSection("Connectors:Office365"));
        services.AddSharePointOnlineClient(configuration.GetSection("Connectors:SharePoint"));
        services.AddTeamsClient(configuration.GetSection("Connectors:Teams"));
        services.AddOneDriveForBusinessClient(configuration.GetSection("Connectors:OneDrive"));
        services.AddMsGraphGroupsAndUsersClient(configuration.GetSection("Connectors:MsGraph"));
        services.AddAzureBlobClient(configuration.GetSection("Connectors:AzureBlob"));
        services.AddSmtpClient(configuration.GetSection("Connectors:Smtp"));
        services.AddMqClient(configuration.GetSection("Connectors:Mq"));
        services.AddOffice365UsersClient(configuration.GetSection("Connectors:Office365Users"));
        services.AddAzureMonitorLogsClient(configuration.GetSection("Connectors:AzureMonitorLogs"));
        services.AddArmClient(configuration.GetSection("Connectors:Arm"));
        services.AddExcelOnlineBusinessClient(configuration.GetSection("Connectors:ExcelOnlineBusiness"));
        services.AddAzureEventGridClient(configuration.GetSection("Connectors:AzureEventGrid"));
        services.AddYammerClient(configuration.GetSection("Connectors:Yammer"));
        services.AddWdatpClient(configuration.GetSection("Connectors:Wdatp"));
        services.AddUniversalPrintClient(configuration.GetSection("Connectors:UniversalPrint"));
        services.AddAzureQueuesClient(configuration.GetSection("Connectors:AzureQueues"));
        services.AddAzureTablesClient(configuration.GetSection("Connectors:AzureTables"));
        services.AddDocumentDbClient(configuration.GetSection("Connectors:DocumentDB"));
        services.AddEventHubsClient(configuration.GetSection("Connectors:EventHubs"));
        services.AddOutlookClient(configuration.GetSection("Connectors:Outlook"));
        services.AddServiceBusConnectorClient(configuration.GetSection("Connectors:ServiceBus"));
        services.AddWordOnlineBusinessClient(configuration.GetSection("Connectors:WordOnlineBusiness"));

        // SDK v0.12.0: Subscribe to per-connector ActivitySource for OpenTelemetry tracing.
        // Each generated client emits activities under "Azure.Connectors.Sdk.<connector>"
        // (e.g., "Azure.Connectors.Sdk.teams", "Azure.Connectors.Sdk.office365").
        // Use a wildcard listener to capture all connector operations:
        ActivitySource.AddActivityListener(new ActivityListener
        {
            // Match all connector SDK activity sources by prefix.
            ShouldListenTo = source => source.Name.StartsWith(
                ConnectorHttpClient.ActivitySourceName, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => Console.WriteLine(
                $"[Trace] START {activity.OperationName} (source: {activity.Source.Name})"),
            ActivityStopped = activity => Console.WriteLine(
                $"[Trace] STOP  {activity.OperationName} — {activity.Duration.TotalMilliseconds:F0}ms"
                + (activity.Status == ActivityStatusCode.Error ? $" ERROR: {activity.StatusDescription}" : string.Empty)),
        });
    })
    .Build();

host.Run();
