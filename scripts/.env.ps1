[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)][string]$TenantId, # can be guid or foobar.onmicrosoft.com
    [Parameter(Mandatory = $true)][string]$ClientAppName = "ciam-test-app",
    [Parameter(Mandatory = $true)][string]$CustomExtensionAppName = "CIAM-custom-auth-extension-app",
    [Parameter(Mandatory = $true)][string]$ApiHostingDomain # FQDN of where you are hosting your API
)

function ImportModule($name) {
    if (-not (Get-Module -Name $name)) {
        Write-Host "Module not found in session. Importing $name..." -ForegroundColor Cyan
        Import-Module -Name $name
    } else {
        Write-Host "$nameAuthentication is already imported." -ForegroundColor Green
    }
}
ImportModule "Microsoft.Graph.Applications"
ImportModule "Microsoft.Graph.Users"
ImportModule "Microsoft.Graph.Identity.SignIns"
################################################################################################################################
# Variables
################################################################################################################################

$apiHostingDomain = $ApiHostingDomain
$clientAppName = $ClientAppName
$customExtensionAppName = $CustomExtensionAppName
$TokenIssaunceExtName = "onTokenIssaunce"

$apiEndpointPasswordMigration = "https://$apiHostingDomain/api/authenticationevent/passwordmigration"
$apiEndpointTokenIssuance = "https://$apiHostingDomain/api/authenticationevent/tokenIssuanceStart"

$certFullPath = "set full path of downloaded/JitMigrationEncryptionCert_some-number.cer"
$keyCredentialsName = "CN=JitMigration"

$Auth0TenantDomain = "...something... .auth0.com"
$Auth0ClientId     = ""
$Auth0ClientSecret = ""

$Auth0Username     = "johndoe@live.com"
$Auth0Password     = "..."
