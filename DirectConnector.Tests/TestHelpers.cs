//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net.Http;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Core.Serialization;
using Azure.Connectors.Sdk;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace DirectConnector.Tests;

/// <summary>
/// Shared helpers for creating mocked connector clients and fake HTTP requests.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Creates a mocked <see cref="TokenCredential"/> and <see cref="ConnectorClientOptions"/>
    /// with a mocked HTTP transport that returns a new response from <paramref name="responseFactory"/>
    /// on every request. Mirrors the SDK test pattern from <c>ConnectorTestHelpers</c>.
    /// </summary>
    public static (TokenCredential Credential, ConnectorClientOptions Options) CreateMockedClientSetup(
        Func<HttpResponseMessage> responseFactory)
    {
        var mockCredential = new Mock<TokenCredential>();
        mockCredential
            .Setup(credential => credential.GetTokenAsync(
                It.IsAny<TokenRequestContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessToken("mock-token", new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() => Task.FromResult(responseFactory()));

        var options = new ConnectorClientOptions();
        options.Transport = new HttpClientTransport(new HttpClient(mockHandler.Object));
        options.Retry.MaxRetries = 0;

        return (mockCredential.Object, options);
    }

    /// <summary>
    /// Creates a mock <see cref="FunctionContext"/> with a configured JSON serializer,
    /// suitable for <see cref="MockHttpRequestData"/> and <c>WriteAsJsonAsync</c>.
    /// </summary>
    public static FunctionContext CreateMockFunctionContext()
    {
        var workerOptions = new WorkerOptions();
        workerOptions.Serializer = new JsonObjectSerializer();

        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(workerOptions));
        var serviceProvider = services.BuildServiceProvider();

        var mockContext = new Mock<FunctionContext>();
        mockContext.Setup(context => context.InstanceServices).Returns(serviceProvider);
        return mockContext.Object;
    }

    /// <summary>
    /// Creates a <see cref="MockHttpRequestData"/> with optional body, method, URL, and query parameters.
    /// </summary>
    public static MockHttpRequestData CreateRequest(
        string? body = null,
        string method = "GET",
        string url = "https://localhost/api/test")
    {
        return new MockHttpRequestData(
            CreateMockFunctionContext(),
            body: body,
            method: method,
            url: url);
    }

    /// <summary>
    /// Creates a <see cref="NullLogger{T}"/> for test constructors.
    /// </summary>
    public static ILogger<T> CreateNullLogger<T>()
    {
        return NullLogger<T>.Instance;
    }
}
