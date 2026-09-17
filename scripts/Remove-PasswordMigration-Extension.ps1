################################################################################################################################
# Variables
################################################################################################################################
# run: . .\.env.ps1   or  . .\env.Development.ps1  to load Variables

Connect-MgGraph -TenantId $tenantId -NoWelcome -Scopes "CustomAuthenticationExtension.ReadWrite.All" 

################################################################################################################################
# remove event listeners and extensions
################################################################################################################################
$evtListeners = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/identity/authenticationEventListeners"
foreach( $pwl in ($evtListeners.value | where {$_."@odata.type" -eq "#microsoft.graph.onPasswordSubmitListener"}) ) {
    $pwl.id
    Invoke-MgGraphRequest -Method DELETE -Uri "https://graph.microsoft.com/v1.0/identity/authenticationEventListeners/$($pwl.id)"
}

$authExt = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/identity/customAuthenticationExtensions"
foreach( $ext in ($authExt.value | where {$_.displayName -eq "OnPasswordSubmitCustomExtension"}) ) {
    $ext.id
    Invoke-MgGraphRequest -Method DELETE -Uri "https://graph.microsoft.com/v1.0/identity/customAuthenticationExtensions/$($ext.id)"
}

