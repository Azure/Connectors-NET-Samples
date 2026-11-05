//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk.Azuretables;
using Azure.Core.Pipeline;
using Microsoft.Azure.Functions.Worker.Http;

namespace DirectConnector.Tests;

[TestClass]
public class AzureTablesFunctionsTests
{
    [TestMethod]
    [Owner("daviburg")]
    [DataRow("GetStorageAccounts", "GET", "/v2/GetStorageAccounts")]
    [DataRow("ListTables", "GET", "/v2/storageAccounts/account/tables")]
    [DataRow("CreateTable", "POST", "/v2/storageAccounts/account/tables")]
    [DataRow("GetTable", "GET", "/v2/storageAccounts/account/tables/SdkTable")]
    [DataRow("DeleteTable", "DELETE", "/v2/storageAccounts/account/tables/SdkTable")]
    [DataRow("GetEntities", "GET", "/v2/storageAccounts/account/tables/SdkTable/entities")]
    [DataRow("GetEntity", "GET", "/v2/storageAccounts/account/tables/SdkTable/entities(PartitionKey='partition',RowKey='row')")]
    [DataRow("CreateEntity", "POST", "/v2/storageAccounts/account/tables/SdkTable/entities")]
    [DataRow("DeleteEntity", "DELETE", "/v2/storageAccounts/account/tables/SdkTable/entities(PartitionKey='partition',RowKey='row')")]
    [DataRow("MergeEntity", "PATCH", "/v2/storageAccounts/account/tables/SdkTable/entities(PartitionKey='partition',RowKey='row')")]
    [DataRow("ReplaceEntity", "PUT", "/v2/storageAccounts/account/tables/SdkTable/entities(PartitionKey='partition',RowKey='row')")]
    [DataRow("InsertMergeEntity", "PATCH", "/v2/storageAccounts/account/tables/SdkTable/entities(PartitionKey='partition',RowKey='row')")]
    [DataRow("InsertReplaceEntity", "PUT", "/v2/storageAccounts/account/tables/SdkTable/entities(PartitionKey='partition',RowKey='row')")]
    public async Task Operations_WithValidInput_UseGeneratedClient(string operation, string method, string path)
    {
        using var handler = new RecordingHandler();
        using var client = AzureTablesFunctionsTests.CreateClient(handler);
        var functions = new AzureTablesFunctions(TestHelpers.CreateNullLogger<AzureTablesFunctions>(), client);
        var query = operation switch
        {
            "GetStorageAccounts" => string.Empty,
            "ListTables" => "?storageAccount=account",
            "GetTable" or "DeleteTable" or "GetEntities" or "CreateEntity" => "?storageAccount=account&table=SdkTable",
            _ => "?storageAccount=account&table=SdkTable&partitionKey=partition&rowKey=row",
        };
        var request = TestHelpers.CreateRequest(
            body: string.Equals(operation, "CreateEntity", StringComparison.Ordinal)
                ? "{\"PartitionKey\":\"partition\",\"RowKey\":\"row\",\"Message\":\"synthetic\"}"
                : "{\"Message\":\"synthetic\"}",
            method: method,
            url: "https://localhost/api/azuretables" + query);

        var response = await AzureTablesFunctionsTests
            .InvokeAsync(functions: functions, operation: operation, request: request)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        Assert.AreEqual(expected: method, actual: handler.Method);
        Assert.AreEqual(expected: "/connection" + path, actual: Uri.UnescapeDataString(handler.Path!));
        Assert.AreEqual(expected: 1, actual: handler.RequestCount);
        using var result = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsTrue(result.RootElement.GetProperty("success").GetBoolean());
        if (string.Equals(operation, "CreateTable", StringComparison.Ordinal))
        {
            Assert.AreEqual(expected: "\"SdkTable\"", actual: handler.Body);
        }
        else if (operation is "CreateEntity" or "MergeEntity" or "ReplaceEntity" or "InsertMergeEntity" or "InsertReplaceEntity")
        {
            using var body = JsonDocument.Parse(handler.Body!);
            Assert.AreEqual(expected: "synthetic", actual: body.RootElement.GetProperty("Message").GetString());
            if (string.Equals(operation, "CreateEntity", StringComparison.Ordinal))
            {
                Assert.AreEqual(expected: "partition", actual: body.RootElement.GetProperty("PartitionKey").GetString());
                Assert.AreEqual(expected: "row", actual: body.RootElement.GetProperty("RowKey").GetString());
                Assert.AreEqual(expected: "partition", actual: result.RootElement.GetProperty("value").GetProperty("PartitionKey").GetString());
            }
        }
    }

    [TestMethod]
    [DataRow("CreateTable")]
    [DataRow("GetTable")]
    [DataRow("DeleteTable")]
    [DataRow("GetEntities")]
    [DataRow("GetEntity")]
    [DataRow("CreateEntity")]
    [DataRow("DeleteEntity")]
    [DataRow("MergeEntity")]
    [DataRow("ReplaceEntity")]
    [DataRow("InsertMergeEntity")]
    [DataRow("InsertReplaceEntity")]
    public async Task Operations_WithMissingSelectors_Return400WithoutInvocation(string operation)
    {
        using var handler = new RecordingHandler();
        using var client = AzureTablesFunctionsTests.CreateClient(handler);
        var functions = new AzureTablesFunctions(TestHelpers.CreateNullLogger<AzureTablesFunctions>(), client);
        var response = await AzureTablesFunctionsTests
            .InvokeAsync(functions, operation, TestHelpers.CreateRequest())
            .ConfigureAwait(continueOnCapturedContext: false);
        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
        Assert.AreEqual(expected: 0, actual: handler.RequestCount);
    }

    [TestMethod]
    [DataRow("CreateEntity", "{")]
    [DataRow("CreateEntity", "[]")]
    [DataRow("CreateEntity", "{\"Message\":\"synthetic\"}")]
    [DataRow("MergeEntity", "null")]
    [DataRow("InsertReplaceEntity", "42")]
    public async Task EntityWrites_WithInvalidBody_Return400WithoutInvocation(string operation, string body)
    {
        using var handler = new RecordingHandler();
        using var client = AzureTablesFunctionsTests.CreateClient(handler);
        var functions = new AzureTablesFunctions(TestHelpers.CreateNullLogger<AzureTablesFunctions>(), client);
        var response = await AzureTablesFunctionsTests
            .InvokeAsync(functions, operation, TestHelpers.CreateRequest(
                body: body,
                url: "https://localhost/api/test?storageAccount=account&table=SdkTable&partitionKey=partition&rowKey=row"))
            .ConfigureAwait(continueOnCapturedContext: false);
        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
        Assert.AreEqual(expected: 0, actual: handler.RequestCount);
    }

    [TestMethod]
    public async Task GetEntityAsync_WithPermissionFailure_ReturnsStructured502()
    {
        using var handler = new RecordingHandler { Status = HttpStatusCode.Forbidden };
        using var client = AzureTablesFunctionsTests.CreateClient(handler);
        var functions = new AzureTablesFunctions(TestHelpers.CreateNullLogger<AzureTablesFunctions>(), client);
        var response = await functions
            .GetEntityAsync(TestHelpers.CreateRequest(
                url: "https://localhost/api/test?storageAccount=account&table=SdkTable&partitionKey=partition&rowKey=row"), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
        Assert.AreEqual(expected: HttpStatusCode.BadGateway, actual: response.StatusCode);
        using var result = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.AreEqual(expected: 403, actual: result.RootElement.GetProperty("statusCode").GetInt32());
        Assert.IsFalse(result.RootElement.GetProperty("success").GetBoolean());
    }

    [TestMethod]
    public async Task GetEntitiesAsync_WithFilterAndSelect_ForwardsBothQueries()
    {
        using var handler = new RecordingHandler();
        using var client = AzureTablesFunctionsTests.CreateClient(handler);
        var functions = new AzureTablesFunctions(TestHelpers.CreateNullLogger<AzureTablesFunctions>(), client);
        await functions
            .GetEntitiesAsync(TestHelpers.CreateRequest(
                url: "https://localhost/api/test?storageAccount=account&table=SdkTable&$filter=RowKey%20eq%20%27row%27&$select=RowKey"), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
        StringAssert.Contains(value: Uri.UnescapeDataString(handler.Query!), substring: "$filter=RowKey eq 'row'");
        StringAssert.Contains(value: Uri.UnescapeDataString(handler.Query!), substring: "$select=RowKey");
    }

    private static AzureTablesClient CreateClient(RecordingHandler handler)
    {
        var setup = TestHelpers.CreateMockedClientSetup(responseFactory: () => new HttpResponseMessage(statusCode: HttpStatusCode.OK));
        setup.Options.Transport = new HttpClientTransport(new HttpClient(handler));
        return new AzureTablesClient(connectionRuntimeUrl: new Uri(uriString: "https://test.azure.com/connection"), credential: setup.Credential, options: setup.Options);
    }

    private static Task<HttpResponseData> InvokeAsync(AzureTablesFunctions functions, string operation, HttpRequestData request)
    {
        return operation switch
        {
            "GetStorageAccounts" => functions.AzureTablesListStorageAccountsAsync(request, CancellationToken.None),
            "ListTables" => functions.AzureTablesListTablesAsync(request, CancellationToken.None),
            "CreateTable" => functions.CreateTableAsync(request, CancellationToken.None),
            "GetTable" => functions.GetTableAsync(request, CancellationToken.None),
            "DeleteTable" => functions.DeleteTableAsync(request, CancellationToken.None),
            "GetEntities" => functions.GetEntitiesAsync(request, CancellationToken.None),
            "GetEntity" => functions.GetEntityAsync(request, CancellationToken.None),
            "CreateEntity" => functions.CreateEntityAsync(request, CancellationToken.None),
            "DeleteEntity" => functions.DeleteEntityAsync(request, CancellationToken.None),
            "MergeEntity" => functions.MergeEntityAsync(request, CancellationToken.None),
            "ReplaceEntity" => functions.ReplaceEntityAsync(request, CancellationToken.None),
            "InsertMergeEntity" => functions.InsertMergeEntityAsync(request, CancellationToken.None),
            "InsertReplaceEntity" => functions.InsertReplaceEntityAsync(request, CancellationToken.None),
            _ => throw new ArgumentException(message: "Unknown test operation.", paramName: nameof(operation)),
        };
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public string? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Query { get; private set; }
        public string? Body { get; private set; }
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.RequestCount++;
            this.Method = request.Method.Method;
            this.Path = request.RequestUri!.AbsolutePath;
            this.Query = request.RequestUri.Query;
            this.Body = request.Content is null
                ? null
                : await request.Content
                    .ReadAsStringAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
            return new HttpResponseMessage(statusCode: this.Status)
            {
                Content = new StringContent(content: "{\"TableName\":\"SdkTable\",\"PartitionKey\":\"partition\",\"RowKey\":\"row\",\"value\":[]}"),
            };
        }
    }
}
