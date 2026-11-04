//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Reflection;
using System.Text.Json;
using Azure.Connectors.Sdk.Azurequeues;
using Azure.Core.Pipeline;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using Moq.Protected;

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

    private static AzureQueuesClient CreateTransportClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> onRequest)
    {
        var (credential, options) = TestHelpers.CreateMockedClientSetup(
            () => throw new InvalidOperationException("Unexpected fallback transport request."));
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage request, CancellationToken token) => onRequest(request, token));
        options.Transport = new HttpClientTransport(new HttpClient(handler.Object));

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

    [TestMethod]
    public async Task AzureQueuesGetMessagesAsync_WithMalformedSuccessResponse_ReturnsInternalServerError()
    {
        using var client = AzureQueuesFunctionsTests.CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{"),
        });
        var functions = new AzureQueuesFunctions(TestHelpers.CreateNullLogger<AzureQueuesFunctions>(), client);
        var request = TestHelpers.CreateRequest(
            method: "POST",
            url: "https://localhost/api/azurequeues/messages?storageAccount=devstorageaccount&queueName=sdk-validation");

        var response = await functions
            .AzureQueuesGetMessagesAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.InternalServerError, actual: response.StatusCode);
        using var document = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsFalse(document.RootElement.GetProperty("success").GetBoolean());
        Assert.IsFalse(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("error").GetString()));
    }

    [TestMethod]
    [Owner("daviburg")]
    [DataRow("AzureQueuesPutQueueAsync", "AzureQueuesPutQueue", "put", "azurequeues/queues")]
    [DataRow("AzureQueuesPutMessageAsync", "AzureQueuesPutMessage", "post", "azurequeues/messages/send")]
    [DataRow("AzureQueuesDeleteMessageAsync", "AzureQueuesDeleteMessage", "delete", "azurequeues/messages")]
    public void AzureQueuesMutation_Trigger_UsesExpectedFunctionRouteAndMethod(
        string methodName,
        string functionName,
        string httpMethod,
        string route)
    {
        var method = typeof(AzureQueuesFunctions).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method);
        var function = method.GetCustomAttribute<FunctionAttribute>();
        Assert.IsNotNull(function);
        Assert.AreEqual(expected: functionName, actual: function.Name);
        var trigger = method.GetParameters()
            .Single(parameter => parameter.ParameterType == typeof(HttpRequestData))
            .GetCustomAttribute<HttpTriggerAttribute>();
        Assert.IsNotNull(trigger);
        Assert.AreEqual(expected: route, actual: trigger.Route);
        CollectionAssert.AreEqual(expected: new[] { httpMethod }, actual: trigger.Methods!.ToArray());
    }

    [TestMethod]
    public async Task AzureQueuesPutQueueAsync_WithValidQuery_DispatchesGeneratedPutAndReturnsValue()
    {
        var calls = 0;
        using var client = AzureQueuesFunctionsTests.CreateTransportClient((outbound, token) =>
        {
            calls++;
            Assert.AreEqual(expected: HttpMethod.Put, actual: outbound.Method);
            Assert.AreEqual(
                expected: "/connection/v2/storageAccounts/account-demo/queues/putQueue?queueName=temporary-queue",
                actual: outbound.RequestUri!.PathAndQuery);
            Assert.IsNull(outbound.Content);
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("\"created\""),
            });
        });
        var functions = new AzureQueuesFunctions(TestHelpers.CreateNullLogger<AzureQueuesFunctions>(), client);
        var request = TestHelpers.CreateRequest(
            method: "PUT",
            url: "https://localhost/api/azurequeues/queues?storageAccount=account-demo&queueName=temporary-queue");

        var response = await functions
            .AzureQueuesPutQueueAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: 1, actual: calls);
        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        using var document = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsTrue(document.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual(expected: "created", actual: document.RootElement.GetProperty("value").GetString());
    }

    [TestMethod]
    [Owner("daviburg")]
    public async Task AzureQueuesPutMessageAsync_WithPlainTextBody_SendsJsonEncodedString()
    {
        const string message = "Order \"demo\" \u0026 notification";
        var calls = 0;
        using var client = AzureQueuesFunctionsTests.CreateTransportClient(async (outbound, token) =>
        {
            calls++;
            Assert.AreEqual(expected: HttpMethod.Post, actual: outbound.Method);
            Assert.AreEqual(
                expected: "/connection/v2/storageAccounts/account-demo/queues/temporary-queue/messages",
                actual: outbound.RequestUri!.PathAndQuery);
            Assert.AreEqual(expected: "application/json", actual: outbound.Content!.Headers.ContentType!.MediaType);
            var actualBody = await outbound.Content
                .ReadAsStringAsync(token)
                .ConfigureAwait(continueOnCapturedContext: false);
            Assert.AreEqual(expected: JsonSerializer.Serialize(message), actual: actualBody);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var functions = new AzureQueuesFunctions(TestHelpers.CreateNullLogger<AzureQueuesFunctions>(), client);
        var request = TestHelpers.CreateRequest(
            body: message,
            method: "POST",
            url: "https://localhost/api/azurequeues/messages/send?storageAccount=account-demo&queueName=temporary-queue");

        var response = await functions
            .AzureQueuesPutMessageAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: 1, actual: calls);
        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        using var document = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsTrue(document.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual(expected: "temporary-queue", actual: document.RootElement.GetProperty("value").GetProperty("queueName").GetString());
    }

    [TestMethod]
    public async Task AzureQueuesDeleteMessageAsync_WithPopReceipt_EncodesReceiptAndReturnsIdentity()
    {
        var calls = 0;
        using var client = AzureQueuesFunctionsTests.CreateTransportClient((outbound, token) =>
        {
            calls++;
            Assert.AreEqual(expected: HttpMethod.Delete, actual: outbound.Method);
            Assert.AreEqual(
                expected: "/connection/v2/storageAccounts/account-demo/queues/temporary-queue/messages/message-1?popreceipt=receipt%2B1%2F2%3D",
                actual: outbound.RequestUri!.PathAndQuery);
            Assert.IsNull(outbound.Content);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var functions = new AzureQueuesFunctions(TestHelpers.CreateNullLogger<AzureQueuesFunctions>(), client);
        var request = TestHelpers.CreateRequest(
            method: "DELETE",
            url: "https://localhost/api/azurequeues/messages?storageAccount=account-demo&queueName=temporary-queue&messageId=message-1&popReceipt=receipt%2B1%2F2%3D");

        var response = await functions
            .AzureQueuesDeleteMessageAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: 1, actual: calls);
        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        using var document = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsTrue(document.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual(expected: "message-1", actual: document.RootElement.GetProperty("value").GetProperty("messageId").GetString());
    }

    [TestMethod]
    public async Task AzureQueuesGetMessagesAsync_WithNumericOptions_UsesGeneratedQueryAndRetainsResponseShape()
    {
        var calls = 0;
        const string payload = """
            {"QueueMessagesList":{"QueueMessage":[{"MessageId":"message-1","PopReceipt":"receipt-1","MessageText":"hello"}]}}
            """;
        using var client = AzureQueuesFunctionsTests.CreateTransportClient((outbound, token) =>
        {
            calls++;
            Assert.AreEqual(expected: HttpMethod.Get, actual: outbound.Method);
            Assert.AreEqual(
                expected: "/connection/v2/storageAccounts/account-demo/queues/temporary-queue/messages?numofmessages=2&visibilitytimeout=30",
                actual: outbound.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(payload),
            });
        });
        var functions = new AzureQueuesFunctions(TestHelpers.CreateNullLogger<AzureQueuesFunctions>(), client);
        var request = TestHelpers.CreateRequest(
            method: "POST",
            url: "https://localhost/api/azurequeues/messages?storageAccount=account-demo&queueName=temporary-queue&numberOfMessages=2&visibilityTimeout=30");

        var response = await functions
            .AzureQueuesGetMessagesAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: 1, actual: calls);
        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        using var document = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsTrue(document.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual(expected: "hello", actual: document.RootElement.GetProperty("messages")[0].GetProperty("MessageText").GetString());
        Assert.AreEqual(expected: "receipt-1", actual: document.RootElement.GetProperty("messages")[0].GetProperty("popReceipt").GetString());
    }

    [TestMethod]
    [DataRow("https://localhost/api/azurequeues/queues?storageAccount=account-demo&queueName=%20", "queue")]
    [DataRow("https://localhost/api/azurequeues/queues?storageAccount=account-demo", "queueName")]
    public async Task AzureQueuesPutQueueAsync_WithInvalidQuery_Returns400WithoutDispatch(string url, string expectedParameter)
    {
        var calls = 0;
        using var client = AzureQueuesFunctionsTests.CreateTransportClient((outbound, token) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var functions = new AzureQueuesFunctions(TestHelpers.CreateNullLogger<AzureQueuesFunctions>(), client);

        var response = await functions
            .AzureQueuesPutQueueAsync(TestHelpers.CreateRequest(method: "PUT", url: url), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: 0, actual: calls);
        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
        StringAssert.Contains(((MockHttpResponseData)response).GetBodyAsString(), expectedParameter);
    }

    [TestMethod]
    [Owner("daviburg")]
    public async Task AzureQueuesPutMessageAsync_WithEmptyBody_Returns400WithoutDispatch()
    {
        var calls = 0;
        using var client = AzureQueuesFunctionsTests.CreateTransportClient((outbound, token) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var functions = new AzureQueuesFunctions(TestHelpers.CreateNullLogger<AzureQueuesFunctions>(), client);
        var request = TestHelpers.CreateRequest(
            body: "  ", method: "POST",
            url: "https://localhost/api/azurequeues/messages/send?storageAccount=account-demo&queueName=temporary-queue");

        var response = await functions
            .AzureQueuesPutMessageAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: 0, actual: calls);
        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
        StringAssert.Contains(((MockHttpResponseData)response).GetBodyAsString(), "body");
    }

    [TestMethod]
    public async Task AzureQueuesDeleteMessageAsync_WithMissingReceipt_Returns400WithoutDispatch()
    {
        var calls = 0;
        using var client = AzureQueuesFunctionsTests.CreateTransportClient((outbound, token) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var functions = new AzureQueuesFunctions(TestHelpers.CreateNullLogger<AzureQueuesFunctions>(), client);
        var request = TestHelpers.CreateRequest(
            method: "DELETE",
            url: "https://localhost/api/azurequeues/messages?storageAccount=account-demo&queueName=temporary-queue&messageId=message-1");

        var response = await functions
            .AzureQueuesDeleteMessageAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: 0, actual: calls);
        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
        StringAssert.Contains(((MockHttpResponseData)response).GetBodyAsString(), "popReceipt");
    }

    [TestMethod]
    [DataRow("numberOfMessages=0")]
    [DataRow("numberOfMessages=33")]
    [DataRow("numberOfMessages=1.5")]
    [DataRow("numberOfMessages=")]
    [DataRow("visibilityTimeout=-1")]
    [DataRow("visibilityTimeout=604801")]
    [DataRow("visibilityTimeout=abc")]
    public async Task AzureQueuesGetMessagesAsync_WithInvalidNumericOption_Returns400WithoutDispatch(string option)
    {
        var calls = 0;
        using var client = AzureQueuesFunctionsTests.CreateTransportClient((outbound, token) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var functions = new AzureQueuesFunctions(TestHelpers.CreateNullLogger<AzureQueuesFunctions>(), client);
        var request = TestHelpers.CreateRequest(
            method: "POST",
            url: $"https://localhost/api/azurequeues/messages?storageAccount=account-demo&queueName=temporary-queue&{option}");

        var response = await functions
            .AzureQueuesGetMessagesAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: 0, actual: calls);
        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
    }

    [TestMethod]
    [Owner("daviburg")]
    [DataRow("putQueue")]
    [DataRow("putMessage")]
    [DataRow("deleteMessage")]
    public async Task AzureQueuesMutation_WithConnectorFailure_Returns502WithStatusAndDetails(string operation)
    {
        using var client = AzureQueuesFunctionsTests.CreateTransportClient((outbound, token) =>
            Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"error\":\"synthetic failure\"}"),
            }));
        var functions = new AzureQueuesFunctions(TestHelpers.CreateNullLogger<AzureQueuesFunctions>(), client);
        var request = operation switch
        {
            "putQueue" => TestHelpers.CreateRequest(method: "PUT", url: "https://localhost/api/azurequeues/queues?storageAccount=account-demo&queueName=temporary-queue"),
            "putMessage" => TestHelpers.CreateRequest(body: "hello", method: "POST", url: "https://localhost/api/azurequeues/messages/send?storageAccount=account-demo&queueName=temporary-queue"),
            _ => TestHelpers.CreateRequest(method: "DELETE", url: "https://localhost/api/azurequeues/messages?storageAccount=account-demo&queueName=temporary-queue&messageId=message-1&popReceipt=receipt-1"),
        };

        var response = operation switch
        {
            "putQueue" => await functions.AzureQueuesPutQueueAsync(request, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false),
            "putMessage" => await functions.AzureQueuesPutMessageAsync(request, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false),
            _ => await functions.AzureQueuesDeleteMessageAsync(request, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false),
        };

        Assert.AreEqual(expected: HttpStatusCode.BadGateway, actual: response.StatusCode);
        using var document = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsFalse(document.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual(expected: 400, actual: document.RootElement.GetProperty("statusCode").GetInt32());
        StringAssert.Contains(document.RootElement.GetProperty("details").GetString()!, "synthetic failure");
    }
}
