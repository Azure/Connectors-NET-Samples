//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using DirectConnector.Configuration;
using Microsoft.Azure.Connectors.Sdk.Arm;
using Microsoft.Azure.Connectors.Sdk.Azureblob;
using Microsoft.Azure.Connectors.Sdk.Azuremonitorlogs;
using Microsoft.Azure.Connectors.Sdk.Mq;
using Microsoft.Azure.Connectors.Sdk.Msgraphgroupsanduser;
using Microsoft.Azure.Connectors.Sdk.Office365;
using Microsoft.Azure.Connectors.Sdk.Office365users;
using Microsoft.Azure.Connectors.Sdk.Onedriveforbusiness;
using Microsoft.Azure.Connectors.Sdk.Sharepointonline;
using Microsoft.Azure.Connectors.Sdk.Smtp;
using Microsoft.Azure.Connectors.Sdk.Teams;
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

        services.AddSingleton<SharepointonlineClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            // NOTE: When ManagedIdentityClientId is null, use the default constructor so
            // the client relies on DefaultAzureCredential. When ManagedIdentityClientId is non-null
            // (empty string = system-assigned MSI, non-empty = user-assigned MSI), use the MSI constructor.
            return options.SharePoint.ManagedIdentityClientId != null
                ? new SharepointonlineClient(
                    options.SharePoint.ConnectionRuntimeUrl,
                    options.SharePoint.ManagedIdentityClientId)
                : new SharepointonlineClient(options.SharePoint.ConnectionRuntimeUrl);
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

        services.AddSingleton<OnedriveforbusinessClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            return options.OneDrive.ManagedIdentityClientId != null
                ? new OnedriveforbusinessClient(
                    options.OneDrive.ConnectionRuntimeUrl,
                    options.OneDrive.ManagedIdentityClientId)
                : new OnedriveforbusinessClient(options.OneDrive.ConnectionRuntimeUrl);
        });

        services.AddSingleton<MsgraphgroupsanduserClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            return options.MsGraph.ManagedIdentityClientId != null
                ? new MsgraphgroupsanduserClient(
                    options.MsGraph.ConnectionRuntimeUrl,
                    options.MsGraph.ManagedIdentityClientId)
                : new MsgraphgroupsanduserClient(options.MsGraph.ConnectionRuntimeUrl);
        });

        services.AddSingleton<AzureblobClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            return options.AzureBlob.ManagedIdentityClientId != null
                ? new AzureblobClient(
                    options.AzureBlob.ConnectionRuntimeUrl,
                    options.AzureBlob.ManagedIdentityClientId)
                : new AzureblobClient(options.AzureBlob.ConnectionRuntimeUrl);
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

        services.AddSingleton<AzuremonitorlogsClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            return options.AzureLogAnalytics.ManagedIdentityClientId != null
                ? new AzuremonitorlogsClient(
                    options.AzureLogAnalytics.ConnectionRuntimeUrl,
                    options.AzureLogAnalytics.ManagedIdentityClientId)
                : new AzuremonitorlogsClient(options.AzureLogAnalytics.ConnectionRuntimeUrl);
        });

        services.AddSingleton<ArmClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            return options.Arm.ManagedIdentityClientId != null
                ? new ArmClient(
                    options.Arm.ConnectionRuntimeUrl,
                    options.Arm.ManagedIdentityClientId)
                : new ArmClient(options.Arm.ConnectionRuntimeUrl);
        });
    })
    .Build();

host.Run();
