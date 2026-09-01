//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Connectors.Sdk.MicrosoftBookings;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Microsoft Bookings discovery-only operations.
/// </summary>
/// <remarks>
/// NOTE(daviburg): SDK 0.14 exposes booking appointment operations as Connector Namespace triggers,
/// so this sample lists the booking pages used as their required SMTP address input.
/// </remarks>
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
            operation: () => this._client
                .ListBookingsBusinessUserAsAdminAsync(cancellationToken),
            cancellationToken);
    }
}