//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk.Arm;

namespace DirectConnector.Tests;

[TestClass]
public class ArmFunctionsTests
{
    private static ArmClient CreateMockedClient(Func<HttpResponseMessage> responseFactory)
    {
        var (credential, options) = TestHelpers.CreateMockedClientSetup(responseFactory);
        return new ArmClient(
            connectionRuntimeUrl: new Uri("https://test.azure.com/connection"),
            credential: credential,
            options: options);
    }

    [TestMethod]
    public async Task ListSubscriptionsAsync_WithValidResponse_ReturnsSubscriptions()
    {
        // Arrange — sanitized payload based on real ARM subscriptions response
        var subscriptionsResponse = new
        {
            value = new[]
            {
                new
                {
                    subscriptionId = "00000000-0000-0000-0000-000000000001",
                    displayName = "Development Subscription",
                    state = "Enabled",
                },
                new
                {
                    subscriptionId = "00000000-0000-0000-0000-000000000002",
                    displayName = "Production Subscription",
                    state = "Enabled",
                },
            },
        };

        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(subscriptionsResponse)),
        });

        var functions = new ArmFunctions(
            TestHelpers.CreateNullLogger<ArmFunctions>(),
            client);

        var request = TestHelpers.CreateRequest();

        // Act
        var response = await functions.ListSubscriptionsAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("Development Subscription", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("Production Subscription", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ListSubscriptionsAsync_WithConnectorError_Returns502()
    {
        // Arrange
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Unauthorized,
            Content = new StringContent("{\"error\":{\"code\":\"AuthorizationFailed\",\"message\":\"The client does not have authorization\"}}"),
        });

        var functions = new ArmFunctions(
            TestHelpers.CreateNullLogger<ArmFunctions>(),
            client);

        var request = TestHelpers.CreateRequest();

        // Act
        var response = await functions.ListSubscriptionsAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("\"success\":false", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ListResourceGroupsAsync_WithSubscriptionId_ReturnsResourceGroups()
    {
        // Arrange — sanitized payload based on real ARM resource groups response
        var resourceGroupsResponse = new
        {
            value = new[]
            {
                new { name = "rg-dev", location = "eastus", id = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-dev" },
                new { name = "rg-prod", location = "westus2", id = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-prod" },
            },
        };

        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(resourceGroupsResponse)),
        });

        var functions = new ArmFunctions(
            TestHelpers.CreateNullLogger<ArmFunctions>(),
            client);

        var request = TestHelpers.CreateRequest(
            url: "https://localhost/api/arm/subscriptions/00000000-0000-0000-0000-000000000001/resourcegroups");

        // Act — subscriptionId is a route parameter
        var response = await functions.ListResourceGroupsAsync(request, "00000000-0000-0000-0000-000000000001", CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("rg-dev", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("rg-prod", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ReadResourceGroupAsync_WithValidResponse_ReturnsResourceGroup()
    {
        var resourceGroupResponse = new
        {
            id = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-test",
            name = "rg-test",
            location = "eastus",
        };
        using var client = ArmFunctionsTests.CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(resourceGroupResponse)),
        });
        var functions = new ArmFunctions(
            TestHelpers.CreateNullLogger<ArmFunctions>(),
            client);
        var request = TestHelpers.CreateRequest(
            url: "https://localhost/api/arm/subscriptions/00000000-0000-0000-0000-000000000001/resourcegroups/rg-test");

        var response = await functions.ReadResourceGroupAsync(
            request,
            "00000000-0000-0000-0000-000000000001",
            "rg-test",
            CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("rg-test", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CreateOrUpdateResourceGroupAsync_WithValidBody_ReturnsCreated()
    {
        var resourceGroupResponse = new
        {
            id = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-test",
            name = "rg-test",
            location = "eastus",
        };
        using var client = ArmFunctionsTests.CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Created,
            Content = new StringContent(JsonSerializer.Serialize(resourceGroupResponse)),
        });
        var functions = new ArmFunctions(
            TestHelpers.CreateNullLogger<ArmFunctions>(),
            client);
        var request = TestHelpers.CreateRequest(
            body: "{\"location\":\"eastus\"}",
            method: "PUT",
            url: "https://localhost/api/arm/subscriptions/00000000-0000-0000-0000-000000000001/resourcegroups/rg-test");

        var response = await functions.CreateOrUpdateResourceGroupAsync(
            request,
            "00000000-0000-0000-0000-000000000001",
            "rg-test",
            CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.Created, actual: response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("rg-test", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CreateOrUpdateResourceGroupAsync_WithInvalidJson_Returns400()
    {
        using var client = ArmFunctionsTests.CreateMockedClient(() => new HttpResponseMessage(HttpStatusCode.OK));
        var functions = new ArmFunctions(
            TestHelpers.CreateNullLogger<ArmFunctions>(),
            client);
        var request = TestHelpers.CreateRequest(body: "{ invalid", method: "PUT");

        var response = await functions.CreateOrUpdateResourceGroupAsync(
            request,
            "00000000-0000-0000-0000-000000000001",
            "rg-test",
            CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
    }

    [TestMethod]
    public async Task DeleteResourceGroupAsync_WithSuccessfulResponse_ReturnsNoContent()
    {
        using var client = ArmFunctionsTests.CreateMockedClient(() => new HttpResponseMessage(HttpStatusCode.NoContent));
        var functions = new ArmFunctions(
            TestHelpers.CreateNullLogger<ArmFunctions>(),
            client);
        var request = TestHelpers.CreateRequest(method: "DELETE");

        var response = await functions.DeleteResourceGroupAsync(
            request,
            "00000000-0000-0000-0000-000000000001",
            "rg-test",
            CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.NoContent, actual: response.StatusCode);
    }
}
