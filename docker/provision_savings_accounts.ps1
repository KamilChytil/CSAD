# Provision savings accounts for existing users that only have a checking account.
# Run this AFTER rebuilding accounts-api and identity-api.

$ErrorActionPreference = "Stop"

Write-Host "Querying existing owners with only a checking account..."

# Get owner IDs that have at least one checking account (AccountType = 0 in JSON)
# but no savings account (AccountType = 1)
$sql = @"
SELECT DISTINCT (data->>'OwnerId')::text
FROM accounts_service.mt_doc_account
WHERE COALESCE((data->>'AccountType')::int, 0) = 0
  AND (data->>'OwnerId')::uuid NOT IN (
      SELECT (data->>'OwnerId')::uuid
      FROM accounts_service.mt_doc_account
      WHERE (data->>'AccountType')::int = 1
  );
"@

$ownerIds = docker exec fairbank-pg-primary psql -U fairbank_admin -d fairbank -t -c $sql |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -ne "" }

if (-not $ownerIds) {
    Write-Host "No users need savings accounts provisioned."
    exit 0
}

Write-Host "Found $($ownerIds.Count) user(s) to provision."

foreach ($ownerId in $ownerIds) {
    Write-Host "  Creating savings account for OwnerId = $ownerId ..."
    try {
        $body = @{ OwnerId = $ownerId; Currency = "CZK"; AccountType = 1 } | ConvertTo-Json
        $response = Invoke-WebRequest -Uri "http://localhost/api/v1/accounts" `
            -Method POST `
            -ContentType "application/json" `
            -Body $body `
            -UseBasicParsing
        Write-Host "    -> $($response.StatusCode)"
    }
    catch {
        Write-Warning "    -> Failed for $ownerId : $_"
    }
}

Write-Host "Done."
