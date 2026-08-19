//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Connectors.Sdk.MicrosoftBookings;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating read-only Microsoft Bookings discovery.
/// </summary>
public class MicrosoftBookingsFunctions
{
    private readonly MicrosoftBookingsClient _client;
    private readonly ILogger<MicrosoftBookingsFunctions> _logger;

    public MicrosoftBookingsFunctions(ILogger<MicrosoftBookingsFunctions> logger, MicrosoftBookingsClient client)
    {
        this._logger = logger;
        this._client = client;
    }

    [Function("MicrosoftBookingsListBusinesses")]
    public Task<HttpResponseData> ListBusinessesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "microsoftbookings/businesses")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "MicrosoftBookingsListBusinesses",
            operation: () => this._client.ListBookingsBusinessUserAsAdminAsync(cancellationToken),
            cancellationToken);
    }
}