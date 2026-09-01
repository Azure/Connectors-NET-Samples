//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Reflection;
using System.Text.Json;
using Azure.Connectors.Sdk.Azurequeues;
using Microsoft.Azure.Functions.Worker;

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

    [TestMethod]
    public async Task AzureQueuesGetMessagesAsync_WithVersionedResponse_ReturnsNestedMessages()
    {
        const string responseJson = """
            {
              "QueueMessagesList": {
                "QueueMessage": [{
                  "MessageId": "message-1",
                  "PopReceipt": "receipt-1",
                  "TimeNextVisible": "2026-08-19T03:00:00Z",
                  "DequeueCount": "1",
                  "MessageText": "SDK 0.14 validation"
                }]
              }
            }
            """;
        using var client = AzureQueuesFunctionsTests.CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseJson),
        });
        var functions = new AzureQueuesFunctions(TestHelpers.CreateNullLogger<AzureQueuesFunctions>(), client);
        var request = TestHelpers.CreateRequest(
            method: "POST",
            url: "https://localhost/api/azurequeues/messages?storageAccount=devstorageaccount&queueName=sdk-validation");

        var response = await functions
            .AzureQueuesGetMessagesAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("SDK 0.14 validation", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(body);
        var nextVisibleTime = document.RootElement
            .GetProperty("messages")[0]
            .GetProperty("nextVisibleTime")
            .GetDateTimeOffset();
        var popReceipt = document.RootElement
            .GetProperty("messages")[0]
            .GetProperty("popReceipt")
            .GetString();
        Assert.AreEqual(
            new DateTimeOffset(2026, 8, 19, 3, 0, 0, TimeSpan.Zero),
            nextVisibleTime);
        Assert.AreEqual(expected: "receipt-1", actual: popReceipt);
    }

    [TestMethod]
    public void AzureQueuesGetMessagesAsync_Trigger_AllowsOnlyPost()
    {
        var method = typeof(AzureQueuesFunctions).GetMethod(
            nameof(AzureQueuesFunctions.AzureQueuesGetMessagesAsync),
            BindingFlags.Instance | BindingFlags.Public);
        var trigger = method!
            .GetParameters()
            .Single(parameter => parameter.ParameterType == typeof(Microsoft.Azure.Functions.Worker.Http.HttpRequestData))
            .GetCustomAttribute<HttpTriggerAttribute>();

        Assert.IsNotNull(trigger);
        Assert.IsNotNull(trigger.Methods);
        CollectionAssert.AreEqual(expected: new[] { "post" }, actual: trigger.Methods.ToArray());
    }
}
