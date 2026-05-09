//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Connectors.Sdk;
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
        services.AddOffice365usersClient(configuration.GetSection("Connectors:Office365Users"));
        services.AddAzuremonitorlogsClient(configuration.GetSection("Connectors:AzureMonitorLogs"));
        services.AddArmClient(configuration.GetSection("Connectors:Arm"));
    })
    .Build();

host.Run();
