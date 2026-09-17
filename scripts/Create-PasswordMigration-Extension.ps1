################################################################################################################################
# Variables
################################################################################################################################
# run: . .\.env.ps1   or  . .\env.Development.ps1  to load Variables

Connect-MgGraph -TenantId $tenantId -NoWelcome -Scopes "Directory.ReadWrite.All,Policy.ReadWrite.AuthenticationFlows, CustomAuthenticationExtension.ReadWrite.All, Application.ReadWrite.All, EventListener.ReadWrite.All" 

function Write-Section( $message ) {
    write-Host "################################################################################################################################" -ForegroundColor DarkGray
    write-Host "# $message" -ForegroundColor DarkGray
}

Write-Section "Check $clientAppName exists"
$clientApp = Get-MgApplication -Filter "displayName eq '$clientAppName'"
if ( $null -eq $clientAPp ) {
    write-error "The client app $clientAppName is not registered. Please register that app and a user Flow first"
    exit
}
$clientAppId = $clientApp.AppId
write-host "AppID $($clientApp.AppId)" -ForegroundColor Green
################################################################################################################################
Write-Section "Check $customExtensionAppName exists"
################################################################################################################################
$extApp = Get-MgApplication -Filter "displayName eq '$customExtensionAppName'"

if ( $null -ne $extApp ) {
    write-host "AppID $($clientApp.AppId)" -ForegroundColor Green
} else {
    write-error "The custom extension app $customExtensionAppName is not registered. run Create-CustomAuthExtension.ps1 script first"
    exit
}
$identifierUriExpected = "api://$apiHostingDomain/$($extApp.AppId)"
if ( $extApp.identifierUris.Contains($identifierUriExpected) -eq $False ) {
    write-error "The $customExtensionAppName has the wrong identifierUri. It should be '$identifierUriExpected'. Please add it in the manifest"
    exit
}
$identifierUri = $extApp.identifierUris[ $extApp.identifierUris.IndexOf($identifierUriExpected) ]
################################################################################################################################
Write-Section "Create extension attribute"
################################################################################################################################

$b2cExt = Get-MgApplication -Filter "startswith(displayName, 'b2c-extension')"
$extAttr = "extension_"+$b2cExt.appId.Replace("-","")+"_toBeMigrated"

$extProps = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/applications/$($b2cExt.id)/extensionProperties"
$prop = ($extProps.value | where {$_.name -eq $extAttr})
if ( $prop -ne $null ) {
    write-host "Extension attribute already exists '$extAttr'" -ForegroundColor Green
} else {
    write-host "Creating extension attribute '$extAttr'" -ForegroundColor Green
    $extProps = Invoke-MgGraphRequest -Method POST -Uri "https://graph.microsoft.com/v1.0/applications/$($b2cExt.id)/extensionProperties" -Body "{'name': 'toBeMigrated', 'dataType': 'Boolean', 'targetObjects':[ 'User' ] }"
}
################################################################################################################################
# In portal.azure.com
# Create a PKCS #12 RSA 2048 self signed Certificate in KeyVault . Name = JitMigrationEncryptionCert., Subject = CN=JitMigration
# Export it in CER format
################################################################################################################################
Write-Section "Load certificate"

if ( $False -eq (test-Path -Path $certFullPath) ) {
    write-error "Certificate not found: $certFullPath"
    exit
}
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certFullPath)
$certBase64 = [Convert]::ToBase64String($cert.RawData)
write-host $cert.Subject -ForegroundColor Green

################################################################################################################################
Write-Section "Update KeyCredentials"
################################################################################################################################

$keyCred = ($extApp.KeyCredentials | where {$_.DisplayName -eq $keyCredentialsName})
if ( $null -ne $keyCred ) {
    write-host "Certificate $keyCredentialsName already set as KeyCredentials for app '$customExtensionAppName'" -ForegroundColor Green
} else {
    write-host "Adding Certificate as KeyCredentials for app '$customExtensionAppName'" -ForegroundColor Green
$keyCredentials = @"
{
  "keyCredentials": [
    {
      "keyId": "$((New-Guid).Guid.ToString())",
      "endDateTime": "$($cert.NotAfter.toUniversaltime().ToString("o"))",
      "startDateTime": "$($cert.NotBefore.toUniversaltime().ToString("o"))",
      "type": "AsymmetricX509Cert",
      "usage": "Encrypt",
      "key": "$certBase64",
      "displayName": "$keyCredentialsName"
    }
  ],
  "tokenEncryptionKeyId": ""
}
"@

    $KeyCredentials = ($KeyCredentials | ConvertFrom-json)
    $KeyCredentials.tokenEncryptionKeyId = $KeyCredentials.keyCredentials[0].keyId

    $kc = Invoke-MgGraphRequest -Method PATCH -Uri "https://graph.microsoft.com/v1.0/applications/$($extApp.id)" -Body ($KeyCredentials | ConvertTo-Json -Compress)
    write-host "Updated" -ForegroundColor Green
}

################################################################################################################################
Write-Section "Define Password Migration extension"
################################################################################################################################

$authExt = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/identity/customAuthenticationExtensions"
$authExtPwdMigration = ($authExt.value | where {$_.displayName -eq "OnPasswordSubmitCustomExtension"})

$needToCreate = $true
foreach( $ext in $authExtPwdMigration ) {
    if ( $ext.endpointConfiguration.targetUrl -eq $apiEndpointPasswordMigration `
        -and $ext.authenticationConfiguration.resourceId -eq $identifierUri ) {
        write-host "Reusing existing password migration extensions ($($ext.id)) for your $identifierUri" -ForegroundColor Yellow
        $authExtPwdMigrationId = $ext.id
        $needToCreate = $False
    } else {
        write-host "WARNING: You have other existing password migration extensions for resourceId $($ext.authenticationConfiguration.resourceId), targetUrl $($ext.endpointConfiguration.targetUrl)" -ForegroundColor White
    }
}

if ( $needToCreate -eq $True ) {
$extensionParams = @"
{
  "@odata.type": "#microsoft.graph.onPasswordSubmitCustomExtension",
  "displayName": "OnPasswordSubmitCustomExtension",
  "description": "Validate password",
  "endpointConfiguration": {
    "@odata.type": "#microsoft.graph.httpRequestEndpoint",
    "targetUrl": "$apiEndpointPasswordMigration"
  },
  "authenticationConfiguration": {
    "@odata.type": "#microsoft.graph.azureAdTokenAuthentication",
    "resourceId": "$identifierUri"
  },
  "clientConfiguration": {
    "timeoutInMilliseconds": 2000,
    "maximumRetries": 1
  }
}
"@

    $authExtNew = Invoke-MgGraphRequest -Method POST -Uri "https://graph.microsoft.com/v1.0/identity/customAuthenticationExtensions" -Body $extensionParams
    $authExt = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/identity/customAuthenticationExtensions"
    $authExtPwdMigration = ($authExt.value | where {$_.displayName -eq "OnPasswordSubmitCustomExtension"})
    $authExtPwdMigrationId = $authExtPwdMigration.id
    write-host "Done. Id: $authExtPwdMigrationId" -ForegroundColor Green
}
################################################################################################################################
Write-Section "Bind the app to the event listener for the extension"
################################################################################################################################

$evtListeners = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/identity/authenticationEventListeners"
$pwdMigrListeners = ($evtListeners.value | where {$_."@odata.type" -eq "#microsoft.graph.onPasswordSubmitListener"})

# See if we have and extension with same targetURL and resourceId (identifierUri).
# If so, add our AppID to the includedApplications array if not there and update it
$needToCreate = $True
foreach( $pwl in $pwdMigrListeners ) {
    if ( $pwl.handler.customExtension.endpointConfiguration.targetUrl -eq $apiEndpointPasswordMigration `
            -and $pwl.handler.customExtension.authenticationConfiguration.resourceId -eq $identifierUri ) {
        $needToCreate = $False
        if ( $pwl.conditions.applications.includeApplications.appID -contains $clientApp.appId -eq $True ) {
            write-host "AppID $($clientApp.appId) already exists as in migration event listener ($($pwl.id)). Nothing to do" -ForegroundColor Yellow
        } else {
            write-host "Reusing existing password migration event listener ($($pwl.id)) for your $identifierUri" -ForegroundColor Yellow
            $pwl.conditions.applications.includeApplications += @{appId = $clientApp.appId }
            $pwl | convertTo-Json -Depth 10
            $evtL = Invoke-MgGraphRequest -Method PATCH -Uri "https://graph.microsoft.com/v1.0/identity/authenticationEventListeners" -Body ($pwl | ConvertTo-json -depth 10 -compress)
        }
        break
    }
}

if ( $needToCreate -eq $True ) {
$evtListener = @"
{  
    "@odata.type": "#microsoft.graph.onPasswordSubmitListener",  
    "conditions": {  
        "applications": {  
            "includeAllApplications": false,  
            "includeApplications": [  
                {  
                    "appId": "$($clientApp.appId)"  
                }  
            ]  
        }  
    },  
    "priority": 500,  
    "handler": {  
        "@odata.type": "#microsoft.graph.onPasswordMigrationCustomExtensionHandler",  
        "migrationPropertyId": "$extAttr",  
        "customExtension": {  
            "id": "$($authExtPwdMigrationId)"  
        }  
    }  
}  
"@

    $evtL = Invoke-MgGraphRequest -Method POST -Uri "https://graph.microsoft.com/v1.0/identity/authenticationEventListeners" -Body $evtListener
    write-host "Done. Id: $($evtL.id)" -ForegroundColor Green
}
