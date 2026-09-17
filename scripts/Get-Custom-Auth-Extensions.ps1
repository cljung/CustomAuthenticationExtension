################################################################################################################################
# Variables
################################################################################################################################
# run: . .\.env.ps1   or  . .\env.Development.ps1  to load Variables

Connect-MgGraph -TenantId $tenantId -NoWelcome -Scopes "CustomAuthenticationExtension.Read.All, EventListener.ReadWrite.All" 

function Write-Section( $message ) {
    write-Host "################################################################################################################################" -ForegroundColor DarkGray
    write-Host "# $message" -ForegroundColor DarkGray
    write-Host "################################################################################################################################" -ForegroundColor DarkGray
}

################################################################################################################################
# List event listeners and extensions
################################################################################################################################
$url = "https://graph.microsoft.com/v1.0/identity/customAuthenticationExtensions"
Write-Section "Custom Authentication Extensions - $url"
$authExt = Invoke-MgGraphRequest -Method GET -Uri $url
$authExt.value | ConvertTo-json -Depth 10

$url = "https://graph.microsoft.com/v1.0/identity/authenticationEventListeners"
Write-Section "Authentication Event Listeners - $url"
$evtListeners = Invoke-MgGraphRequest -Method GET -Uri $url
$evtListeners.value | ConvertTo-json -Depth 10
