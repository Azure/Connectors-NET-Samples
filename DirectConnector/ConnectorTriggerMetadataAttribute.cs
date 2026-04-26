//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace DirectConnector;

/// <summary>
/// Decorates a trigger function with connector metadata for design-time IntelliSense.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is a POC placeholder for the future <c>[ConnectorTrigger]</c> attribute
/// in the Azure Functions connector extension. It captures the connector name, trigger operation,
/// and connection settings key so the SDK LSP can provide:
/// </para>
/// <list type="bullet">
/// <item>Filtered trigger operation completions based on <see cref="ConnectorName"/></item>
/// <item>Typed payload suggestions based on <see cref="OperationName"/></item>
/// <item>Connection validation based on <see cref="Connection"/></item>
/// </list>
/// <para>
/// At runtime, this attribute has no effect — the actual trigger binding is still
/// <c>[HttpTrigger]</c> receiving Connector Gateway callbacks. When the Functions extension
/// ships, this will be replaced by the real <c>[ConnectorTrigger]</c>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ConnectorTriggerMetadataAttribute : Attribute
{
    /// <summary>
    /// The connector API name. Use constants from <see cref="Microsoft.Azure.Connectors.Sdk.ConnectorNames"/>.
    /// </summary>
    public string ConnectorName { get; set; } = "";

    /// <summary>
    /// The trigger operation name. Use constants from the connector's <c>*TriggerOperations</c> class
    /// (e.g., <see cref="Microsoft.Azure.Connectors.DirectClient.Office365.Office365TriggerOperations"/>).
    /// </summary>
    public string OperationName { get; set; } = "";

    /// <summary>
    /// The app settings key for the connection. Uses the <c>__</c> separator convention
    /// (e.g., <c>"Office365Connection"</c> resolves <c>Office365Connection__aiGatewayName</c>).
    /// </summary>
    public string Connection { get; set; } = "";
}
