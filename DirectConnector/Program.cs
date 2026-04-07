//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using DirectConnector.Configuration;
using Microsoft.Azure.Connectors.DirectClient.Office365;
using Microsoft.Azure.Connectors.DirectClient.Sharepointonline;
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

        // NOTE(daviburg): Bind connector options from configuration.
        // This uses the Options pattern for hierarchical, validated configuration.
        // See: https://learn.microsoft.com/dotnet/core/extensions/options
        services.AddOptions<ConnectorOptions>()
            .Bind(configuration.GetSection(ConnectorOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient();

        // NOTE(daviburg): Register generated connector clients as singletons.
        // The factory overload lets DI own the instance lifetime and call Dispose,
        // which exercises the ownership-based disposal pattern: the client will
        // dispose its internally-created HttpClient and DefaultAzureCredential.
        // NOTE(daviburg): Validation of ConnectionRuntimeUrl is handled by
        // [Required] attribute + ValidateOnStart() at host initialization.
        services.AddSingleton<Office365Client>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ConnectorOptions>>().Value;

            // NOTE(daviburg): When ManagedIdentityClientId is null, use the default constructor so
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

            // NOTE(daviburg): When ManagedIdentityClientId is null, use the default constructor so
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
    })
    .Build();

host.Run();
