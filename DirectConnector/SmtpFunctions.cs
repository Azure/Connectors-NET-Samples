//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Smtp;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating SMTP operations using the generated
/// <see cref="SmtpClient"/> from the DirectClient SDK.
/// </summary>
public class SmtpFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<SmtpFunctions> _logger;
    private readonly SmtpClient _smtpClient;

    public SmtpFunctions(
        ILogger<SmtpFunctions> logger,
        SmtpClient smtpClient)
    {
        this._logger = logger;
        this._smtpClient = smtpClient;
    }

    /// <summary>
    /// Sends an email using the generated <see cref="SmtpClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request containing email details (from, to, subject, body).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("SmtpSendEmail")]
    public async Task<HttpResponseData> SmtpSendEmailAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "smtp/email")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("SmtpSendEmail: Using generated SmtpClient from SDK.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var input = JsonSerializer.Deserialize<SmtpSendEmailRequest>(body, SmtpFunctions.JsonOptions);

            if (input == null || string.IsNullOrWhiteSpace(input.To) || string.IsNullOrWhiteSpace(input.From))
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest
                    .WriteAsJsonAsync(new { error = "'from' and 'to' are required." })
                    .ConfigureAwait(continueOnCapturedContext: false);
                return badRequest;
            }

            var email = new Email
            {
                From = input.From,
                To = input.To,
                Subject = input.Subject ?? "No Subject",
                Body = input.Body ?? string.Empty
            };

            await this._smtpClient
                .SendEmailAsync(input: email, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    message = "Email sent via generated SmtpClient from SDK.",
                    from = input.From,
                    to = input.To,
                    subject = input.Subject,
                    timestamp = DateTime.UtcNow
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "SMTP connector error: '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.Status,
                    details = ex.ResponseBody
                }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (JsonException ex)
        {
            this._logger.LogWarning(ex, "Invalid JSON in request body.");

            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Request body must contain valid JSON." })
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error in SmtpSendEmail.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    private record SmtpSendEmailRequest(string? From, string? To, string? Subject, string? Body);
}
