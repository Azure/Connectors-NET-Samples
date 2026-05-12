//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Yammer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Yammer operations using the generated
/// <see cref="YammerClient"/> from the Azure Connectors SDK.
/// </summary>
public class YammerFunctions
{
    private readonly ILogger<YammerFunctions> _logger;
    private readonly YammerClient _yammerClient;

    public YammerFunctions(
        ILogger<YammerFunctions> logger,
        YammerClient yammerClient)
    {
        this._logger = logger;
        this._yammerClient = yammerClient;
    }

    /// <summary>
    /// Gets the list of Yammer networks for the authenticated user.
    /// </summary>
    [Function("YammerGetNetworks")]
    public async Task<HttpResponseData> YammerGetNetworksAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "yammer/networks")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("YammerGetNetworks: Using generated YammerClient from SDK.");

        try
        {
            var networks = await this._yammerClient
                .GetNetworksAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, networks }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "YammerGetNetworks failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in YammerGetNetworks.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
