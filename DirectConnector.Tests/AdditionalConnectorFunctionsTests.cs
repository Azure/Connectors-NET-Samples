//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.AzureAutomation;
using Azure.Connectors.Sdk.AzureDigitalTwins;
using Azure.Connectors.Sdk.AzureVM;
using Azure.Connectors.Sdk.KeyVault;
using Azure.Connectors.Sdk.MicrosoftBookings;
using Azure.Connectors.Sdk.Office365Groups;
using Azure.Connectors.Sdk.Office365GroupsMail;
using Azure.Connectors.Sdk.Onenote;
using Azure.Connectors.Sdk.PowerBI;
using Azure.Connectors.Sdk.Shifts;
using Azure.Connectors.Sdk.Todo;
using Azure.Core;
using Microsoft.Azure.Functions.Worker.Http;

namespace DirectConnector.Tests;

[TestClass]
public class AdditionalConnectorFunctionsTests
{
    [TestMethod]
    public async Task AzureAutomationListSubscriptionsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureAutomationClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new AzureAutomationFunctions(TestHelpers.CreateNullLogger<AzureAutomationFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(functions.ListSubscriptionsAsync)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task AzureAutomationGetJobStatusAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureAutomationClient(uri, credential, options),
            "{\"id\":\"job-1\",\"properties\":{\"status\":\"Completed\"}}");
        var functions = new AzureAutomationFunctions(TestHelpers.CreateNullLogger<AzureAutomationFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(
                (request, cancellationToken) => functions.GetJobStatusAsync(
                    TestHelpers.CreateRequest(url: "https://localhost/api/azureautomation/jobs/status?subscriptionId=sub&resourceGroup=rg&automationAccount=account&jobId=job-1"),
                    cancellationToken))
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task AzureAutomationGetJobStatusAsync_WithMissingJobId_ReturnsBadRequest()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureAutomationClient(uri, credential, options),
            "{}");
        var functions = new AzureAutomationFunctions(TestHelpers.CreateNullLogger<AzureAutomationFunctions>(), client);
        var request = TestHelpers.CreateRequest(
            url: "https://localhost/api/azureautomation/jobs/status?subscriptionId=sub&resourceGroup=rg&automationAccount=account");

        var response = await functions
            .GetJobStatusAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        using var document = JsonDocument.Parse(body);
        Assert.AreEqual(
            "Query parameters 'subscriptionId', 'resourceGroup', 'automationAccount', and 'jobId' are required.",
            document.RootElement.GetProperty("error").GetString());
    }

    [TestMethod]
    public async Task AzureAutomationCreateJobAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureAutomationClient(uri, credential, options),
            "{\"id\":\"job-1\",\"properties\":{\"status\":\"New\"}}");
        var functions = new AzureAutomationFunctions(TestHelpers.CreateNullLogger<AzureAutomationFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(
                (request, cancellationToken) => functions.CreateJobAsync(
                    TestHelpers.CreateRequest(
                        method: "POST",
                        url: "https://localhost/api/azureautomation/jobs?subscriptionId=sub&resourceGroup=rg&automationAccount=account&runbookName=runbook"),
                    cancellationToken))
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task AzureAutomationCreateJobAsync_WithMissingRunbookName_ReturnsBadRequest()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureAutomationClient(uri, credential, options),
            "{}");
        var functions = new AzureAutomationFunctions(TestHelpers.CreateNullLogger<AzureAutomationFunctions>(), client);
        var request = TestHelpers.CreateRequest(
            method: "POST",
            url: "https://localhost/api/azureautomation/jobs?subscriptionId=sub&resourceGroup=rg&automationAccount=account");

        var response = await functions
            .CreateJobAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        using var document = JsonDocument.Parse(body);
        Assert.AreEqual(
            "Query parameters 'subscriptionId', 'resourceGroup', 'automationAccount', and 'runbookName' are required.",
            document.RootElement.GetProperty("error").GetString());
    }

    [TestMethod]
    public async Task AzureDigitalTwinsListModelsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureDigitalTwinsClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new AzureDigitalTwinsFunctions(TestHelpers.CreateNullLogger<AzureDigitalTwinsFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(functions.ListModelsAsync)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task AzureVMListSubscriptionsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureVMClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new AzureVMFunctions(TestHelpers.CreateNullLogger<AzureVMFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(functions.ListSubscriptionsAsync)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task AzureVMGetVirtualMachineAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureVMClient(uri, credential, options),
            "{\"id\":\"/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm-1\",\"name\":\"vm-1\"}");
        var functions = new AzureVMFunctions(TestHelpers.CreateNullLogger<AzureVMFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(
                (request, cancellationToken) => functions.GetVirtualMachineAsync(
                    TestHelpers.CreateRequest(url: "https://localhost/api/azurevm/virtualmachine?subscriptionId=sub&resourceGroup=rg&virtualMachine=vm-1"),
                    cancellationToken))
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task KeyVaultListSecretsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new KeyVaultClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new KeyVaultFunctions(TestHelpers.CreateNullLogger<KeyVaultFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(functions.ListSecretsAsync)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task MicrosoftBookingsListBusinessesAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new MicrosoftBookingsClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new MicrosoftBookingsFunctions(TestHelpers.CreateNullLogger<MicrosoftBookingsFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(functions.ListBusinessesAsync)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task Office365GroupsListGroupsAsync_WithPagedResponse_ReturnsAllGroups()
    {
        var responseBodies = new Queue<string>(new[]
        {
            "{\"@odata.nextLink\":\"https://test.azure.com/groups?page=2\",\"value\":[{\"id\":\"group-1\"}]}",
            "{\"value\":[{\"id\":\"group-2\"}]}",
        });
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new Office365GroupsClient(uri, credential, options),
            () => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseBodies.Dequeue()),
            });
        var functions = new Office365GroupsFunctions(TestHelpers.CreateNullLogger<Office365GroupsFunctions>(), client);

        var response = await functions
            .ListGroupsAsync(TestHelpers.CreateRequest(), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("group-1", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("group-2", StringComparison.Ordinal));
        Assert.AreEqual(expected: 0, actual: responseBodies.Count);
    }

    [TestMethod]
    public async Task Office365GroupsMailListGroupsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new Office365GroupsMailClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new Office365GroupsMailFunctions(TestHelpers.CreateNullLogger<Office365GroupsMailFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(functions.ListGroupsAsync)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task Office365GroupsMailListConversationsAsync_WithPagedResponse_ReturnsAllConversations()
    {
        var responseBodies = new Queue<string>(new[]
        {
            "{\"@odata.nextLink\":\"https://test.azure.com/conversations?page=2\",\"value\":[{\"id\":\"conversation-1\"}]}",
            "{\"value\":[{\"id\":\"conversation-2\"}]}",
        });
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new Office365GroupsMailClient(uri, credential, options),
            () => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseBodies.Dequeue()),
            });
        var functions = new Office365GroupsMailFunctions(TestHelpers.CreateNullLogger<Office365GroupsMailFunctions>(), client);

        var response = await functions
            .ListConversationsAsync(
                TestHelpers.CreateRequest(url: "https://localhost/api/office365groupsmail/conversations?groupId=group-1"),
                CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("conversation-1", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("conversation-2", StringComparison.Ordinal));
        Assert.AreEqual(expected: 0, actual: responseBodies.Count);
    }

    [TestMethod]
    public async Task OnenoteListNotebooksAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new OnenoteClient(uri, credential, options),
            "[]");
        var functions = new OnenoteFunctions(TestHelpers.CreateNullLogger<OnenoteFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(functions.ListNotebooksAsync)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task PowerBIListWorkspacesAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new PowerBIClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new PowerBIFunctions(TestHelpers.CreateNullLogger<PowerBIFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(functions.ListWorkspacesAsync)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task PowerBIListScorecardsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new PowerBIClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new PowerBIFunctions(TestHelpers.CreateNullLogger<PowerBIFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(
                (request, cancellationToken) => functions.ListScorecardsAsync(
                    TestHelpers.CreateRequest(url: "https://localhost/api/powerbi/scorecards?workspace=workspace-1"),
                    cancellationToken))
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task ShiftsListTeamsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new ShiftsClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new ShiftsFunctions(TestHelpers.CreateNullLogger<ShiftsFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(functions.ListTeamsAsync)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task ShiftsListCrossTeamShiftsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new ShiftsClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new ShiftsFunctions(TestHelpers.CreateNullLogger<ShiftsFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(functions.ListCrossTeamShiftsAsync)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task TodoListTaskListsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new TodoClient(uri, credential, options),
            "[]");
        var functions = new TodoFunctions(TestHelpers.CreateNullLogger<TodoFunctions>(), client);
        await AdditionalConnectorFunctionsTests
            .AssertSuccessAsync(functions.ListTaskListsAsync)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [TestMethod]
    public async Task SharedExecutor_WithConnectorError_ReturnsBadGateway()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new KeyVaultClient(uri, credential, options),
            "{\"error\":{\"code\":\"ServiceUnavailable\",\"message\":\"Try again\"}}",
            HttpStatusCode.ServiceUnavailable);
        var functions = new KeyVaultFunctions(TestHelpers.CreateNullLogger<KeyVaultFunctions>(), client);

        var response = await functions
            .ListSecretsAsync(TestHelpers.CreateRequest(), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.IsTrue(((MockHttpResponseData)response).GetBodyAsString().Contains("\"success\":false", StringComparison.Ordinal));
    }

    private static TClient CreateClient<TClient>(
        Func<Uri, TokenCredential, ConnectorClientOptions, TClient> clientFactory,
        string responseBody,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        where TClient : ConnectorClientBase
    {
        return AdditionalConnectorFunctionsTests.CreateClient(
            clientFactory,
            () => new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseBody),
            });
    }

    private static TClient CreateClient<TClient>(
        Func<Uri, TokenCredential, ConnectorClientOptions, TClient> clientFactory,
        Func<HttpResponseMessage> responseFactory)
        where TClient : ConnectorClientBase
    {
        var (credential, options) = TestHelpers.CreateMockedClientSetup(responseFactory);
        return clientFactory(new Uri("https://test.azure.com/connection"), credential, options);
    }

    private static async Task AssertSuccessAsync(
        Func<HttpRequestData, CancellationToken, Task<HttpResponseData>> operation)
    {
        var response = await operation(TestHelpers.CreateRequest(), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(((MockHttpResponseData)response).GetBodyAsString().Contains("\"success\":true", StringComparison.Ordinal));
    }
}