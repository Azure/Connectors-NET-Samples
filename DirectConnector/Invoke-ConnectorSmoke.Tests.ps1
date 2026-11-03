#requires -Version 7.0
#------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
#------------------------------------------------------------
$ErrorActionPreference = 'Stop'
$runner = Join-Path $PSScriptRoot 'Invoke-ConnectorSmoke.ps1'
$output = Join-Path ([IO.Path]::GetTempPath()) ('connector-smoke-test-' + [guid]::NewGuid().ToString('N') + '.json')
$state = [pscustomobject]@{ Mode = 'Succeeded'; Requests = 0 }

function Invoke-WebRequest {
    param([uri] $Uri, [int] $TimeoutSec, [switch] $SkipHttpErrorCheck)
    $state.Requests++
    if (-not $SkipHttpErrorCheck -or $TimeoutSec -lt 1) { throw 'Request contract is missing bounded timeout or error-response handling.' }
    $payload = @{
        success = $true; value = @(); subscriptions = @(); topicTypes = @(); storageAccounts = @();
        accounts = @(); eventHubs = @(); contentTypes = @(); sources = @(); users = @(); groups = @();
        categories = @(); files = @(); calendars = @(); contactFolders = @(); tasks = @(); queues = @();
        topics = @(); sites = @(); teams = @(); shares = @(); networks = @(); drives = @(); alerts = @()
    }
    if ($Uri.AbsolutePath.EndsWith('/users/me', [StringComparison]::Ordinal)) {
        return @{ StatusCode = 200; Content = '{"Id":"synthetic-user"}' }
    }
    if ($Uri.AbsolutePath.EndsWith('/dataverse/environments', [StringComparison]::Ordinal)) {
        return @{ StatusCode = 200; Content = '{"value":[]}' }
    }
    if ($Uri.AbsolutePath.EndsWith('/wdatp/alerts', [StringComparison]::Ordinal) -and $state.Mode -in @('ExpectedDefender', 'WrongDefender')) {
        $message = if ($state.Mode -eq 'ExpectedDefender') {
            'Missing user permissions. API required permissions: ViewData, user permissions: None.'
        } else { 'Different authorization failure.' }
        $detail = @{ error = @{ code = 'Forbidden'; message = $message } } | ConvertTo-Json -Compress
        $body = @{ success = $false; statusCode = 403; details = $detail } | ConvertTo-Json -Compress
        return @{ StatusCode = 502; Content = $body }
    }
    if ($Uri.AbsolutePath.EndsWith('/arm/subscriptions', [StringComparison]::Ordinal)) {
        switch ($state.Mode) {
            'FalseSuccess' { return @{ StatusCode = 200; Content = '{"success":false,"subscriptions":[]}' } }
            'MissingPayload' { return @{ StatusCode = 200; Content = '{"success":true}' } }
            'StringSuccess' { return @{ StatusCode = 200; Content = '{"success":"true","subscriptions":[]}' } }
            'Malformed' { return @{ StatusCode = 200; Content = '{' } }
        }
    }
    return @{ StatusCode = 200; Content = ($payload | ConvertTo-Json -Depth 3 -Compress) }
}

try {
    $functions = @(& $runner -ListFunctions)
    if ($functions.Count -ne 38 -or @($functions | Select-Object -Unique).Count -ne 38) { throw 'Smoke discovery inventory is incomplete or ambiguous.' }
    if ($state.Requests -ne 0) { throw 'Inventory listing must not issue requests.' }
    foreach ($mode in @('Succeeded', 'ExpectedDefender', 'FalseSuccess', 'MissingPayload', 'StringSuccess', 'Malformed', 'WrongDefender')) {
        $state.Mode = $mode
        $state.Requests = 0
        $failed = $false
        try { $null = & $runner -OutputPath $output } catch {
            if ($_.Exception.Message -notlike 'Some smoke operations did not validate.*') { throw }
            $failed = $true
        }
        $results = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json -ErrorAction Stop
        if (@($results).Count -ne 38 -or $state.Requests -ne 38) { throw "Scenario '$mode' did not cover all requests." }
        $expectedFailure = $mode -notin @('Succeeded', 'ExpectedDefender')
        if ($failed -ne $expectedFailure) { throw "Scenario '$mode' returned the wrong terminal result." }
        $expectedCount = if ($mode -eq 'ExpectedDefender') { 1 } else { 0 }
        if (@($results | Where-Object Outcome -EQ 'ExpectedPermissionFailure').Count -ne $expectedCount) { throw "Scenario '$mode' misclassified permission failures." }
        if ($mode -eq 'ExpectedDefender') {
            $defender = $results | Where-Object Function -EQ 'WdatpGetAlerts'
            if (-not $defender.SdkValidationPassed -or $defender.DataReadSucceeded) { throw 'Permission-error validation must not claim a successful data read.' }
        }
    }
    $state.Requests = 0
    $rejected = $false
    foreach ($baseUri in @('https://example.invalid', 'http://user@127.0.0.1:7071', 'http://127.0.0.1:7071?x=1', 'http://127.0.0.1:7071#fragment')) {
        $rejected = $false
        try { $null = & $runner -BaseUri $baseUri -ListFunctions } catch { $rejected = $true }
        if (-not $rejected -or $state.Requests -ne 0) { throw 'Unsupported target guard failed.' }
    }
    if (-not $rejected -or $state.Requests -ne 0) { throw 'Remote-target guard failed.' }
    'Passed 12 smoke-runner regression scenarios; all request inventories were non-empty.'
}
finally {
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -ErrorAction Stop }
}
