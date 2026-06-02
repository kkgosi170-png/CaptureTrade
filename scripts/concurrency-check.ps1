<#
.SYNOPSIS
    Fires N concurrent POSTs of the SAME trade and confirms exactly one row results.

.DESCRIPTION
    Demonstrates the idempotency + concurrency guarantee: many simultaneous submissions of an
    identical external_id must collapse to a single stored trade. Run the API first, then run
    this script. It prints the count of distinct HTTP statuses and the report row for the trade.

.EXAMPLE
    ./scripts/concurrency-check.ps1 -BaseUrl http://localhost:5000 -Count 25
#>
param(
    [string]$BaseUrl = "http://localhost:5000",
    [int]$Count = 25
)

$externalId = "CONC-" + (Get-Date -Format "yyyyMMddHHmmss")
$body = @{
    external_id = $externalId
    account     = "ACC-CONC"
    symbol      = "MSFT"
    side        = "BUY"
    quantity    = 100
    price       = 310.25
    trade_time  = "2025-01-15T10:30:00Z"
    currency    = "USD"
} | ConvertTo-Json

Write-Host "Firing $Count concurrent submissions of external_id=$externalId ..." -ForegroundColor Cyan

$jobs = 1..$Count | ForEach-Object {
    Start-ThreadJob -ScriptBlock {
        param($url, $payload)
        try {
            $resp = Invoke-WebRequest -Uri "$url/api/trades" -Method Post `
                -ContentType "application/json" -Body $payload -SkipHttpErrorCheck
            [int]$resp.StatusCode
        } catch {
            "ERR: $($_.Exception.Message)"
        }
    } -ArgumentList $BaseUrl, $body
}

$results = $jobs | Receive-Job -Wait -AutoRemoveJob

Write-Host "`nHTTP status distribution:" -ForegroundColor Yellow
$results | Group-Object | ForEach-Object { "  $($_.Name): $($_.Count)" }

Write-Host "`nReport rows for ACC-CONC (expect a single row, total_qty = 100):" -ForegroundColor Yellow
$report = Invoke-RestMethod -Uri "$BaseUrl/api/reports/trades?from=2025-01-15&to=2025-01-15"
$report.rows | Where-Object { $_.account -eq "ACC-CONC" } | Format-Table -AutoSize
