//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk.Azurequeues;

namespace DirectConnector.Tests;

[TestClass]
public class AzureQueuesFunctionsTests
{
    private static AzureQueuesClient CreateMockedClient(Func<HttpResponseMessage> responseFactory)
    {
        var (credential, options) = TestHelpers.CreateMockedClientSetup(responseFactory);
        return new AzureQueuesClient(
            connectionRuntimeUrl: new Uri("https://test.azure.com/connection"),
            credential: credential,
            options: options);
    }

    [TestMethod]
    public async Task AzureQueuesListStorageAccountsAsync_WithValidResponse_ReturnsAccounts()
    {
        // Arrange — sanitized payload based on real Azure Queues response
        var accountsResponse = new
        {
            value = new[]
            {
                new { Name = "devstorageaccount", DisplayName = "devstorageaccount" },
                new { Name = "prodstorageaccount", DisplayName = "prodstorageaccount" },
            },
        };

        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(accountsResponse)),
        });

        var functions = new AzureQueuesFunctions(
            TestHelpers.CreateNullLogger<AzureQueuesFunctions>(),
            client);

        var request = TestHelpers.CreateRequest();

        // Act
        var response = await functions.AzureQueuesListStorageAccountsAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("\"success\":true", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("devstorageaccount", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AzureQueuesListQueuesAsync_WithMissingStorageAccount_Returns400()
    {
        // Arrange — no storageAccount query parameter
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("[]"),
        });

        var functions = new AzureQueuesFunctions(
            TestHelpers.CreateNullLogger<AzureQueuesFunctions>(),
            client);

        var request = TestHelpers.CreateRequest(url: "https://localhost/api/azurequeues/queues");

        // Act
        var response = await functions.AzureQueuesListQueuesAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("storageAccount", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AzureQueuesListQueuesAsync_WithStorageAccount_ReturnsQueues()
    {
        // Arrange — sanitized payload based on real Azure Queues list response
        var queuesResponse = new[]
        {
            new { QueueName = "orders-queue" },
            new { QueueName = "notifications-queue" },
        };

        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(queuesResponse)),
        });

        var functions = new AzureQueuesFunctions(
            TestHelpers.CreateNullLogger<AzureQueuesFunctions>(),
            client);

        var request = TestHelpers.CreateRequest(
            url: "https://localhost/api/azurequeues/queues?storageAccount=devstorageaccount");

        // Act
        var response = await functions.AzureQueuesListQueuesAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("\"success\":true", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AzureQueuesListStorageAccountsAsync_WithConnectorError_Returns502()
    {
        // Arrange
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.ServiceUnavailable,
            Content = new StringContent("{\"error\":{\"code\":\"ServiceUnavailable\",\"message\":\"Connector temporarily unavailable\"}}"),
        });

        var functions = new AzureQueuesFunctions(
            TestHelpers.CreateNullLogger<AzureQueuesFunctions>(),
            client);

        var request = TestHelpers.CreateRequest();

        // Act
        var response = await functions.AzureQueuesListStorageAccountsAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("\"success\":false", StringComparison.Ordinal));
    }
}
