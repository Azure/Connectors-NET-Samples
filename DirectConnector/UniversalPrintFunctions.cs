//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.UniversalPrint;
using Azure.Connectors.Sdk.UniversalPrint.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Universal Print operations using the generated
/// <see cref="UniversalPrintClient"/> from the Azure Connectors SDK.
/// </summary>
public class UniversalPrintFunctions
{
    private readonly ILogger<UniversalPrintFunctions> _logger;
    private readonly UniversalPrintClient _universalPrintClient;

    public UniversalPrintFunctions(
        ILogger<UniversalPrintFunctions> logger,
        UniversalPrintClient universalPrintClient)
    {
        this._logger = logger;
        this._universalPrintClient = universalPrintClient;
    }

    /// <summary>
    /// Lists recent print shares from Universal Print.
    /// </summary>
    [Function("UniversalPrintListShares")]
    public async Task<HttpResponseData> UniversalPrintListSharesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "universalprint/shares")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("UniversalPrintListShares: Using generated UniversalPrintClient from SDK.");

        try
        {
            var shares = await this._universalPrintClient
                .ListRecentSharesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, shares }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "UniversalPrintListShares failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in UniversalPrintListShares.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
