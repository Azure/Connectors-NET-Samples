//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using DirectConnector.Configuration;
using Azure.Connectors.Sdk.AzureBlob;
using Azure.Connectors.Sdk.AzureBlob.Models;
using Microsoft.Azure.Connectors.DirectClient.Azureloganalytics;
using Azure.Connectors.Sdk.Mq;
using Azure.Connectors.Sdk.Mq.Models;
using Azure.Connectors.Sdk.MsGraphGroupsAndUsers;
using Azure.Connectors.Sdk.MsGraphGroupsAndUsers.Models;
using Azure.Connectors.Sdk.Office365;
using Azure.Connectors.Sdk.Office365.Models;
using Azure.Connectors.Sdk.Office365users;
using Azure.Connectors.Sdk.Office365users.Models;
using Azure.Connectors.Sdk.OneDriveForBusiness;
using Azure.Connectors.Sdk.OneDriveForBusiness.Models;
using Azure.Connectors.Sdk.SharePointOnline;
using Azure.Connectors.Sdk.SharePointOnline.Models;
using Azure.Connectors.Sdk.Smtp;
using Azure.Connectors.Sdk.Smtp.Models;
using Azure.Connectors.Sdk.Teams;
using Azure.Connectors.Sdk.Teams.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;

        // NOTE: Bind connector options from configuration.
        // This uses the Options pattern for hierarchical, validated configuration.
        // See: https://learn.microsoft.com/dotnet/core/extensions/options
        services.AddOptions<ConnectorOptions>()
            .Bind(configuration.GetSection(ConnectorOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient();

        // NOTE: Register generated connector clients as singletons.
        // The factory overload lets DI own the instance lifetime and call Dispose,
        // which exercises the ownership-based disposal pattern: the client will
        // dispose its internally-created HttpClient and DefaultAzureCredential.
        // NOTE: Validation of ConnectionRuntimeUrl is handled by
        // [Required] attribute + ValidateOnStart() at host initialization.
        services.AddSingleton<Office365Client>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            // NOTE: When ManagedIdentityClientId is null, use the default constructor so
            // the client relies on DefaultAzureCredential. When ManagedIdentityClientId is non-null
            // (empty string = system-assigned MSI, non-empty = user-assigned MSI), use the MSI constructor.
            return options.Office365.ManagedIdentityClientId != null
                ? new Office365Client(
                    options.Office365.ConnectionRuntimeUrl,
                    options.Office365.ManagedIdentityClientId)
                : new Office365Client(options.Office365.ConnectionRuntimeUrl);
        });

        services.AddSingleton<SharePointOnlineClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            // NOTE: When ManagedIdentityClientId is null, use the default constructor so
            // the client relies on DefaultAzureCredential. When ManagedIdentityClientId is non-null
            // (empty string = system-assigned MSI, non-empty = user-assigned MSI), use the MSI constructor.
            return options.SharePoint.ManagedIdentityClientId != null
                ? new SharePointOnlineClient(
                    options.SharePoint.ConnectionRuntimeUrl,
                    options.SharePoint.ManagedIdentityClientId)
                : new SharePointOnlineClient(options.SharePoint.ConnectionRuntimeUrl);
        });

        services.AddSingleton<TeamsClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            return options.Teams.ManagedIdentityClientId != null
                ? new TeamsClient(
                    options.Teams.ConnectionRuntimeUrl,
                    options.Teams.ManagedIdentityClientId)
                : new TeamsClient(options.Teams.ConnectionRuntimeUrl);
        });

        services.AddSingleton<OneDriveForBusinessClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            return options.OneDrive.ManagedIdentityClientId != null
                ? new OneDriveForBusinessClient(
                    options.OneDrive.ConnectionRuntimeUrl,
                    options.OneDrive.ManagedIdentityClientId)
                : new OneDriveForBusinessClient(options.OneDrive.ConnectionRuntimeUrl);
        });

        services.AddSingleton<MsGraphGroupsAndUsersClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            return options.MsGraph.ManagedIdentityClientId != null
                ? new MsGraphGroupsAndUsersClient(
                    options.MsGraph.ConnectionRuntimeUrl,
                    options.MsGraph.ManagedIdentityClientId)
                : new MsGraphGroupsAndUsersClient(options.MsGraph.ConnectionRuntimeUrl);
        });

        services.AddSingleton<AzureBlobClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            return options.AzureBlob.ManagedIdentityClientId != null
                ? new AzureBlobClient(
                    options.AzureBlob.ConnectionRuntimeUrl,
                    options.AzureBlob.ManagedIdentityClientId)
                : new AzureBlobClient(options.AzureBlob.ConnectionRuntimeUrl);
        });

        services.AddSingleton<SmtpClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            return options.Smtp.ManagedIdentityClientId != null
                ? new SmtpClient(
                    options.Smtp.ConnectionRuntimeUrl,
                    options.Smtp.ManagedIdentityClientId)
                : new SmtpClient(options.Smtp.ConnectionRuntimeUrl);
        });

        services.AddSingleton<MqClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            return options.Mq.ManagedIdentityClientId != null
                ? new MqClient(
                    options.Mq.ConnectionRuntimeUrl,
                    options.Mq.ManagedIdentityClientId)
                : new MqClient(options.Mq.ConnectionRuntimeUrl);
        });

        services.AddSingleton<Office365usersClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            return options.Office365Users.ManagedIdentityClientId != null
                ? new Office365usersClient(
                    options.Office365Users.ConnectionRuntimeUrl,
                    options.Office365Users.ManagedIdentityClientId)
                : new Office365usersClient(options.Office365Users.ConnectionRuntimeUrl);
        });

        services.AddSingleton<AzureloganalyticsClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            return options.AzureLogAnalytics.ManagedIdentityClientId != null
                ? new AzureloganalyticsClient(
                    options.AzureLogAnalytics.ConnectionRuntimeUrl,
                    options.AzureLogAnalytics.ManagedIdentityClientId)
                : new AzureloganalyticsClient(options.AzureLogAnalytics.ConnectionRuntimeUrl);
        });
    })
    .Build();

host.Run();
