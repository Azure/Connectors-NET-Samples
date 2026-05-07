//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Core;
using Azure.Identity;
using DirectConnector.Configuration;
using Microsoft.Azure.Connectors.DirectClient.Azureblob;
using Microsoft.Azure.Connectors.DirectClient.Azureloganalytics;
using Microsoft.Azure.Connectors.DirectClient.Mq;
using Microsoft.Azure.Connectors.DirectClient.Msgraphgroupsanduser;
using Microsoft.Azure.Connectors.DirectClient.Office365;
using Microsoft.Azure.Connectors.DirectClient.Office365users;
using Microsoft.Azure.Connectors.DirectClient.Onedriveforbusiness;
using Microsoft.Azure.Connectors.DirectClient.Sharepointonline;
using Microsoft.Azure.Connectors.DirectClient.Smtp;
using Microsoft.Azure.Connectors.DirectClient.Teams;
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

        // NOTE: Resolve the credential once for all connector clients.
        // In Azure: defaults to ManagedIdentityCredential (system-assigned).
        // For user-assigned MSI: set ManagedIdentityClientId in configuration.
        // For local dev: set UseAzureCliCredential=true in local.settings.json.
        services.AddSingleton<TokenCredential>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            if (options.UseAzureCliCredential)
            {
                return new AzureCliCredential();
            }

            if (!string.IsNullOrEmpty(options.ManagedIdentityClientId))
            {
                return new ManagedIdentityCredential(
                    ManagedIdentityId.FromUserAssignedClientId(options.ManagedIdentityClientId));
            }

            return new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
        });

        // NOTE: Register generated connector clients as singletons.
        // The factory overload lets DI own the instance lifetime and call Dispose.
        // All clients share the same credential resolved above.
        // NOTE: Validation of ConnectionRuntimeUrl is handled by
        // [Required] attribute + ValidateOnStart() at host initialization.
        services.AddSingleton<Office365Client>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;
            var credential = serviceProvider.GetRequiredService<TokenCredential>();

            return new Office365Client(options.Office365.ConnectionRuntimeUrl, credential);
        });

        services.AddSingleton<SharepointonlineClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;
            var credential = serviceProvider.GetRequiredService<TokenCredential>();

            return new SharepointonlineClient(options.SharePoint.ConnectionRuntimeUrl, credential);
        });

        services.AddSingleton<TeamsClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;
            var credential = serviceProvider.GetRequiredService<TokenCredential>();

            return new TeamsClient(options.Teams.ConnectionRuntimeUrl, credential);
        });

        services.AddSingleton<OnedriveforbusinessClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;
            var credential = serviceProvider.GetRequiredService<TokenCredential>();

            return new OnedriveforbusinessClient(options.OneDrive.ConnectionRuntimeUrl, credential);
        });

        services.AddSingleton<MsgraphgroupsanduserClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;
            var credential = serviceProvider.GetRequiredService<TokenCredential>();

            return new MsgraphgroupsanduserClient(options.MsGraph.ConnectionRuntimeUrl, credential);
        });

        services.AddSingleton<AzureblobClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;
            var credential = serviceProvider.GetRequiredService<TokenCredential>();

            return new AzureblobClient(options.AzureBlob.ConnectionRuntimeUrl, credential);
        });

        services.AddSingleton<SmtpClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;
            var credential = serviceProvider.GetRequiredService<TokenCredential>();

            return new SmtpClient(options.Smtp.ConnectionRuntimeUrl, credential);
        });

        services.AddSingleton<MqClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;
            var credential = serviceProvider.GetRequiredService<TokenCredential>();

            return new MqClient(options.Mq.ConnectionRuntimeUrl, credential);
        });

        services.AddSingleton<Office365usersClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;
            var credential = serviceProvider.GetRequiredService<TokenCredential>();

            return new Office365usersClient(options.Office365Users.ConnectionRuntimeUrl, credential);
        });

        services.AddSingleton<AzureloganalyticsClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;
            var credential = serviceProvider.GetRequiredService<TokenCredential>();

            return new AzureloganalyticsClient(options.AzureLogAnalytics.ConnectionRuntimeUrl, credential);
        });
    })
    .Build();

host.Run();
