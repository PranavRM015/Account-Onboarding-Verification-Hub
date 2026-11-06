<#
.SYNOPSIS
  Creates the PRODUCTION Azure environment: a new resource group with the same resources as dev, named the same
  except that "dev" becomes "prod". Then it hands over to deploy-azure.ps1 to create the apps.

.DESCRIPTION
  Safe by default: WITHOUT -Apply it only prints the plan and checks that the globally unique names are free.
  Nothing is created until you run it again with -Apply.

  Created, in this order (each step checks first and skips what already exists, so a re-run is safe):
    1. Resource group                   rg-verificationhub-prod-cus-01
    2. Log Analytics workspace          workspace-rgverificationhubprodcus01
    3. Container registry               acrverificationhubprod
    4. SQL server (Entra-only auth)     srv-verificationhub-prod-cus
    5. Three serverless databases       db-verificationhub-{identity,intake,review}-prod-cus
    6. Container Apps environment       cae-verificationhub-prod-cus
    7. The five images, copied (not rebuilt) from the dev registry into the prod registry, tagged "prod"
    8. .env.prod with NEW secrets for prod (signing key, internal API key, tax ID pepper) - never printed
  Then deploy-azure.ps1 creates the managed identity (id-hub-prod), database users, the Review procedures and the
  five Container Apps (ca-hub-identity, ca-hub-risk, ca-hub-review, ca-hub-intake, ca-hub-web).

  Prerequisites: Azure CLI logged in (az login) with rights to create resources and role assignments in the
  subscription; openssl (Git for Windows ships one) to generate the signing key.

.EXAMPLE
  .\deploy\create-prod-infra.ps1             # show the plan, change nothing
  .\deploy\create-prod-infra.ps1 -Apply      # create the infrastructure
  .\deploy\create-prod-infra.ps1 -Apply -Deploy   # ...and also run deploy-azure.ps1 for prod straight afterwards
#>
[CmdletBinding()]
param(
  [string]$SubscriptionId   = '968d8a5a-affc-47df-a5a5-719b5bda4d5b',   # same subscription as dev
  [string]$Location         = 'centralus',
  [string]$ResourceGroup    = 'rg-verificationhub-prod-cus-01',
  [string]$Workspace        = 'workspace-rgverificationhubprodcus01',
  [string]$AcrName          = 'acrverificationhubprod',
  [string]$AcrSku           = 'Basic',
  [string]$SqlServer        = 'srv-verificationhub-prod-cus',
  [string]$IdentityDb       = 'db-verificationhub-identity-prod-cus',
  [string]$IntakeDb         = 'db-verificationhub-intake-prod-cus',
  [string]$ReviewDb         = 'db-verificationhub-review-prod-cus',
  [string]$EnvironmentName  = 'cae-verificationhub-prod-cus',
  [string]$ManagedIdentity  = 'id-hub-prod',
  [string]$DevAcrName       = 'aovhacrprm',                              # where the tested images come from
  [string]$DevImageTag      = 'dev',
  [string]$ProdImageTag     = 'prod',
  [string]$BackupRedundancy = 'Local',                                   # same as dev; consider Zone or Geo for real production
  [string]$EnvFile          = (Join-Path $PSScriptRoot '..\.env.prod'),
  [switch]$Apply,
  [switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$Images = 'identity', 'risk', 'review', 'intake', 'web'
$Dbs    = $IdentityDb, $IntakeDb, $ReviewDb

function Step($msg) { Write-Host "`n== $msg" -ForegroundColor Cyan }
function Info($msg) { Write-Host "   $msg" }
function Skip($msg) { Write-Host "   [skip] $msg" -ForegroundColor DarkGray }
function Done($msg) { Write-Host "   [ok]   $msg" -ForegroundColor Green }
function Plan($msg) { Write-Host "   [plan] $msg" -ForegroundColor Yellow }

# Runs az, throws on a non-zero exit code, returns stdout.
function Invoke-Az {
  $out = & az @args --only-show-errors
  if ($LASTEXITCODE -ne 0) { throw "az $($args[0]) $($args[1]) failed (exit $LASTEXITCODE)." }
  $out
}

# True when the az command succeeds (used to ask "does it exist?").
function Test-Az {
  $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
  $null = & az @args --only-show-errors 2>$null
  $ok = ($LASTEXITCODE -eq 0)
  $ErrorActionPreference = $old
  $ok
}

$tags = @('environment=prod', 'developer=Pranav', "date=$((Get-Date).ToString('MM-dd-yy'))")

# ---------- 0. preflight ----------------------------------------------------
Step '0. Preflight'
Invoke-Az account set --subscription $SubscriptionId | Out-Null
$me = Invoke-Az ad signed-in-user show --query '{id:id,upn:userPrincipalName}' -o json | ConvertFrom-Json
Info "subscription $SubscriptionId, signed in as $($me.upn)"
if ($Apply) { Info 'MODE: -Apply, resources WILL be created.' } else { Plan 'MODE: plan only, nothing will be created. Add -Apply to create.' }

# Names that must be unique across all of Azure.
$acrFree = (Invoke-Az acr check-name --name $AcrName --query nameAvailable -o tsv) -eq 'true'
$acrExists = Test-Az acr show --name $AcrName
if ($acrExists) { Skip "registry $AcrName already exists" } elseif ($acrFree) { Info "registry name $AcrName is free" } else { throw "The registry name '$AcrName' is taken by someone else. Pass a different -AcrName." }

$sqlExists = Test-Az sql server show -g $ResourceGroup -n $SqlServer
if ($sqlExists) { Skip "SQL server $SqlServer already exists" }
else {
  $body = (@{ name = $SqlServer; type = 'Microsoft.Sql/servers' } | ConvertTo-Json -Compress)
  $check = Invoke-Az rest --method post --uri "https://management.azure.com/subscriptions/$SubscriptionId/providers/Microsoft.Sql/checkNameAvailability?api-version=2021-11-01" --body $body -o json | ConvertFrom-Json
  if (-not $check.available) { throw "The SQL server name '$SqlServer' is not available ($($check.message)). Pass a different -SqlServer." }
  Info "SQL server name $SqlServer is free"
}

# Source images must exist in the dev registry before anything is created.
$devRegistryId = Invoke-Az acr show --name $DevAcrName --query id -o tsv
foreach ($i in $Images) {
  $tags_ = @(Invoke-Az acr repository show-tags --name $DevAcrName --repository "hub/$i" -o tsv)
  if ($tags_ -notcontains $DevImageTag) { throw "hub/${i}:$DevImageTag is not in $DevAcrName, so there is nothing to promote." }
}
Info "all five source images (hub/*:$DevImageTag) are in $DevAcrName"

if (-not $Apply) {
  Step 'Plan (nothing created)'
  Plan "1. resource group        $ResourceGroup ($Location)"
  Plan "2. Log Analytics         $Workspace"
  Plan "3. container registry    $AcrName ($AcrSku)"
  Plan "4. SQL server            $SqlServer (Entra-only sign-in, admin $($me.upn))"
  foreach ($d in $Dbs) { Plan "5. database              $d (serverless GP_S_Gen5_1, 0.5-1 vCore, auto-pause off, $BackupRedundancy backup)" }
  Plan "6. Container Apps env    $EnvironmentName"
  foreach ($i in $Images) { Plan "7. image copy             hub/${i}:$DevImageTag ($DevAcrName) -> hub/${i}:$ProdImageTag ($AcrName)" }
  Plan "8. secrets file          $EnvFile (new signing key, internal API key, tax ID pepper)"
  Plan "then deploy-azure.ps1:   $ManagedIdentity, database users, Review procedures, ca-hub-identity / risk / review / intake / web"
  Write-Host "`nRe-run with -Apply to create it." -ForegroundColor Yellow
  return
}

# ---------- 1. resource group -----------------------------------------------
Step "1. Resource group $ResourceGroup"
if ((Invoke-Az group exists --name $ResourceGroup) -eq 'true') { Skip 'already exists' }
else { Invoke-Az group create --name $ResourceGroup --location $Location --tags @tags | Out-Null; Done 'created' }

# ---------- 2. Log Analytics workspace -------------------------------------
Step "2. Log Analytics workspace $Workspace"
if (Test-Az monitor log-analytics workspace show -g $ResourceGroup -n $Workspace) { Skip 'already exists' }
else { Invoke-Az monitor log-analytics workspace create -g $ResourceGroup -n $Workspace -l $Location --tags @tags | Out-Null; Done 'created' }

# ---------- 3. container registry ------------------------------------------
Step "3. Container registry $AcrName"
if ($acrExists) { Skip 'already exists' }
else { Invoke-Az acr create -g $ResourceGroup -n $AcrName -l $Location --sku $AcrSku --admin-enabled false --tags @tags | Out-Null; Done 'created (admin user off; apps pull with the managed identity)' }

# ---------- 4. SQL server ---------------------------------------------------
Step "4. SQL server $SqlServer"
if ($sqlExists) { Skip 'already exists' }
else {
  Invoke-Az sql server create -g $ResourceGroup -n $SqlServer -l $Location `
    --enable-ad-only-auth --external-admin-principal-type User --external-admin-name $me.upn --external-admin-sid $me.id `
    --minimal-tls-version 1.2 --tags @tags | Out-Null
  Done "created, Entra admin $($me.upn), no SQL password exists"
}

# ---------- 5. databases ----------------------------------------------------
Step '5. Databases'
foreach ($d in $Dbs) {
  if (Test-Az sql db show -g $ResourceGroup -s $SqlServer -n $d) { Skip "$d already exists"; continue }
  Invoke-Az sql db create -g $ResourceGroup -s $SqlServer -n $d `
    --edition GeneralPurpose --family Gen5 --capacity 1 --compute-model Serverless --min-capacity 0.5 `
    --auto-pause-delay -1 --backup-storage-redundancy $BackupRedundancy --zone-redundant false --tags @tags | Out-Null
  Done "$d created (serverless, auto-pause off)"
}

# ---------- 6. Container Apps environment ----------------------------------
Step "6. Container Apps environment $EnvironmentName"
if (Test-Az containerapp env show -g $ResourceGroup -n $EnvironmentName) { Skip 'already exists' }
else {
  $wsId  = Invoke-Az monitor log-analytics workspace show -g $ResourceGroup -n $Workspace --query customerId -o tsv
  $wsKey = Invoke-Az monitor log-analytics workspace get-shared-keys -g $ResourceGroup -n $Workspace --query primarySharedKey -o tsv
  Invoke-Az containerapp env create -g $ResourceGroup -n $EnvironmentName -l $Location --logs-workspace-id $wsId --logs-workspace-key $wsKey --tags @tags | Out-Null
  Done 'created'
}

# ---------- 7. promote the tested images -----------------------------------
Step "7. Copy images from $DevAcrName to $AcrName (no rebuild)"
$have = @(Invoke-Az acr repository list --name $AcrName -o tsv)
foreach ($i in $Images) {
  $already = $false
  if ($have -contains "hub/$i") { $already = (@(Invoke-Az acr repository show-tags --name $AcrName --repository "hub/$i" -o tsv) -contains $ProdImageTag) }
  if ($already) { Skip "hub/${i}:$ProdImageTag already there"; continue }
  Invoke-Az acr import --name $AcrName --source "hub/${i}:$DevImageTag" --registry $devRegistryId --image "hub/${i}:$ProdImageTag" --force | Out-Null
  Done "hub/${i}:$ProdImageTag"
}

# ---------- 8. secrets for prod --------------------------------------------
Step "8. Secrets file $EnvFile"
if (Test-Path $EnvFile) { Skip 'already exists (not overwritten)' }
else {
  $openssl = (Get-Command openssl -ErrorAction SilentlyContinue).Source
  if (-not $openssl) { $openssl = @('C:\Program Files\Git\usr\bin\openssl.exe', 'C:\Program Files (x86)\Git\usr\bin\openssl.exe') | Where-Object { Test-Path $_ } | Select-Object -First 1 }
  if (-not $openssl) { throw 'openssl was not found. Install Git for Windows (it includes one) or add openssl to PATH, then re-run.' }
  $rng = New-Object Security.Cryptography.RNGCryptoServiceProvider
  function New-Secret($bytes) { $b = New-Object byte[] $bytes; $rng.GetBytes($b); ($b | ForEach-Object { $_.ToString('x2') }) -join '' }
  $pem = (& $openssl genrsa 2048 2>$null | & $openssl pkcs8 -topk8 -nocrypt) -join "`n"
  if ($pem -notmatch 'BEGIN PRIVATE KEY') { throw 'Could not generate the signing key with openssl.' }
  $lines = @(
    '# PRODUCTION secrets. Generated by create-prod-infra.ps1. Never commit this file or share it.',
    "INTERNAL_API_KEY=$(New-Secret 32)",
    "TAX_ID_PEPPER=$(New-Secret 24)",
    ('JWT_PRIVATE_KEY_PEM="' + ($pem -replace "`r?`n", '\n') + '"')
  )
  [IO.File]::WriteAllText($EnvFile, (($lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))
  Done 'created with new prod-only secrets (not printed). Back this file up somewhere safe.'
}

# ---------- hand over to the app deployment --------------------------------
$deployScript = Join-Path $PSScriptRoot 'deploy-azure.ps1'
$deployArgs = @{
  SubscriptionId = $SubscriptionId; ResourceGroup = $ResourceGroup; Location = $Location; AcrName = $AcrName
  ImageTag = $ProdImageTag; EnvironmentName = $EnvironmentName; SqlServer = $SqlServer
  IdentityDb = $IdentityDb; IntakeDb = $IntakeDb; ReviewDb = $ReviewDb; ManagedIdentity = $ManagedIdentity; EnvFile = $EnvFile
}
if ($Deploy) {
  Step 'Running deploy-azure.ps1 for prod'
  & $deployScript @deployArgs
} else {
  Step 'Infrastructure is ready. Create the apps with:'
  Write-Host "   .\deploy\deploy-azure.ps1 -SubscriptionId $SubscriptionId -ResourceGroup $ResourceGroup -AcrName $AcrName -ImageTag $ProdImageTag ``"
  Write-Host "       -EnvironmentName $EnvironmentName -SqlServer $SqlServer -IdentityDb $IdentityDb -IntakeDb $IntakeDb -ReviewDb $ReviewDb ``"
  Write-Host "       -ManagedIdentity $ManagedIdentity -EnvFile $EnvFile"
}
