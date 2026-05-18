//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.ExcelOnlineBusiness;
using Azure.Connectors.Sdk.ExcelOnlineBusiness.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Excel Online (Business) operations using the generated
/// <see cref="ExcelOnlineBusinessClient"/> from the Azure Connectors SDK.
/// </summary>
/// <remarks>
/// Excel Online Business uses SharePoint-hosted Excel files, which are accessible
/// via the connector's site-based document library model (<see cref="ExcelOnlineBusinessClient.GetSourcesAsync"/>,
/// <see cref="ExcelOnlineBusinessClient.GetDrivesAsync"/>).
/// </remarks>
public class ExcelOnlineFunctions
{
    private readonly ILogger<ExcelOnlineFunctions> _logger;
    private readonly ExcelOnlineBusinessClient _excelOnlineBusinessClient;

    public ExcelOnlineFunctions(
        ILogger<ExcelOnlineFunctions> logger,
        ExcelOnlineBusinessClient excelOnlineBusinessClient)
    {
        this._logger = logger;
        this._excelOnlineBusinessClient = excelOnlineBusinessClient;
    }

    /// <summary>
    /// Lists available SharePoint sources (sites) for the Excel Online Business connector.
    /// This is the top-level discovery call — use it to find a valid <c>location</c> value
    /// for <see cref="ExcelGetDrivesAsync"/> and <see cref="ExcelGetTablesAsync"/>.
    /// </summary>
    [Function("ExcelGetSources")]
    public async Task<HttpResponseData> ExcelGetSourcesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "excel/sources")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ExcelGetSources: listing SharePoint sources via ExcelOnlineBusinessClient.");

        try
        {
            var sources = await this._excelOnlineBusinessClient
                .GetSourcesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, sources }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ExcelGetSources failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ExcelGetSources.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists document libraries (drives) within a SharePoint site.
    /// Requires <c>?location=</c> with a SharePoint site URL from <see cref="ExcelGetSourcesAsync"/>.
    /// Returns drive names and IDs usable as the <c>documentLibrary</c> parameter in <see cref="ExcelGetTablesAsync"/>.
    /// </summary>
    [Function("ExcelGetDrives")]
    public async Task<HttpResponseData> ExcelGetDrivesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "excel/drives")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var location = request.Query["location"];
        if (string.IsNullOrEmpty(location))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Missing required query parameter 'location'. Example: ?location=https://microsoft.sharepoint.com." }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        this._logger.LogInformation("ExcelGetDrives: listing drives for location '{Location}'.", location);

        try
        {
            var drives = await this._excelOnlineBusinessClient
                .GetDrivesAsync(location: location, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, location, drives }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ExcelGetDrives failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ExcelGetDrives.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Gets tables from an Excel workbook in a SharePoint document library.
    /// </summary>
    /// <remarks>
    /// Uses the Excel Online Business connector. Discovery flow:
    /// <list type="number">
    ///   <item>Call <c>GET /api/excel/sources</c> — pick a SharePoint site URL as <c>location</c>.</item>
    ///   <item>Call <c>GET /api/excel/drives?location={url}</c> — pick a drive ID as <c>documentLibrary</c>.</item>
    ///   <item>Upload an xlsx file to the drive (via SharePoint upload or direct).</item>
    ///   <item>Call <c>GET /api/excel/tables?location={url}&amp;documentLibrary={id}&amp;file={name.xlsx}</c>.</item>
    /// </list>
    /// </remarks>
    [Function("ExcelGetTables")]
    public async Task<HttpResponseData> ExcelGetTablesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "excel/tables")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var location = request.Query["location"];
        var documentLibrary = request.Query["documentLibrary"];
        var file = request.Query["file"];

        if (string.IsNullOrEmpty(location) || string.IsNullOrEmpty(documentLibrary) || string.IsNullOrEmpty(file))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Missing required parameters. Example: ?location=https://microsoft.sharepoint.com&documentLibrary={driveId}&file=TestWorkbook.xlsx." }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        this._logger.LogInformation("ExcelGetTables: Using generated ExcelOnlineBusinessClient from SDK.");

        try
        {
            var tables = await this._excelOnlineBusinessClient
                .GetTablesAsync(documentLibrary: documentLibrary, file: file, location: location, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, tables }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ExcelGetTables failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ExcelGetTables.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
