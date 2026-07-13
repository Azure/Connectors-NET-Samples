//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk.AzureMonitorLogs;

namespace DirectConnector.Tests;

[TestClass]
public class AzureLogAnalyticsFunctionsTests
{
    private static AzureMonitorLogsClient CreateMockedClient(Func<HttpResponseMessage> responseFactory)
    {
        var (credential, options) = TestHelpers.CreateMockedClientSetup(responseFactory);
        return new AzureMonitorLogsClient(
            connectionRuntimeUrl: new Uri("https://test.azure.com/connection"),
            credential: credential,
            options: options);
    }

    [TestMethod]
    public async Task QueryDataAsync_WithValidResponse_ReturnsRows()
    {
        var queryResponse = new
        {
            value = new[]
            {
                new { TimeGenerated = "2026-07-10T00:00:00Z", RowCount = 1 },
            },
        };
        using var client = AzureLogAnalyticsFunctionsTests.CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(queryResponse)),
        });
        var functions = new AzureLogAnalyticsFunctions(
            TestHelpers.CreateNullLogger<AzureLogAnalyticsFunctions>(),
            client);
        var request = TestHelpers.CreateRequest(
            body: "{\"query\":\"traces | take 1\",\"timerangetype\":\"1\"}",
            method: "POST",
            url: AzureLogAnalyticsFunctionsTests.CreateResourceUri("query"));

        var response = await functions.QueryDataAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("TimeGenerated", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task QueryDataAsync_WithInvalidJsonBody_Returns400()
    {
        using var client = AzureLogAnalyticsFunctionsTests.CreateMockedClient(() => new HttpResponseMessage(HttpStatusCode.OK));
        var functions = new AzureLogAnalyticsFunctions(
            TestHelpers.CreateNullLogger<AzureLogAnalyticsFunctions>(),
            client);
        var request = TestHelpers.CreateRequest(
            body: "{ invalid",
            method: "POST",
            url: AzureLogAnalyticsFunctionsTests.CreateResourceUri("query"));

        var response = await functions.QueryDataAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
    }

    [TestMethod]
    public async Task QueryDataAsync_WithMissingResourceName_Returns400()
    {
        using var client = AzureLogAnalyticsFunctionsTests.CreateMockedClient(() => new HttpResponseMessage(HttpStatusCode.OK));
        var functions = new AzureLogAnalyticsFunctions(
            TestHelpers.CreateNullLogger<AzureLogAnalyticsFunctions>(),
            client);
        var request = TestHelpers.CreateRequest(
            body: "{\"query\":\"traces | take 1\"}",
            method: "POST",
            url: "https://localhost/api/loganalytics/query?subscription=sub-1&resourceGroup=rg-1&resourceType=Application%20Insights");

        var response = await functions.QueryDataAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
    }

    [TestMethod]
    public async Task QueryDataAsync_WithConnectorError_Returns502()
    {
        using var client = AzureLogAnalyticsFunctionsTests.CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = new StringContent("{\"error\":\"Query syntax error\"}"),
        });
        var functions = new AzureLogAnalyticsFunctions(
            TestHelpers.CreateNullLogger<AzureLogAnalyticsFunctions>(),
            client);
        var request = TestHelpers.CreateRequest(
            body: "{\"query\":\"invalid\"}",
            method: "POST",
            url: AzureLogAnalyticsFunctionsTests.CreateResourceUri("query"));

        var response = await functions.QueryDataAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.BadGateway, actual: response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("\"success\":false", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task QuerySchemaAsync_WithValidResponse_ReturnsSchema()
    {
        var schemaResponse = new
        {
            columns = new[]
            {
                new { name = "timestamp", type = "datetime" },
            },
        };
        using var client = AzureLogAnalyticsFunctionsTests.CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(schemaResponse)),
        });
        var functions = new AzureLogAnalyticsFunctions(
            TestHelpers.CreateNullLogger<AzureLogAnalyticsFunctions>(),
            client);
        var request = TestHelpers.CreateRequest(
            body: "traces | take 1",
            method: "POST",
            url: AzureLogAnalyticsFunctionsTests.CreateResourceUri("queryschema"));

        var response = await functions.QuerySchemaAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("columns", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task QuerySchemaAsync_WithEmptyQuery_Returns400()
    {
        using var client = AzureLogAnalyticsFunctionsTests.CreateMockedClient(() => new HttpResponseMessage(HttpStatusCode.OK));
        var functions = new AzureLogAnalyticsFunctions(
            TestHelpers.CreateNullLogger<AzureLogAnalyticsFunctions>(),
            client);
        var request = TestHelpers.CreateRequest(
            body: " ",
            method: "POST",
            url: AzureLogAnalyticsFunctionsTests.CreateResourceUri("queryschema"));

        var response = await functions.QuerySchemaAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
    }

    private static string CreateResourceUri(string operation)
    {
        return $"https://localhost/api/loganalytics/{operation}?subscription=sub-1&resourceGroup=rg-1&resourceType=Application%20Insights&resourceName=app-1";
    }
}
