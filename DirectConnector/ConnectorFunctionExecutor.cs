//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

internal static class ConnectorFunctionExecutor
{
    public static async Task<HttpResponseData> ExecuteAsync<T>(
        HttpRequestData request,
        ILogger logger,
        string operationName,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await operation()
                .ConfigureAwait(continueOnCapturedContext: false);
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, value }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            logger.LogError(ex, "Operation '{OperationName}' failed with status '{StatusCode}'.", operationName, ex.Status);

            var response = request.CreateResponse(HttpStatusCode.BadGateway);
            await response
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            logger.LogError(ex, "Operation '{OperationName}' failed.", operationName);

            var response = request.CreateResponse(HttpStatusCode.InternalServerError);
            await response
                .WriteAsJsonAsync(new { success = false, error = ex.Message }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
    }
}