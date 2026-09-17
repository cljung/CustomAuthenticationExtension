################################################################################################################################
# Variables
################################################################################################################################

# run: . .\.env.ps1   or  . .\env.Development.ps1  to load Variables

Connect-MgGraph -tenantId $tenantId -NoWelcome -Scopes "Application.ReadWrite.All", "AppRoleAssignment.ReadWrite.All"

################################################################################################################################
# Create the Custome Authentication Extension app registrations  
################################################################################################################################
$extApp = Get-MgApplication -Filter "displayName eq '$customExtensionAppName'"

if ( $null -ne $expApp ) {
    write-host "Application '$customExtensionAppName' already exists." -ForegroundColor Yellow
    $identifierUri = $extApp.identifierUris[0]
} else {
    Write-Host "Creating Custom Authentication Extension App Reg..." -ForegroundColor Cyan
    # Microsoft Graph Well-Known AppId & CustomAuthExtension.Receive.Payload AppRole ID
    $msgraphAppId = "00000003-0000-0000-c000-000000000000"
    $customAuthRoleId = "214e810f-fda8-4fd7-a475-29461495eb00"

    $requiredResourceAccess = @(
        @{
            resourceAppId = $msgraphAppId
            resourceAccess = @(
                @{
                    id = $customAuthRoleId
                    type = "Role" 
                }
            )
        }
    )

    Write-Host "Registering Application ' $customExtensionAppName'..." -ForegroundColor Cyan

    $newApp = New-MgApplication -DisplayName  $customExtensionAppName -RequiredResourceAccess $requiredResourceAccess

    Write-Host "Application created successfully!" -ForegroundColor Green
    Write-Host "AppId (Client ID): $($newApp.AppId)"
    Write-Host "ObjectId:          $($newApp.Id)"

    $identifierUri = "api://$apiHostingDomain/$($newApp.AppId)"
    Update-MgApplication -ApplicationId $newApp.Id -IdentifierUris @($identifierUri)

    Write-Host "`nCreating corresponding Service Principal..." -ForegroundColor Cyan
    $newSp = New-MgServicePrincipal -AppId $newApp.AppId

    Write-Host "Service Principal (Enterprise App) Created!" -ForegroundColor Green
    Write-Host "Service Principal ObjectId: $($newSp.Id)"

    Write-Host "Grant admin consent for the application '$customExtensionAppName' in the Entra Admin Center..." -ForegroundColor Green
}

# set the app owner to this user
$currentContext = Get-MgContext
$user = Get-MgUser -Filter "userPrincipalName eq '$($currentContext.Account)'"
$userOdataId = @{
     "@odata.id"= "https://graph.microsoft.com/v1.0/directoryObjects/$($user.id)"
     }
New-MgApplicationOwnerByRef -ApplicationId $newApp.Id -BodyParameter $userOdataId