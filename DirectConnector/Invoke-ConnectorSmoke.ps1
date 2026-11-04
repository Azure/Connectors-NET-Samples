#requires -Version 7.0
#------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
#------------------------------------------------------------
[CmdletBinding()]
param(
    [uri] $BaseUri = 'http://127.0.0.1:7071',
    [string] $OutputPath,
    [ValidateRange(1, 300)] [int] $TimeoutSeconds = 120,
    [switch] $ListFunctions
)

$ErrorActionPreference = 'Stop'
if (-not $BaseUri.IsAbsoluteUri -or $BaseUri.Scheme -notin @('http', 'https') -or -not $BaseUri.IsLoopback -or $BaseUri.UserInfo -or $BaseUri.Query -or $BaseUri.Fragment) {
    throw 'Use an absolute loopback HTTP(S) Functions URL. This runner does not target remote services directly.'
}

$checks = @(
    @{ Function = 'ArmListSubscriptions'; Route = 'arm/subscriptions'; Connector = 'arm'; Method = 'SubscriptionsListAsync'; Field = 'subscriptions' }
    @{ Function = 'AzureAutomationListSubscriptions'; Route = 'azureautomation/subscriptions'; Connector = 'azureautomation'; Method = 'SubscriptionsListAsync'; Field = 'value' }
    @{ Function = 'EventGridListTopicTypes'; Route = 'eventgrid/topictypes'; Connector = 'azureeventgrid'; Method = 'TopicTypesListAsync'; Field = 'topicTypes.value' }
    @{ Function = 'EventGridListSubscriptions'; Route = 'eventgrid/subscriptions'; Connector = 'azureeventgrid'; Method = 'SubscriptionsListAsync'; Field = 'subscriptions.value' }
    @{ Function = 'ListLogAnalyticsSubscriptions'; Route = 'loganalytics/subscriptions'; Connector = 'azuremonitorlogs'; Method = 'ListSubscriptionsAsync'; Field = 'subscriptions' }
    @{ Function = 'AzureVMListSubscriptions'; Route = 'azurevm/subscriptions'; Connector = 'azurevm'; Method = 'SubscriptionsListAsync'; Field = 'value' }
    @{ Function = 'AzureQueuesListStorageAccounts'; Route = 'azurequeues/storageaccounts'; Connector = 'azurequeues'; Method = 'GetStorageAccountsAsync'; Field = 'storageAccounts.value' }
    @{ Function = 'AzureTablesListStorageAccounts'; Route = 'azuretables/storageaccounts'; Connector = 'azuretables'; Method = 'GetStorageAccountsAsync'; Field = 'storageAccounts.value' }
    @{ Function = 'DataverseListEnvironments'; Route = 'dataverse/environments'; Connector = 'commondataservice'; Method = 'GetDataSetsAsync'; Field = 'value' }
    @{ Function = 'CosmosDbListAccounts'; Route = 'cosmosdb/accounts'; Connector = 'documentdb'; Method = 'GetCosmosDbAccountsAsync'; Field = 'accounts.value' }
    @{ Function = 'EventHubsListHubs'; Route = 'eventhubs/hubs'; Connector = 'eventhubs'; Method = 'GetEventHubsAsync'; Field = 'eventHubs' }
    @{ Function = 'EventHubsListContentTypes'; Route = 'eventhubs/contenttypes'; Connector = 'eventhubs'; Method = 'GetContentTypesAsync'; Field = 'contentTypes' }
    @{ Function = 'ExcelGetSources'; Route = 'excel/sources'; Connector = 'excelonlinebusiness'; Method = 'GetSourcesAsync'; Field = 'sources.value' }
    @{ Function = 'MicrosoftBookingsListBusinesses'; Route = 'microsoftbookings/businesses'; Connector = 'microsoftbookings'; Method = 'ListBookingsBusinessUserAsAdminAsync'; Field = 'value.mailboxes' }
    @{ Function = 'ListGraphUsers'; Route = 'graph/users'; Connector = 'msgraphgroupsanduser'; Method = 'ListUsersAsync'; Field = 'users.value' }
    @{ Function = 'ListGraphGroups'; Route = 'graph/groups'; Connector = 'msgraphgroupsanduser'; Method = 'ListGroupsByDisplayNameSearchAsync'; Field = 'groups.value' }
    @{ Function = 'GetCategories'; Route = 'categories'; Connector = 'office365'; Method = 'GetOutlookCategoryNamesAsync'; Field = 'categories' }
    @{ Function = 'Office365GroupsListGroups'; Route = 'office365groups/groups'; Connector = 'office365groups'; Method = 'ListGroupsAsync'; Field = 'value' }
    @{ Function = 'Office365GroupsMailListGroups'; Route = 'office365groupsmail/groups'; Connector = 'office365groupsmail'; Method = 'ListGroupsAsync'; Field = 'value.value' }
    @{ Function = 'GetMyProfile'; Route = 'users/me'; Connector = 'office365users'; Method = 'MyProfileAsync'; Field = 'Id' }
    @{ Function = 'ListOneDriveRoot'; Route = 'onedrive/root'; Connector = 'onedriveforbusiness'; Method = 'ListRootFolderAsync'; Field = 'files' }
    @{ Function = 'OnenoteListNotebooks'; Route = 'onenote/notebooks'; Connector = 'onenote'; Method = 'GetNotebooksAsync'; Field = 'value' }
    @{ Function = 'OutlookListCalendars'; Route = 'outlook/calendars'; Connector = 'outlook'; Method = 'CalendarGetTablesAsync'; Field = 'calendars.value' }
    @{ Function = 'OutlookListContactFolders'; Route = 'outlook/contactfolders'; Connector = 'outlook'; Method = 'ContactGetTablesAsync'; Field = 'contactFolders.value' }
    @{ Function = 'PlannerListGroups'; Route = 'planner/groups'; Connector = 'planner'; Method = 'ListGroupsAsync'; Field = 'groups.value' }
    @{ Function = 'PlannerListMyTasks'; Route = 'planner/tasks'; Connector = 'planner'; Method = 'ListMyTasksAsync'; Field = 'tasks' }
    @{ Function = 'PowerBIListWorkspaces'; Route = 'powerbi/workspaces'; Connector = 'powerbi'; Method = 'ListGroupsAsync'; Field = 'value.value' }
    @{ Function = 'ServiceBusListQueues'; Route = 'servicebus/queues'; Connector = 'servicebus'; Method = 'GetQueuesAsync'; Field = 'queues' }
    @{ Function = 'ServiceBusListTopics'; Route = 'servicebus/topics'; Connector = 'servicebus'; Method = 'GetTopicsAsync'; Field = 'topics' }
    @{ Function = 'ListSharePointSites'; Route = 'sharepoint/sites'; Connector = 'sharepointonline'; Method = 'GetDataSetsAsync'; Field = 'sites' }
    @{ Function = 'ShiftsListTeams'; Route = 'shifts/teams'; Connector = 'shifts'; Method = 'GetAllTeamsAsync'; Field = 'value.value' }
    @{ Function = 'GetAllTeams'; Route = 'teams/teams'; Connector = 'teams'; Method = 'GetAllTeamsAsync'; Field = 'teams.value' }
    @{ Function = 'TodoListTaskLists'; Route = 'todo/lists'; Connector = 'todo'; Method = 'GetAllTodoListsAsync'; Field = 'value' }
    @{ Function = 'UniversalPrintListShares'; Route = 'universalprint/shares'; Connector = 'universalprint'; Method = 'ListRecentSharesAsync'; Field = 'shares.value' }
    @{ Function = 'YammerGetNetworks'; Route = 'yammer/networks'; Connector = 'yammer'; Method = 'GetNetworksAsync'; Field = 'networks' }
    @{ Function = 'WordOnlineGetSources'; Route = 'wordonline/sources'; Connector = 'wordonlinebusiness'; Method = 'GetSourcesAsync'; Field = 'sources.value' }
    @{ Function = 'WordOnlineGetDrives'; Route = 'wordonline/drives'; Connector = 'wordonlinebusiness'; Method = 'GetDrivesAsync'; Field = 'drives.value' }
    @{ Function = 'WdatpGetAlerts'; Route = 'wdatp/alerts'; Connector = 'wdatp'; Method = 'GetAlertsAsync'; Field = 'alerts' }
)

if (@($checks.Function | Select-Object -Unique).Count -ne $checks.Count) {
    throw 'Duplicate Function names in the smoke inventory.'
}
if ($ListFunctions) {
    $checks.Function
    return
}

$results = foreach ($check in $checks) {
    $status = 'Failed'
    $httpStatus = $null
    $downstreamStatus = $null
    $errorCode = $null
    try {
        $response = Invoke-WebRequest -Uri ($BaseUri.AbsoluteUri.TrimEnd('/') + '/api/' + $check.Route) -TimeoutSec $TimeoutSeconds -SkipHttpErrorCheck
        $httpStatus = [int] $response.StatusCode
        $body = $response.Content | ConvertFrom-Json -ErrorAction Stop
        $successProperty = $body.PSObject.Properties['success']
        $payload = $body
        foreach ($segment in $check.Field.Split('.')) {
            if ($payload -isnot [pscustomobject]) {
                $payload = $null
                break
            }
            $property = $payload.PSObject.Properties[$segment]
            if ($null -eq $property) {
                $payload = $null
                break
            }
            $payload = $property.Value
        }
        $validPayload = $payload -is [array]
        $validSuccess = $successProperty -and $successProperty.Value -is [bool] -and $successProperty.Value -eq $true
        if ($check.Function -ceq 'DataverseListEnvironments') {
            $validSuccess = $null -eq $successProperty
        }
        elseif ($check.Function -ceq 'GetMyProfile') {
            $validSuccess = $null -eq $successProperty
            $validPayload = $payload -is [string] -and -not [string]::IsNullOrWhiteSpace($payload)
        }
        if ($httpStatus -eq 200 -and $validSuccess -and $validPayload) {
            $status = 'Succeeded'
        }
        else {
            $downstreamStatus = $body.statusCode
            if ($body.details) {
                $detail = $body.details | ConvertFrom-Json -ErrorAction Stop
                $errorCode = $detail.error.code
                if (-not $errorCode) { $errorCode = $detail.'odata.error'.code }
                if (-not $errorCode) { $errorCode = $detail.code }
                if ($check.Function -ceq 'WdatpGetAlerts' -and $httpStatus -eq 502 -and $downstreamStatus -eq 403 -and $errorCode -ceq 'Forbidden' -and $detail.error.message -ceq 'Missing user permissions. API required permissions: ViewData, user permissions: None.') {
                    $status = 'ExpectedPermissionFailure'
                }
            }
            if (-not $errorCode) { $errorCode = if ($null -ne $downstreamStatus) { 'ConnectorHttp' + $downstreamStatus } else { 'InvalidSampleResponse' } }
        }
    }
    catch {
        $errorCode = $_.Exception.GetType().Name
        Write-Warning ("Smoke request for '{0}' could not be validated ({1})." -f $check.Function, $errorCode)
    }
    [pscustomobject]@{
        Function = $check.Function
        Connector = $check.Connector
        SdkMethod = $check.Method
        Outcome = $status
        HttpStatus = $httpStatus
        DownstreamStatus = $downstreamStatus
        ErrorCode = $errorCode
        DataReadSucceeded = ($status -eq 'Succeeded')
        SdkValidationPassed = ($status -in @('Succeeded', 'ExpectedPermissionFailure'))
    }
}

$json = $results | ConvertTo-Json -Depth 5
if ($OutputPath) { [IO.File]::WriteAllText($OutputPath, $json) }
$results
if (@($results | Where-Object { -not $_.SdkValidationPassed }).Count -gt 0) {
    throw 'Some smoke operations did not validate. Inspect the structured results; failures are not success-shaped defaults.'
}
