################################################################################################################################
# Variables
################################################################################################################################
# run: . .\.env.ps1   or  . .\env.Development.ps1  to load Variables

Connect-MgGraph -TenantId $tenantId -NoWelcome -Scopes "CustomAuthenticationExtension.Read.All, Application.Read.All, EventListener.Read.All" 

$clientApp = Get-MgApplication -Filter "displayName eq '$clientAppName'"
if ( $null -eq $clientAPp ) {
    write-error "The client app $clientAppName is not registered. Please register that app and a user Flow first"
    exit
}
$extApp = Get-MgApplication -Filter "displayName eq '$customExtensionAppName'"

if ( $null -eq $extApp ) {
    write-error "The custom extension app $customExtensionAppName is not registered"
    exit
}

if ( $False -eq (test-Path -Path $certFullPath) ) {
    write-error "Certificate not found: $certFullPath"
    exit
}
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certFullPath)
write-host "Note that the encryption certificate expires at $($cert.NotAfter.toUniversaltime().ToString("o"))"

$currentContext = Get-MgContext
$domain = $currentContext.Account.Split("@")[1]

$cfg = @"
{
"Entra": {
    "Domain": "$domain",
    "Instance": "https://{tenantId}.ciamlogin.com/",
    "TenantId": "$($currentContext.tenantID)",
    "ClientId": "$($clientApp.AppID)",
    "ClientSecret": "",
    "AcceptedAuds": [
      "$($extApp.AppID)"
    ],
    "AcceptedSigninAppIDs": [
      "$($clientApp.AppID)"
    ]
  }
}
"@

write-host "For your appsettings.json:`n$cfg"

