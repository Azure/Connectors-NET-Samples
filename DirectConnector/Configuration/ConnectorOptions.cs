//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace DirectConnector.Configuration;

/// <summary>
/// Configuration options for all managed connectors.
/// </summary>
public class ConnectorOptions
{
    /// <summary>
    /// Configuration section name in appsettings/local.settings.
    /// </summary>
    public const string SectionName = "Connectors";

    /// <summary>
    /// Office365 connector options.
    /// </summary>
    [ValidateObjectMembers]
    public Office365Options Office365 { get; set; } = new Office365Options();

    /// <summary>
    /// SharePoint Online connector options.
    /// </summary>
    [ValidateObjectMembers]
    public SharePointOptions SharePoint { get; set; } = new SharePointOptions();

    /// <summary>
    /// Microsoft Teams connector options.
    /// </summary>
    [ValidateObjectMembers]
    public TeamsOptions Teams { get; set; } = new TeamsOptions();

    /// <summary>
    /// OneDrive for Business connector options.
    /// </summary>
    [ValidateObjectMembers]
    public OneDriveOptions OneDrive { get; set; } = new OneDriveOptions();

    /// <summary>
    /// MS Graph Groups and Users connector options.
    /// </summary>
    [ValidateObjectMembers]
    public MsGraphOptions MsGraph { get; set; } = new MsGraphOptions();

    /// <summary>
    /// Azure Resource Manager (ARM) connector options.
    /// </summary>
    [ValidateObjectMembers]
    public ArmOptions Arm { get; set; } = new ArmOptions();

    /// <summary>
    /// Azure Blob Storage connector options.
    /// </summary>
    [ValidateObjectMembers]
    public AzureBlobOptions AzureBlob { get; set; } = new AzureBlobOptions();

    /// <summary>
    /// SMTP connector options.
    /// </summary>
    [ValidateObjectMembers]
    public SmtpOptions Smtp { get; set; } = new SmtpOptions();

    /// <summary>
    /// IBM MQ connector options.
    /// </summary>
    [ValidateObjectMembers]
    public MqOptions Mq { get; set; } = new MqOptions();

    /// <summary>
    /// Office 365 Users connector options.
    /// </summary>
    [ValidateObjectMembers]
    public Office365UsersOptions Office365Users { get; set; } = new Office365UsersOptions();

    /// <summary>
    /// Azure Log Analytics connector options.
    /// </summary>
    [ValidateObjectMembers]
    public AzureLogAnalyticsOptions AzureLogAnalytics { get; set; } = new AzureLogAnalyticsOptions();
}

/// <summary>
/// Configuration options for the Microsoft Teams connector.
/// </summary>
public class TeamsOptions
{
    /// <summary>
    /// The API connection runtime URL for Microsoft Teams.
    /// </summary>
    [Required(ErrorMessage = "Teams ConnectionRuntimeUrl is required.")]
    public string ConnectionRuntimeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Managed identity client ID for user-assigned identity.
    /// Set to empty string for system-assigned managed identity.
    /// Leave unset (null) to use the DefaultAzureCredential chain (CLI, env vars, etc.).
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
}

/// <summary>
/// Configuration options for the Office365 connector.
/// </summary>
public class Office365Options
{
    /// <summary>
    /// The API connection runtime URL for Office365.
    /// </summary>
    [Required(ErrorMessage = "Office365 ConnectionRuntimeUrl is required.")]
    public string ConnectionRuntimeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Managed identity client ID for user-assigned identity.
    /// Set to empty string for system-assigned managed identity.
    /// Leave unset (null) to use the DefaultAzureCredential chain (CLI, env vars, etc.).
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
}

/// <summary>
/// Configuration options for the SharePoint Online connector.
/// </summary>
public class SharePointOptions
{
    /// <summary>
    /// The API connection runtime URL for SharePoint Online.
    /// </summary>
    [Required(ErrorMessage = "SharePoint ConnectionRuntimeUrl is required.")]
    public string ConnectionRuntimeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Managed identity client ID for user-assigned identity.
    /// Set to empty string for system-assigned managed identity.
    /// Leave unset (null) to use the DefaultAzureCredential chain (CLI, env vars, etc.).
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
}

/// <summary>
/// Configuration options for the OneDrive for Business connector.
/// </summary>
public class OneDriveOptions
{
    /// <summary>
    /// The API connection runtime URL for OneDrive for Business.
    /// </summary>
    [Required(ErrorMessage = "OneDrive ConnectionRuntimeUrl is required.")]
    public string ConnectionRuntimeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Managed identity client ID for user-assigned identity.
    /// Set to empty string for system-assigned managed identity.
    /// Leave unset (null) to use the DefaultAzureCredential chain (CLI, env vars, etc.).
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
}

/// <summary>
/// Configuration options for the MS Graph Groups and Users connector.
/// </summary>
public class MsGraphOptions
{
    /// <summary>
    /// The API connection runtime URL for MS Graph Groups and Users.
    /// </summary>
    [Required(ErrorMessage = "Connectors:MsGraph:ConnectionRuntimeUrl is required.")]
    public string ConnectionRuntimeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Managed identity client ID for user-assigned identity.
    /// Set to empty string for system-assigned managed identity.
    /// Leave unset (null) to use the DefaultAzureCredential chain (CLI, env vars, etc.).
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
}

/// <summary>
/// Configuration options for the Azure Blob Storage connector.
/// </summary>
public class AzureBlobOptions
{
    /// <summary>
    /// The API connection runtime URL for Azure Blob Storage.
    /// </summary>
    [Required(ErrorMessage = "Connectors:AzureBlob:ConnectionRuntimeUrl is required.")]
    public string ConnectionRuntimeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Managed identity client ID for user-assigned identity.
    /// Set to empty string for system-assigned managed identity.
    /// Leave unset (null) to use the DefaultAzureCredential chain (CLI, env vars, etc.).
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
}

/// <summary>
/// Configuration options for the SMTP connector.
/// </summary>
public class SmtpOptions
{
    /// <summary>
    /// The API connection runtime URL for SMTP.
    /// </summary>
    [Required(ErrorMessage = "Connectors:Smtp:ConnectionRuntimeUrl is required.")]
    public string ConnectionRuntimeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Managed identity client ID for user-assigned identity.
    /// Set to empty string for system-assigned managed identity.
    /// Leave unset (null) to use the DefaultAzureCredential chain (CLI, env vars, etc.).
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
}

/// <summary>
/// Configuration options for the IBM MQ connector.
/// </summary>
public class MqOptions
{
    /// <summary>
    /// The API connection runtime URL for IBM MQ.
    /// </summary>
    [Required(ErrorMessage = "Connectors:Mq:ConnectionRuntimeUrl is required.")]
    public string ConnectionRuntimeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Managed identity client ID for user-assigned identity.
    /// Set to empty string for system-assigned managed identity.
    /// Leave unset (null) to use the DefaultAzureCredential chain (CLI, env vars, etc.).
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
}

/// <summary>
/// Configuration options for the Office 365 Users connector.
/// </summary>
public class Office365UsersOptions
{
    /// <summary>
    /// The API connection runtime URL for Office 365 Users.
    /// </summary>
    [Required(ErrorMessage = "Connectors:Office365Users:ConnectionRuntimeUrl is required.")]
    public string ConnectionRuntimeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Managed identity client ID for user-assigned identity.
    /// Set to empty string for system-assigned managed identity.
    /// Leave unset (null) to use the DefaultAzureCredential chain (CLI, env vars, etc.).
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
}

/// <summary>
/// Configuration options for the Azure Log Analytics connector.
/// </summary>
public class AzureLogAnalyticsOptions
{
    /// <summary>
    /// The API connection runtime URL for Azure Log Analytics.
    /// </summary>
    [Required(ErrorMessage = "Connectors:AzureLogAnalytics:ConnectionRuntimeUrl is required.")]
    public string ConnectionRuntimeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Managed identity client ID for user-assigned identity.
    /// Set to empty string for system-assigned managed identity.
    /// Leave unset (null) to use the DefaultAzureCredential chain (CLI, env vars, etc.).
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
}

/// <summary>
/// Configuration options for the Azure Resource Manager (ARM) connector.
/// </summary>
public class ArmOptions
{
    /// <summary>
    /// The API connection runtime URL for Azure Resource Manager.
    /// </summary>
    [Required(ErrorMessage = "Connectors:Arm:ConnectionRuntimeUrl is required.")]
    public string ConnectionRuntimeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Managed identity client ID for user-assigned identity.
    /// Set to empty string for system-assigned managed identity.
    /// Leave unset (null) to use the DefaultAzureCredential chain (CLI, env vars, etc.).
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
}
