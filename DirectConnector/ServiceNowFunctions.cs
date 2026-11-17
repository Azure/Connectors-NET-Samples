//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.ServiceNow;
using Azure.Connectors.Sdk.ServiceNow.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating ServiceNow multipart attachment upload.
/// </summary>
public class ServiceNowFunctions
{
    internal const string IncidentTable = "incident";
    internal const string DefaultFileName = "connectors-sdk-sample.txt";
    internal const string AttachmentContentType = "text/plain";

    private readonly ServiceNowClient _client;
    private readonly ILogger<ServiceNowFunctions> _logger;

    public ServiceNowFunctions(ILogger<ServiceNowFunctions> logger, ServiceNowClient client)
    {
        this._logger = logger;
        this._client = client;
    }

    /// <summary>
    /// Creates a temporary incident, uploads the plain-text request body to it as a multipart
    /// attachment, downloads the attachment to verify its content, and then deletes the
    /// attachment and the incident. An empty body uploads a generated sample text.
    /// Optional query: <c>fileName</c>.
    /// </summary>
    [Function("ServiceNowAttachmentRoundTrip")]
    public async Task<HttpResponseData> ServiceNowAttachmentRoundTripAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "servicenow/attachments/roundtrip")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var fileName = request.Query["fileName"];
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = ServiceNowFunctions.DefaultFileName;
        }

        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var text = await reader
            .ReadToEndAsync(cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        if (string.IsNullOrEmpty(text))
        {
            text = $"Azure Connectors SDK sample attachment created at {DateTimeOffset.UtcNow:O}.";
        }

        return await ConnectorFunctionExecutor
            .ExecuteAsync(
                request,
                this._logger,
                operationName: "ServiceNowAttachmentRoundTrip",
                operation: () => this.RunAttachmentRoundTripAsync(fileName, Encoding.UTF8.GetBytes(text), cancellationToken),
                cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async Task<object> RunAttachmentRoundTripAsync(string fileName, byte[] content, CancellationToken cancellationToken)
    {
        var incident = await this._client
            .CreateRecordAsync(
                recordType: ServiceNowFunctions.IncidentTable,
                input: new RecordBody
                {
                    AdditionalProperties =
                    {
                        ["short_description"] = JsonSerializer.SerializeToElement("Azure Connectors SDK attachment sample (deleted automatically)"),
                    },
                },
                fields: "sys_id,number",
                cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        var incidentSysId = ServiceNowFunctions.GetRequiredString(incident.Result, "sys_id", "created incident");

        try
        {
            var upload = await this._client
                .UploadAttachmentAsync(
                    attachmentContent: new MultipartFile(content, fileName, ServiceNowFunctions.AttachmentContentType),
                    tableName: ServiceNowFunctions.IncidentTable,
                    tableId: incidentSysId,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            var attachmentSysId = ServiceNowFunctions.GetRequiredString(upload.Result, "sys_id", "uploaded attachment");

            var downloaded = await this._client
                .RetrieveAttachmentContentAsync(attachmentSysId, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            await this._client
                .DeleteAttachmentAsync(attachmentSysId, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            await this._client
                .DeleteRecordAsync(ServiceNowFunctions.IncidentTable, incidentSysId, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return new
            {
                incidentNumber = ServiceNowFunctions.GetOptionalString(incident.Result, "number"),
                incidentSysId,
                attachmentSysId,
                fileName = ServiceNowFunctions.GetOptionalString(upload.Result, "file_name") ?? fileName,
                contentType = ServiceNowFunctions.GetOptionalString(upload.Result, "content_type"),
                uploadedBytes = content.Length,
                downloadedBytes = downloaded.Length,
                contentMatches = content.AsSpan().SequenceEqual(downloaded),
                cleanedUp = true,
            };
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            // NOTE: Remove the temporary incident when a later step fails; ServiceNow deletes its attachments with it.
            try
            {
                await this._client
                    .DeleteRecordAsync(ServiceNowFunctions.IncidentTable, incidentSysId, CancellationToken.None)
                    .ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (Exception cleanupException) when (!cleanupException.IsFatal())
            {
                this._logger.LogWarning(cleanupException, "Could not delete temporary incident '{IncidentSysId}'.", incidentSysId);
            }

            throw;
        }
    }

    private static string GetRequiredString(JsonElement? result, string propertyName, string description)
    {
        return ServiceNowFunctions.GetOptionalString(result, propertyName)
            ?? throw new InvalidOperationException(message: $"The {description} response did not include '{propertyName}'.");
    }

    private static string? GetOptionalString(JsonElement? result, string propertyName)
    {
        return result is { ValueKind: JsonValueKind.Object } element
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
