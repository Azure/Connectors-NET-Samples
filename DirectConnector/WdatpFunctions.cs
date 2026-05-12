//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Wdatp;
using Azure.Connectors.Sdk.Wdatp.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Microsoft Defender for Endpoint (WDATP) operations
/// using the generated <see cref="WdatpClient"/> from the Azure Connectors SDK.
/// </summary>
public class WdatpFunctions
{
    private readonly ILogger<WdatpFunctions> _logger;
    private readonly WdatpClient _wdatpClient;

    public WdatpFunctions(
        ILogger<WdatpFunctions> logger,
        WdatpClient wdatpClient)
    {
        this._logger = logger;
        this._wdatpClient = wdatpClient;
    }

    /// <summary>
    /// Lists alerts from Microsoft Defender for Endpoint.
    /// Demonstrates paginated enumeration via <c>await foreach</c> over <c>AsyncPageable</c>.
    /// </summary>
    [Function("WdatpGetAlerts")]
    public async Task<HttpResponseData> WdatpGetAlertsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "wdatp/alerts")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("WdatpGetAlerts: Using generated WdatpClient from SDK.");

        try
        {
            var alerts = new List<Alert>();
            await foreach (var alert in this._wdatpClient
                .GetAlertsAsync()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false))
            {
                alerts.Add(alert);
            }

            this._logger.LogInformation("Found '{Count}' alerts.", alerts.Count);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, count = alerts.Count, alerts }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "WdatpGetAlerts failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in WdatpGetAlerts.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
