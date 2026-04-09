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
