param(
    [int]$ExpectedQueueCount = 17,
    [string]$DocsDir = "docs"
)

$ErrorActionPreference = "Stop"

function Add-Check {
    param(
        [System.Collections.Generic.List[object]]$Bucket,
        [string]$Name,
        [bool]$Passed,
        [string]$Details
    )

    $Bucket.Add([pscustomobject]@{
        Check   = $Name
        Status  = if ($Passed) { "PASS" } else { "FAIL" }
        Details = $Details
    }) | Out-Null
}

function Has-Pattern {
    param(
        [string]$Text,
        [string]$Pattern
    )

    return [regex]::IsMatch(
        $Text,
        $Pattern,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )
}

$rootPath = (Resolve-Path ".").Path
$docsPath = Join-Path $rootPath $DocsDir

if (-not (Test-Path $docsPath)) {
    throw "Docs directory was not found: $docsPath"
}

$checks = New-Object System.Collections.Generic.List[object]
$fileCache = @{}

$requiredFiles = @(
    "08-design-system.md",
    "09-figma-workflow.md",
    "10-vibe-design-process.md",
    "11-figma-one-shot-runbook.md",
    "12-wave1-screen-blueprint.md",
    "13-one-shot-preflight-checklist.md",
    "14-ux-copy-wave1-ua.md",
    "15-component-specs-wave1.md",
    "16-interaction-matrix-wave1.md",
    "17-batch-execution-script.md",
    "18-one-shot-dry-run-report.md",
    "19-one-shot-command-center.md",
    "figma-queue.md"
)

foreach ($file in $requiredFiles) {
    $fullPath = Join-Path $docsPath $file
    $exists = Test-Path $fullPath
    $existsDetails = if ($exists) { "ok" } else { "missing" }
    Add-Check -Bucket $checks -Name "Required file: $file" -Passed $exists -Details $existsDetails

    if ($exists) {
        $fileCache[$file] = Get-Content -Path $fullPath -Raw -Encoding UTF8
    }
}

if (-not $fileCache.ContainsKey("figma-queue.md")) {
    Write-Host ""
    Write-Host "Cannot continue: docs/figma-queue.md is missing." -ForegroundColor Red
    exit 1
}

$queueText = $fileCache["figma-queue.md"]
$runbookText = if ($fileCache.ContainsKey("11-figma-one-shot-runbook.md")) { $fileCache["11-figma-one-shot-runbook.md"] } else { "" }
$preflightText = if ($fileCache.ContainsKey("13-one-shot-preflight-checklist.md")) { $fileCache["13-one-shot-preflight-checklist.md"] } else { "" }
$batchText = if ($fileCache.ContainsKey("17-batch-execution-script.md")) { $fileCache["17-batch-execution-script.md"] } else { "" }
$blueprintText = if ($fileCache.ContainsKey("12-wave1-screen-blueprint.md")) { $fileCache["12-wave1-screen-blueprint.md"] } else { "" }
$copyText = if ($fileCache.ContainsKey("14-ux-copy-wave1-ua.md")) { $fileCache["14-ux-copy-wave1-ua.md"] } else { "" }
$commandCenterText = if ($fileCache.ContainsKey("19-one-shot-command-center.md")) { $fileCache["19-one-shot-command-center.md"] } else { "" }
$dryRunText = if ($fileCache.ContainsKey("18-one-shot-dry-run-report.md")) { $fileCache["18-one-shot-dry-run-report.md"] } else { "" }

# Queue ID coverage
$queueMatches = [regex]::Matches($queueText, '^### QUEUE-(\d{3})', [System.Text.RegularExpressions.RegexOptions]::Multiline)
$queueIds = @()
foreach ($m in $queueMatches) {
    $queueIds += [int]$m.Groups[1].Value
}

$expectedIds = 1..$ExpectedQueueCount
$missingIds = @($expectedIds | Where-Object { $_ -notin $queueIds })
$extraIds = @($queueIds | Where-Object { $_ -lt 1 -or $_ -gt $ExpectedQueueCount })

Add-Check -Bucket $checks -Name "Queue entry count" -Passed ($queueIds.Count -eq $ExpectedQueueCount) -Details "found=$($queueIds.Count), expected=$ExpectedQueueCount"
Add-Check -Bucket $checks -Name "Queue range continuity" -Passed (($missingIds.Count -eq 0) -and ($extraIds.Count -eq 0)) -Details ("missing={0}; extra={1}" -f (($missingIds -join ",").Trim(), ($extraIds -join ",").Trim()))

$pendingCount = [regex]::Matches($queueText, '\[\s\]\s*Pending').Count
Add-Check -Bucket $checks -Name "Pending status count" -Passed ($pendingCount -eq $ExpectedQueueCount) -Details "pending=$pendingCount, expected=$ExpectedQueueCount"

$summaryPendingOk = Has-Pattern -Text $queueText -Pattern ("\|\s*Pending\s*\|\s*{0}\s*\|" -f $ExpectedQueueCount)
$summaryAppliedOk = Has-Pattern -Text $queueText -Pattern '\|\s*Applied\s*\|\s*0\s*\|'
$summaryTotalOk = Has-Pattern -Text $queueText -Pattern ("\|\s*Total\s*\|\s*{0}\s*\|" -f $ExpectedQueueCount)

Add-Check -Bucket $checks -Name "Queue summary Pending row" -Passed $summaryPendingOk -Details "Pending row must match expected count"
Add-Check -Bucket $checks -Name "Queue summary Applied row" -Passed $summaryAppliedOk -Details "Applied row must be 0 before launch"
Add-Check -Bucket $checks -Name "Queue summary Total row" -Passed $summaryTotalOk -Details "Total row must match expected count"

# Hard gate markers
$ds08 = if ($fileCache.ContainsKey("08-design-system.md")) { $fileCache["08-design-system.md"] } else { "" }
$wf09 = if ($fileCache.ContainsKey("09-figma-workflow.md")) { $fileCache["09-figma-workflow.md"] } else { "" }
$vp10 = if ($fileCache.ContainsKey("10-vibe-design-process.md")) { $fileCache["10-vibe-design-process.md"] } else { "" }

$hardGate08 = Has-Pattern -Text $ds08 -Pattern 'Owner approval gate|hard gate'
$hardGate09 = Has-Pattern -Text $wf09 -Pattern 'hard gate'
$hardGate10 = Has-Pattern -Text $vp10 -Pattern 'hard gate'
$hardGateQueue = Has-Pattern -Text $queueText -Pattern 'hard gate'

Add-Check -Bucket $checks -Name "Hard gate marker in 08-design-system.md" -Passed $hardGate08 -Details "must document explicit approval gate"
Add-Check -Bucket $checks -Name "Hard gate marker in 09-figma-workflow.md" -Passed $hardGate09 -Details "must document explicit approval gate"
Add-Check -Bucket $checks -Name "Hard gate marker in 10-vibe-design-process.md" -Passed $hardGate10 -Details "must document explicit approval gate"
Add-Check -Bucket $checks -Name "Hard gate marker in figma-queue.md" -Passed $hardGateQueue -Details "must document explicit approval gate"

# Approval template consistency by anchor tokens
$approvalAnchorPattern = 'MCP FIGMA:.*QUEUE-001\.\.\.QUEUE-017'
Add-Check -Bucket $checks -Name "Approval template anchor in queue" -Passed (Has-Pattern -Text $queueText -Pattern $approvalAnchorPattern) -Details "must include MCP FIGMA + QUEUE range"
Add-Check -Bucket $checks -Name "Approval template anchor in runbook" -Passed (Has-Pattern -Text $runbookText -Pattern $approvalAnchorPattern) -Details "must include MCP FIGMA + QUEUE range"
Add-Check -Bucket $checks -Name "Approval template anchor in batch doc" -Passed (Has-Pattern -Text $batchText -Pattern $approvalAnchorPattern) -Details "must include MCP FIGMA + QUEUE range"
Add-Check -Bucket $checks -Name "Approval template anchor in command center" -Passed (Has-Pattern -Text $commandCenterText -Pattern $approvalAnchorPattern) -Details "must include MCP FIGMA + QUEUE range"

# Localization baseline checks via ASCII anchors
$hasQueue013 = Has-Pattern -Text $queueText -Pattern 'QUEUE-013'
$hasQueue013DefaultUa = Has-Pattern -Text $queueText -Pattern 'Default Ukrainian'
$hasRunbookDefaultUa = Has-Pattern -Text $runbookText -Pattern 'default UA'
$hasBatchDefaultUa = Has-Pattern -Text $batchText -Pattern 'default language.*UA|default UA'
$hasChecklistDefaultLanguage = Has-Pattern -Text $preflightText -Pattern 'default language\s*='
$hasCopyDefaultKey = Has-Pattern -Text $copyText -Pattern 'profile\.interface_language\.value_default'
$hasBlueprintWave1 = Has-Pattern -Text $blueprintText -Pattern 'Wave 1'

Add-Check -Bucket $checks -Name "Queue includes QUEUE-013" -Passed $hasQueue013 -Details "localization task must exist"
Add-Check -Bucket $checks -Name "Queue states Default Ukrainian" -Passed $hasQueue013DefaultUa -Details "QUEUE-013 title must include default Ukrainian"
Add-Check -Bucket $checks -Name "Runbook references default UA" -Passed $hasRunbookDefaultUa -Details "runbook should keep localization baseline"
Add-Check -Bucket $checks -Name "Batch script references default UA" -Passed $hasBatchDefaultUa -Details "batch sequence should keep localization baseline"
Add-Check -Bucket $checks -Name "Preflight checklist has default language gate" -Passed $hasChecklistDefaultLanguage -Details "checklist should require default language validation"
Add-Check -Bucket $checks -Name "UA copy deck has default language key" -Passed $hasCopyDefaultKey -Details "copy source should include interface default key"
Add-Check -Bucket $checks -Name "Blueprint present for Wave 1 screens" -Passed $hasBlueprintWave1 -Details "screen blueprint must exist"

# Cross-link integrity
$runbookHasCopy = Has-Pattern -Text $runbookText -Pattern 'docs/14-ux-copy-wave1-ua\.md'
$runbookHasInteraction = Has-Pattern -Text $runbookText -Pattern 'docs/16-interaction-matrix-wave1\.md'
$checklistHasBatch = Has-Pattern -Text $preflightText -Pattern 'docs/17-batch-execution-script\.md'
$checklistHasDryRun = Has-Pattern -Text $preflightText -Pattern 'docs/18-one-shot-dry-run-report\.md'
$dryRunHasNoConnectionRule = Has-Pattern -Text $dryRunText -Pattern 'no Figma MCP connection without explicit owner approval|Hard gate'

Add-Check -Bucket $checks -Name "Runbook references copy source" -Passed $runbookHasCopy -Details "runbook should reference docs/14"
Add-Check -Bucket $checks -Name "Runbook references interaction source" -Passed $runbookHasInteraction -Details "runbook should reference docs/16"
Add-Check -Bucket $checks -Name "Checklist references batch script" -Passed $checklistHasBatch -Details "checklist should reference docs/17"
Add-Check -Bucket $checks -Name "Checklist references dry-run report" -Passed $checklistHasDryRun -Details "checklist should reference docs/18"
Add-Check -Bucket $checks -Name "Dry-run report enforces no pre-approval connection" -Passed $dryRunHasNoConnectionRule -Details "dry-run report should preserve gate policy"

$failed = @($checks | Where-Object { $_.Status -eq "FAIL" })
$passed = @($checks | Where-Object { $_.Status -eq "PASS" })

Write-Host ""
Write-Host "BaguetteDesign One-shot Preflight Audit" -ForegroundColor Cyan
Write-Host "Workspace: $rootPath"
Write-Host "Docs path: $docsPath"
Write-Host "Expected queue count: $ExpectedQueueCount"
Write-Host ""
($checks | Format-Table -AutoSize | Out-String).TrimEnd() | Write-Host
Write-Host ""
Write-Host ("Summary: PASS={0}, FAIL={1}" -f $passed.Count, $failed.Count)

if ($failed.Count -gt 0) {
    Write-Host "Preflight status: NOT READY" -ForegroundColor Red
    exit 1
}

Write-Host "Preflight status: READY" -ForegroundColor Green
exit 0
