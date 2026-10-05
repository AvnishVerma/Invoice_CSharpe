param([string]$ResultsDirectory = "artifacts/test-results")
$ErrorActionPreference = "Stop"
$resultsPath = (Resolve-Path -LiteralPath $ResultsDirectory).Path
$rows = foreach ($file in Get-ChildItem -LiteralPath $resultsPath -Recurse -Filter *.trx) {
    [xml]$document = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($result in $document.TestRun.Results.UnitTestResult) {
        [pscustomobject]@{ Test = $result.testName; Outcome = $result.outcome; Duration = $result.duration; Source = $file.Name }
    }
}
$rows | Sort-Object Test | Export-Csv -LiteralPath (Join-Path $resultsPath "test-cases.csv") -NoTypeInformation
$summary = @("# Automated test results", "", "Generated from TRX files. Theory cases count as separate tests; legacy headless assertions are inside one regression test.", "", "| Project/result file | Passed | Failed | Other |", "| --- | ---: | ---: | ---: |")
foreach ($group in $rows | Group-Object Source) {
    $passed = @($group.Group | Where-Object Outcome -eq "Passed").Count
    $failed = @($group.Group | Where-Object Outcome -eq "Failed").Count
    $other = $group.Count - $passed - $failed
    $summary += "| $($group.Name) | $passed | $failed | $other |"
}
$failedRows = @($rows | Where-Object Outcome -eq "Failed")
if ($failedRows.Count -gt 0) {
    $summary += "", "Failing tests:", ""
    foreach ($row in $failedRows) { $summary += "- $($row.Test)" }
}
$summary += "", "Cobertura coverage:", "", "| Coverage file | Lines covered | Lines valid | Branches covered | Branches valid |", "| --- | ---: | ---: | ---: | ---: |"
foreach ($file in Get-ChildItem -LiteralPath $resultsPath -Recurse -Filter coverage.cobertura.xml) {
    [xml]$coverage = Get-Content -LiteralPath $file.FullName -Raw
    $relative = [IO.Path]::GetRelativePath($resultsPath, $file.FullName)
    $summary += "| $relative | $($coverage.coverage.'lines-covered') | $($coverage.coverage.'lines-valid') | $($coverage.coverage.'branches-covered') | $($coverage.coverage.'branches-valid') |"
}
$summary | Set-Content -LiteralPath (Join-Path $resultsPath "summary.md")
$summary
