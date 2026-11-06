<#
.SYNOPSIS
  Deploys the four Verification Hub services (identity, risk, review, intake) to Azure Container Apps (dev).

.DESCRIPTION
  Idempotent: safe to re-run. Every step checks first and skips what already exists.
    1. Checks the five images (identity, risk, review, intake, web) exist in the registry.
    2. Finds the (single) Container Apps environment.
    3. Creates one user-assigned managed identity and grants it AcrPull on the registry.
    4. SQL access: opens the firewall for this machine, then for each database checks whether the
       identity is already a user with the needed roles and only creates/grants what is missing.
    5. Creates (or updates) the four Container Apps in order: identity, risk, review, intake.

  Secrets are read from the repo-root .env (never printed) and stored as Container App secrets.

  Prerequisites: Azure CLI logged in (az login) as the SQL server's Microsoft Entra admin.

.EXAMPLE
  .\deploy\deploy-azure.ps1
  .\deploy\deploy-azure.ps1 -RotateInternalKey      # generate a fresh Internal API key for Azure
  .\deploy\deploy-azure.ps1 -SkipSql                # skip the database-access step
#>
[CmdletBinding()]
param(
  [string]$SubscriptionId    = '968d8a5a-affc-47df-a5a5-719b5bda4d5b',
  [string]$ResourceGroup     = 'rg-verificationhub-dev-cus-01',
  [string]$Location          = 'centralus',
  [string]$AcrName           = 'aovhacrprm',
  [string]$ImageTag          = 'dev',
  [string]$EnvironmentName   = '',            # leave empty to auto-detect the single environment
  [string]$EnvironmentResourceGroup = '',     # set when the environment lives in another resource group (prod shares dev's)
  [string]$SqlServer         = 'srv-verificationhub-dev-cus',   # short name, no .database.windows.net
  [string]$SqlResourceGroup  = '',            # defaults to $ResourceGroup
  [string]$IdentityDb        = 'db-verificationhub-identity-dev-cus',
  [string]$IntakeDb          = 'db-verificationhub-intake-dev-cus',
  [string]$ReviewDb          = 'db-verificationhub-review-dev-cus',
  [string]$ManagedIdentity   = 'id-hub-dev',
  [string]$AppSuffix         = '',            # appended to every Container App name, e.g. '-prod-eus2'
  [string]$CorsOrigin        = 'http://localhost:4200',
  [string]$EnvFile           = (Join-Path $PSScriptRoot '..\.env'),
  [switch]$RotateInternalKey,
  [switch]$SkipSql
)

$ErrorActionPreference = 'Stop'
if (-not $SqlResourceGroup) { $SqlResourceGroup = $ResourceGroup }

$AppNames = @{
  identity = "ca-hub-identity$AppSuffix"
  risk     = "ca-hub-risk$AppSuffix"
  review   = "ca-hub-review$AppSuffix"
  intake   = "ca-hub-intake$AppSuffix"
  web      = "ca-hub-web$AppSuffix"
}
$AcrServer = "$AcrName.azurecr.io"
$SqlFqdn   = "$SqlServer.database.windows.net"

# ---------- helpers ---------------------------------------------------------
function Step($msg)  { Write-Host "`n== $msg" -ForegroundColor Cyan }
function Info($msg)  { Write-Host "   $msg" }
function Skip($msg)  { Write-Host "   [skip] $msg" -ForegroundColor DarkGray }
function Done($msg)  { Write-Host "   [ok]   $msg" -ForegroundColor Green }

# Runs az, throws on a non-zero exit code, returns stdout. Never echoes the arguments (they can hold secrets).
function Invoke-Az {
  # Windows PowerShell 5.1 turns any stderr text (progress, warnings) into a terminating error under 'Stop'; the exit code is what counts.
  $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
  $errFile = [IO.Path]::GetTempFileName()
  try {
    $out = & az @args --only-show-errors 2>$errFile
    $code = $LASTEXITCODE; $ErrorActionPreference = $old
    if ($code -ne 0) { throw "az $($args[0]) $($args[1]) failed (exit $code). $((Get-Content $errFile -Raw))" }
  } finally { $ErrorActionPreference = $old; Remove-Item $errFile -ErrorAction SilentlyContinue }
  $out
}

function Read-DotEnv($path) {
  if (-not (Test-Path $path)) { throw "Env file not found: $path" }
  $map = @{}
  $lines = @(Get-Content $path)
  for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match '^\s*#' -or $line -notmatch '=') { continue }
    $k, $v = $line.Split('=', 2)
    $v = $v.Trim()
    # a quoted value may span several lines (e.g. a PEM key): keep reading until the closing quote
    if ($v.StartsWith('"') -and ($v.Length -eq 1 -or -not $v.EndsWith('"'))) {
      while ($i + 1 -lt $lines.Count) { $i++; $v += "`n" + $lines[$i].Trim(); if ($lines[$i].Trim().EndsWith('"')) { break } }
    }
    if ($v.Length -ge 2 -and $v.StartsWith('"') -and $v.EndsWith('"')) { $v = $v.Substring(1, $v.Length - 2) }
    $map[$k.Trim()] = $v
  }
  $map
}

# Rebuilds a PEM as a single line ("-----BEGIN X----- <base64> -----END X-----") whatever way it was stored
# (real newlines, literal backslash-n, or spaces). .NET ImportFromPem accepts this, and az CLI passes it as one argument.
function ConvertTo-SingleLinePem($raw) {
  $m = [regex]::Match($raw, '-----BEGIN ([A-Z ]+)-----(.*?)-----END \1-----', 'Singleline')
  if (-not $m.Success) { throw 'JWT_PRIVATE_KEY_PEM is not a PEM (no BEGIN/END markers found).' }
  $body = $m.Groups[2].Value.Replace('\n', '') -replace '\s', ''
  "-----BEGIN $($m.Groups[1].Value)----- $body -----END $($m.Groups[1].Value)-----"
}

function Invoke-SqlScalar($db, $token, $sql) {
  $cs = "Server=tcp:$SqlFqdn,1433;Database=$db;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;"
  $conn = New-Object System.Data.SqlClient.SqlConnection($cs)
  $conn.AccessToken = $token
  try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 120
    $cmd.ExecuteScalar()
  } finally { $conn.Dispose() }
}

# Create when missing, otherwise update. $Secrets / $EnvVars are arrays of 'name=value' strings.
function Deploy-App($name, $image, $ingress, $secrets, $envVars, $port = '8080') {
  $exists = (Invoke-Az containerapp list -g $ResourceGroup --query "[?name=='$name'].name" -o tsv)
  if (-not $exists) {
    Info "creating $name ..."
    $a = @('containerapp','create','-n',$name,'-g',$ResourceGroup,'--environment',$script:EnvName,
           '--image',"$AcrServer/hub/${image}:$ImageTag",
           '--target-port',$port,'--ingress',$ingress,
           '--min-replicas','1','--max-replicas','1',
           '--user-assigned',$script:IdentityId,
           '--registry-server',$AcrServer,'--registry-identity',$script:IdentityId)
    if ($secrets.Count) { $a += '--secrets'; $a += $secrets }
    if ($envVars.Count) { $a += '--env-vars'; $a += $envVars }
    Invoke-Az @a | Out-Null
  } else {
    Info "$name exists - updating secrets, env vars and image ..."
    if ($secrets.Count) { Invoke-Az containerapp secret set -n $name -g $ResourceGroup --secrets @secrets | Out-Null }
    $a = @('containerapp','update','-n',$name,'-g',$ResourceGroup,'--image',"$AcrServer/hub/${image}:$ImageTag")
    if ($envVars.Count) { $a += '--set-env-vars'; $a += $envVars }
    Invoke-Az @a | Out-Null
  }
  # let apps call each other over plain http://<app-name> inside the environment
  Invoke-Az containerapp ingress update -n $name -g $ResourceGroup --allow-insecure true | Out-Null
  Done "$name deployed"
}

# ---------- 0. preflight ----------------------------------------------------
Step '0. Preflight'
Invoke-Az account set --subscription $SubscriptionId | Out-Null
$me = (Invoke-Az account show --query user.name -o tsv)
Info "subscription $SubscriptionId, signed in as $me"

$envMap = Read-DotEnv $EnvFile
foreach ($req in 'INTERNAL_API_KEY','JWT_PRIVATE_KEY_PEM','TAX_ID_PEPPER') {
  if (-not $envMap[$req]) { throw "$req is missing from $EnvFile" }
}
$internalKey = $envMap['INTERNAL_API_KEY']
if ($RotateInternalKey) {
  $internalKey = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
  Info 'generated a new Internal API key for Azure (used for identity AND intake)'
}
if ($internalKey.Length -lt 32) { throw 'INTERNAL_API_KEY must be at least 32 characters.' }

# ---------- 1. images -------------------------------------------------------
Step '1. Images in the registry'
$repos = @(Invoke-Az acr repository list --name $AcrName -o tsv)
foreach ($r in 'hub/identity','hub/risk','hub/review','hub/intake','hub/web') {
  if ($repos -notcontains $r) { throw "Image repository '$r' not found in $AcrName. Push it first." }
  $tags = @(Invoke-Az acr repository show-tags --name $AcrName --repository $r -o tsv)
  if ($tags -notcontains $ImageTag) { throw "Tag '$ImageTag' not found on $r." }
}
Done "all four images with tag '$ImageTag' present"

# ---------- 2. environment --------------------------------------------------
Step '2. Container Apps environment'
if ($EnvironmentName -and $EnvironmentResourceGroup) { $script:EnvName = (Invoke-Az containerapp env show -g $EnvironmentResourceGroup -n $EnvironmentName --query id -o tsv) }  # full resource id, so it works across resource groups
elseif ($EnvironmentName) { $script:EnvName = $EnvironmentName }
else {
  # Only this resource group's environments, so dev and prod can exist side by side in one subscription.
  $envs = @(Invoke-Az containerapp env list -g $ResourceGroup --query '[].name' -o tsv)
  if ($envs.Count -ne 1) { throw "Expected exactly 1 environment in $ResourceGroup, found $($envs.Count). Pass -EnvironmentName." }
  $script:EnvName = $envs[0]
}
Done "using environment '$(Split-Path $script:EnvName -Leaf)'"

# ---------- 3. managed identity + AcrPull ----------------------------------
Step '3. Managed identity and registry pull access'
$have = (Invoke-Az identity list -g $ResourceGroup --query "[?name=='$ManagedIdentity'].name" -o tsv)
if ($have) { Skip "identity '$ManagedIdentity' already exists" }
else { Invoke-Az identity create -n $ManagedIdentity -g $ResourceGroup -l $Location | Out-Null; Done "created identity '$ManagedIdentity'" }

$script:IdentityId = (Invoke-Az identity show -n $ManagedIdentity -g $ResourceGroup --query id -o tsv)
$principalId       = (Invoke-Az identity show -n $ManagedIdentity -g $ResourceGroup --query principalId -o tsv)
$clientId          = (Invoke-Az identity show -n $ManagedIdentity -g $ResourceGroup --query clientId -o tsv)
$acrId             = (Invoke-Az acr show -n $AcrName --query id -o tsv)

$pull = (Invoke-Az role assignment list --assignee $principalId --scope $acrId --query "[?roleDefinitionName=='AcrPull'] | length(@)" -o tsv)
if ([int]$pull -ge 1) { Skip 'AcrPull already assigned' }
else {
  Invoke-Az role assignment create --assignee-object-id $principalId --assignee-principal-type ServicePrincipal --role AcrPull --scope $acrId | Out-Null
  Done 'granted AcrPull on the registry (waiting 30s for propagation)'
  Start-Sleep -Seconds 30
}

# ---------- 4. SQL access ---------------------------------------------------
if ($SkipSql) { Step '4. SQL access'; Skip '-SkipSql given' }
else {
  Step '4. SQL access'

  # firewall: this machine + "allow Azure services"
  $myIp = (Invoke-RestMethod -Uri 'https://api.ipify.org').Trim()
  $rules = @(Invoke-Az sql server firewall-rule list -g $SqlResourceGroup -s $SqlServer --query '[].{n:name,s:startIpAddress,e:endIpAddress}' -o json | ConvertFrom-Json)
  if ($rules | Where-Object { $_.s -eq $myIp -and $_.e -eq $myIp }) { Skip "firewall already allows $myIp" }
  else { Invoke-Az sql server firewall-rule create -g $SqlResourceGroup -s $SqlServer -n "deploy-$($env:USERNAME)" --start-ip-address $myIp --end-ip-address $myIp | Out-Null; Done "firewall rule added for $myIp" }
  if ($rules | Where-Object { $_.s -eq '0.0.0.0' -and $_.e -eq '0.0.0.0' }) { Skip 'Azure services already allowed' }
  else { Invoke-Az sql server firewall-rule create -g $SqlResourceGroup -s $SqlServer -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 | Out-Null; Done 'allowed Azure services' }

  $token = (Invoke-Az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv)
  $sidHex = '0x' + ((([Guid]$clientId).ToByteArray() | ForEach-Object { $_.ToString('X2') }) -join '')
  $roles = 'db_datareader','db_datawriter','db_ddladmin'

  foreach ($db in $IdentityDb, $IntakeDb, $ReviewDb) {
    Info "checking $db (a paused serverless database can take about a minute to wake) ..."
    $userExists = [int](Invoke-SqlScalar $db $token "select count(*) from sys.database_principals where name = N'$ManagedIdentity'")
    $roleList   = ($roles | ForEach-Object { "N'$_'" }) -join ','
    $haveRoles  = [int](Invoke-SqlScalar $db $token ("select count(*) from sys.database_role_members rm " +
        "join sys.database_principals r on r.principal_id = rm.role_principal_id " +
        "join sys.database_principals m on m.principal_id = rm.member_principal_id " +
        "where m.name = N'$ManagedIdentity' and r.name in ($roleList)"))

    if ($userExists -eq 1 -and $haveRoles -eq $roles.Count) { Skip "$db : user and all $($roles.Count) roles already in place"; continue }

    if ($userExists -eq 0) {
      Invoke-SqlScalar $db $token "create user [$ManagedIdentity] with sid = $sidHex, type = E;" | Out-Null
      Done "$db : created user $ManagedIdentity"
    }
    foreach ($role in $roles) {
      $in = [int](Invoke-SqlScalar $db $token ("select count(*) from sys.database_role_members rm " +
          "join sys.database_principals r on r.principal_id = rm.role_principal_id " +
          "join sys.database_principals m on m.principal_id = rm.member_principal_id " +
          "where m.name = N'$ManagedIdentity' and r.name = N'$role'"))
      if ($in -eq 0) { Invoke-SqlScalar $db $token "alter role [$role] add member [$ManagedIdentity];" | Out-Null; Done "$db : added to $role" }
    }
  }

  # Review's 60-day ticket expiry runs as stored procedures. Push them into the Review database (CREATE OR ALTER, so
  # re-runs are safe; the file is split on its GO lines because each CREATE must be alone in its batch). The built-in
  # roles above do not include EXECUTE, so grant it too - without it the expiry job fails with SQL error 229.
  Info "$ReviewDb : applying services/review/database/Procedures.sql"
  $proceduresFile = Join-Path $PSScriptRoot '..\services\review\database\Procedures.sql'
  if (-not (Test-Path $proceduresFile)) { throw "Not found: $proceduresFile" }
  $batches = [regex]::Split((Get-Content $proceduresFile -Raw), '(?im)^\s*GO\s*$') | Where-Object { $_.Trim() }
  foreach ($batch in $batches) { Invoke-SqlScalar $ReviewDb $token $batch | Out-Null }
  Invoke-SqlScalar $ReviewDb $token "grant execute on object::dbo.pr_expireReviewTickets to [$ManagedIdentity];" | Out-Null
  Done "$ReviewDb : procedures created/updated, EXECUTE granted on dbo.pr_expireReviewTickets"
}

# ---------- 5. the four apps ------------------------------------------------
function ConnStr($db) {
  "Server=tcp:$SqlFqdn,1433;Database=$db;Authentication=Active Directory Managed Identity;User Id=$clientId;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"
}
$identityUrl = "http://$($AppNames.identity)"
$riskUrl     = "http://$($AppNames.risk)"
$reviewUrl   = "http://$($AppNames.review)"
$jwks        = "$identityUrl/.well-known/jwks.json"

Step '5a. Identity'
Deploy-App $AppNames.identity 'identity' 'external' @(
    "sql-conn=$(ConnStr $IdentityDb)",
    "jwt-pem=$(ConvertTo-SingleLinePem $envMap['JWT_PRIVATE_KEY_PEM'])",
    "internal-key=$internalKey"
  ) @(
    'ConnectionStrings__IdentityConnection=secretref:sql-conn',
    'Jwt__PrivateKeyPem=secretref:jwt-pem',
    'Internal__ApiKey=secretref:internal-key',
    "Cors__AllowedOrigins__0=$CorsOrigin",
    'Database__MigrateOnStartup=true'
  )

Step '5b. Risk'
Deploy-App $AppNames.risk 'risk' 'internal' @() @(
    "Identity__JwksUrl=$jwks",
    'Identity__RequireHttps=false'
  )

Step '5c. Review'
Deploy-App $AppNames.review 'review' 'external' @(
    "sql-conn=$(ConnStr $ReviewDb)"
  ) @(
    'ConnectionStrings__ReviewConnection=secretref:sql-conn',
    "Identity__JwksUrl=$jwks",
    'Identity__RequireHttps=false',
    'Database__MigrateOnStartup=true'
  )

Step '5d. Intake'
Deploy-App $AppNames.intake 'intake' 'external' @(
    "sql-conn=$(ConnStr $IntakeDb)",
    "internal-key=$internalKey",
    "tax-pepper=$($envMap['TAX_ID_PEPPER'])"
  ) @(
    'ConnectionStrings__DefaultConnection=secretref:sql-conn',
    'Internal__ApiKey=secretref:internal-key',
    'TaxIdHashPepper=secretref:tax-pepper',
    "Identity__BaseUrl=$identityUrl",
    "Risk__BaseUrl=$riskUrl",
    "Review__BaseUrl=$reviewUrl",
    "Cors__AllowedOrigins__0=$CorsOrigin",
    'Database__EnsureCreatedOnStartup=true'
  )

Step '5e. Web (Angular + nginx, routes /api to the services above)'
$apiOrigin = { param($n) "https://$(Invoke-Az containerapp show -n $n -g $ResourceGroup --query properties.configuration.ingress.fqdn -o tsv)" }
Deploy-App $AppNames.web 'web' 'external' @() @(
    "IDENTITY_UPSTREAM=$(& $apiOrigin $AppNames.identity)",
    "INTAKE_UPSTREAM=$(& $apiOrigin $AppNames.intake)",
    "REVIEW_UPSTREAM=$(& $apiOrigin $AppNames.review)",
    'DNS_RESOLVER=168.63.129.16'
  ) '80'

# ---------- 6. summary ------------------------------------------------------
Step '6. Result'
foreach ($k in 'identity','risk','review','intake','web') {
  $n = $AppNames[$k]
  $fqdn = (Invoke-Az containerapp show -n $n -g $ResourceGroup --query properties.configuration.ingress.fqdn -o tsv)
  $state = (Invoke-Az containerapp show -n $n -g $ResourceGroup --query properties.runningStatus.state -o tsv 2>$null)
  Write-Host ("   {0,-16} https://{1}   [{2}]" -f $n, $fqdn, $state)
}
Write-Host "`nSmoke test:  curl https://<identity-fqdn>/.well-known/jwks.json"
if ($RotateInternalKey) { Write-Host 'Note: a new Internal API key was used; it is stored only as a Container App secret.' -ForegroundColor Yellow }
