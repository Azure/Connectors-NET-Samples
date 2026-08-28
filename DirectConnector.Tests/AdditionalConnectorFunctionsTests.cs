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
    public Task AzureAutomationListSubscriptionsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureAutomationClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new AzureAutomationFunctions(TestHelpers.CreateNullLogger<AzureAutomationFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(functions.ListSubscriptionsAsync);
    }

    [TestMethod]
    public Task AzureAutomationGetJobStatusAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureAutomationClient(uri, credential, options),
            "{\"id\":\"job-1\",\"properties\":{\"status\":\"Completed\"}}");
        var functions = new AzureAutomationFunctions(TestHelpers.CreateNullLogger<AzureAutomationFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(
            (request, cancellationToken) => functions.GetJobStatusAsync(
                TestHelpers.CreateRequest(url: "https://localhost/api/azureautomation/jobs/status?subscriptionId=sub&resourceGroup=rg&automationAccount=account&jobId=job-1"),
                cancellationToken));
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
    public Task AzureAutomationCreateJobAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureAutomationClient(uri, credential, options),
            "{\"id\":\"job-1\",\"properties\":{\"status\":\"New\"}}");
        var functions = new AzureAutomationFunctions(TestHelpers.CreateNullLogger<AzureAutomationFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(
            (request, cancellationToken) => functions.CreateJobAsync(
                TestHelpers.CreateRequest(
                    method: "POST",
                    url: "https://localhost/api/azureautomation/jobs?subscriptionId=sub&resourceGroup=rg&automationAccount=account&runbookName=runbook"),
                cancellationToken));
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
    public Task AzureDigitalTwinsListModelsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureDigitalTwinsClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new AzureDigitalTwinsFunctions(TestHelpers.CreateNullLogger<AzureDigitalTwinsFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(functions.ListModelsAsync);
    }

    [TestMethod]
    public Task AzureVMListSubscriptionsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureVMClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new AzureVMFunctions(TestHelpers.CreateNullLogger<AzureVMFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(functions.ListSubscriptionsAsync);
    }

    [TestMethod]
    public Task AzureVMGetVirtualMachineAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new AzureVMClient(uri, credential, options),
            "{\"id\":\"/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm-1\",\"name\":\"vm-1\"}");
        var functions = new AzureVMFunctions(TestHelpers.CreateNullLogger<AzureVMFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(
            (request, cancellationToken) => functions.GetVirtualMachineAsync(
                TestHelpers.CreateRequest(url: "https://localhost/api/azurevm/virtualmachine?subscriptionId=sub&resourceGroup=rg&virtualMachine=vm-1"),
                cancellationToken));
    }

    [TestMethod]
    public Task KeyVaultListSecretsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new KeyVaultClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new KeyVaultFunctions(TestHelpers.CreateNullLogger<KeyVaultFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(functions.ListSecretsAsync);
    }

    [TestMethod]
    public Task MicrosoftBookingsListBusinessesAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new MicrosoftBookingsClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new MicrosoftBookingsFunctions(TestHelpers.CreateNullLogger<MicrosoftBookingsFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(functions.ListBusinessesAsync);
    }

    [TestMethod]
    public Task Office365GroupsListGroupsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new Office365GroupsClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new Office365GroupsFunctions(TestHelpers.CreateNullLogger<Office365GroupsFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(functions.ListGroupsAsync);
    }

    [TestMethod]
    public Task Office365GroupsMailListGroupsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new Office365GroupsMailClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new Office365GroupsMailFunctions(TestHelpers.CreateNullLogger<Office365GroupsMailFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(functions.ListGroupsAsync);
    }

    [TestMethod]
    public Task Office365GroupsMailListConversationsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new Office365GroupsMailClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new Office365GroupsMailFunctions(TestHelpers.CreateNullLogger<Office365GroupsMailFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(
            (request, cancellationToken) => functions.ListConversationsAsync(
                TestHelpers.CreateRequest(url: "https://localhost/api/office365groupsmail/conversations?groupId=group-1"),
                cancellationToken));
    }

    [TestMethod]
    public Task OnenoteListNotebooksAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new OnenoteClient(uri, credential, options),
            "[]");
        var functions = new OnenoteFunctions(TestHelpers.CreateNullLogger<OnenoteFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(functions.ListNotebooksAsync);
    }

    [TestMethod]
    public Task PowerBIListWorkspacesAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new PowerBIClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new PowerBIFunctions(TestHelpers.CreateNullLogger<PowerBIFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(functions.ListWorkspacesAsync);
    }

    [TestMethod]
    public Task PowerBIListScorecardsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new PowerBIClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new PowerBIFunctions(TestHelpers.CreateNullLogger<PowerBIFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(
            (request, cancellationToken) => functions.ListScorecardsAsync(
                TestHelpers.CreateRequest(url: "https://localhost/api/powerbi/scorecards?workspace=workspace-1"),
                cancellationToken));
    }

    [TestMethod]
    public Task ShiftsListTeamsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new ShiftsClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new ShiftsFunctions(TestHelpers.CreateNullLogger<ShiftsFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(functions.ListTeamsAsync);
    }

    [TestMethod]
    public Task ShiftsListCrossTeamShiftsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new ShiftsClient(uri, credential, options),
            "{\"value\":[]}");
        var functions = new ShiftsFunctions(TestHelpers.CreateNullLogger<ShiftsFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(functions.ListCrossTeamShiftsAsync);
    }

    [TestMethod]
    public Task TodoListTaskListsAsync_WithValidResponse_ReturnsOk()
    {
        using var client = AdditionalConnectorFunctionsTests.CreateClient(
            (uri, credential, options) => new TodoClient(uri, credential, options),
            "[]");
        var functions = new TodoFunctions(TestHelpers.CreateNullLogger<TodoFunctions>(), client);
        return AdditionalConnectorFunctionsTests.AssertSuccessAsync(functions.ListTaskListsAsync);
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
        var (credential, options) = TestHelpers.CreateMockedClientSetup(() => new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(responseBody),
        });
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