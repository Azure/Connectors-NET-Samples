//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Connectors.Sdk;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((hostContext, services) =>
    {
        var connectors = hostContext.Configuration.GetSection("Connectors");

        // NOTE: Register generated connector clients as singletons.
        // Each extension method reads ConnectionRuntimeUrl (required) and
        // ManagedIdentityClientId (optional) from the configuration section.
        services
            .AddOffice365Client(connectors.GetSection("Office365"))
            .AddSharepointonlineClient(connectors.GetSection("SharePoint"))
            .AddTeamsClient(connectors.GetSection("Teams"))
            .AddOnedriveforbusinessClient(connectors.GetSection("OneDrive"))
            .AddMsgraphgroupsanduserClient(connectors.GetSection("MsGraph"))
            .AddAzureblobClient(connectors.GetSection("AzureBlob"))
            .AddSmtpClient(connectors.GetSection("Smtp"))
            .AddMqClient(connectors.GetSection("Mq"))
            .AddOffice365usersClient(connectors.GetSection("Office365Users"));
    })
    .Build();

host.Run();
