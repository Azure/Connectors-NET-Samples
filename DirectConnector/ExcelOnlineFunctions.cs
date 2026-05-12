//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.ExcelOnline;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Excel Online operations using the generated
/// <see cref="ExcelOnlineClient"/> from the Azure Connectors SDK.
/// </summary>
public class ExcelOnlineFunctions
{
    private readonly ILogger<ExcelOnlineFunctions> _logger;
    private readonly ExcelOnlineClient _excelOnlineClient;

    public ExcelOnlineFunctions(
        ILogger<ExcelOnlineFunctions> logger,
        ExcelOnlineClient excelOnlineClient)
    {
        this._logger = logger;
        this._excelOnlineClient = excelOnlineClient;
    }

    /// <summary>
    /// Gets tables from an Excel workbook in OneDrive for Business.
    /// </summary>
    [Function("ExcelGetTables")]
    public async Task<HttpResponseData> ExcelGetTablesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "excel/tables")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var documentLibrary = request.Query["documentLibrary"] ?? "Documents";
        var file = request.Query["file"];

        if (string.IsNullOrEmpty(file))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Missing required query parameter 'file'. Example: ?file=TestWorkbook.xlsx&documentLibrary=Documents." }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        this._logger.LogInformation("ExcelGetTables: Using generated ExcelOnlineClient from SDK.");

        try
        {
            var tables = await this._excelOnlineClient
                .GetTablesAsync(documentLibrary: documentLibrary, file: file, cancellationToken: cancellationToken)
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
