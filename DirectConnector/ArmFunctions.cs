//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk.Arm;
using Azure.Connectors.Sdk.Arm.Models;
using Azure.Connectors.Sdk;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Azure Resource Manager operations using the generated
/// <see cref="ArmClient"/> from the DirectClient SDK.
/// </summary>
/// <remarks>
/// ARM connector uses OAuth (user-delegated Azure AD token).
/// The connection requires OAuth consent via the consent link flow.
/// </remarks>
public class ArmFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<ArmFunctions> _logger;
    private readonly ArmClient _armClient;

    public ArmFunctions(
        ILogger<ArmFunctions> logger,
        ArmClient armClient)
    {
        this._logger = logger;
        this._armClient = armClient;
    }

    /// <summary>
    /// Lists all subscriptions accessible to the authenticated user.
    /// </summary>
    [Function("ListSubscriptions")]
    public async Task<HttpResponseData> ListSubscriptionsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "arm/subscriptions")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListSubscriptions: Using generated ArmClient.");

        try
        {
            var subscriptions = new List<Subscription>();
            await foreach (var subscription in this._armClient
                .SubscriptionsListAsync()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false))
            {
                subscriptions.Add(subscription);
            }

            this._logger.LogInformation("Found '{Count}' subscriptions.", subscriptions.Count);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(subscriptions)
                .ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ARM connector error: '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ListSubscriptions.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
    }

    /// <summary>
    /// Lists resource groups in a subscription.
    /// </summary>
    [Function("ListResourceGroups")]
    public async Task<HttpResponseData> ListResourceGroupsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "arm/subscriptions/{subscriptionId}/resourcegroups")] HttpRequestData request,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListResourceGroups: subscription='{SubscriptionId}'.", subscriptionId);

        try
        {
            var resourceGroups = new List<ResourceGroup>();
            await foreach (var resourceGroup in this._armClient
                .ResourceGroupsListAsync(subscription: subscriptionId)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false))
            {
                resourceGroups.Add(resourceGroup);
            }

            this._logger.LogInformation("Found '{Count}' resource groups.", resourceGroups.Count);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(resourceGroups)
                .ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ARM connector error: '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ListResourceGroups.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
    }

    /// <summary>
    /// Reads a specific resource by its resource group, provider, and short resource ID.
    /// </summary>
    [Function("ReadResource")]
    public async Task<HttpResponseData> ReadResourceAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "arm/resource")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var subscriptionId = request.Query["subscriptionId"];
        var resourceGroup = request.Query["resourceGroup"];
        var provider = request.Query["provider"];
        var shortResourceId = request.Query["shortResourceId"];
        var apiVersion = request.Query["apiVersion"];

        if (string.IsNullOrWhiteSpace(subscriptionId) ||
            string.IsNullOrWhiteSpace(resourceGroup) ||
            string.IsNullOrWhiteSpace(provider) ||
            string.IsNullOrWhiteSpace(shortResourceId) ||
            string.IsNullOrWhiteSpace(apiVersion))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameters 'subscriptionId', 'resourceGroup', 'provider', 'shortResourceId', and 'apiVersion' are required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        this._logger.LogInformation("ReadResource: '{Provider}/{ShortResourceId}' in '{ResourceGroup}'.", provider, shortResourceId, resourceGroup);

        try
        {
            var resource = await this._armClient
                .ResourcesGetByIdAsync(
                    subscription: subscriptionId,
                    resourceGroup: resourceGroup,
                    resourceProvider: provider,
                    shortResourceId: shortResourceId,
                    clientApiVersion: apiVersion,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            this._logger.LogInformation("Read resource '{Name}' of type '{Type}'.", resource.Name, resource.Type);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(resource)
                .ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ARM connector error: '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ReadResource.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
    }

    /// <summary>
    /// Lists resources in a resource group.
    /// </summary>
    [Function("ListResourcesByResourceGroup")]
    public async Task<HttpResponseData> ListResourcesByResourceGroupAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "arm/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/resources")] HttpRequestData request,
        string subscriptionId,
        string resourceGroupName,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListResourcesByResourceGroup: subscription='{SubscriptionId}', resourceGroup='{ResourceGroup}'.", subscriptionId, resourceGroupName);

        try
        {
            var resources = new List<GenericResource>();
            await foreach (var resource in this._armClient
                .ResourceGroupsListResourcesAsync(subscription: subscriptionId, resourceGroup: resourceGroupName)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false))
            {
                resources.Add(resource);
            }

            this._logger.LogInformation("Found '{Count}' resources.", resources.Count);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(resources)
                .ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ARM connector error: '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ListResourcesByResourceGroup.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
    }
}
