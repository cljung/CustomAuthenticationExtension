################################################################################################################################
# Variables
################################################################################################################################

# run: . .\.env.ps1   or  . .\env.Development.ps1  to load Variables

Connect-MgGraph -tenantId $tenantId -NoWelcome -Scopes "Application.ReadWrite.All", "AppRoleAssignment.ReadWrite.All", "Policy.ReadWrite.ApplicationConfiguration", "Policy.Read.All"

$clientApp = Get-MgApplication -Filter "displayName eq '$clientAppName'"
if ( $null -eq $clientAPp ) {
    write-error "The client app $clientAppName is not registered. Please register that app and a user Flow first"
    exit
}
$clientAppId = $clientApp.AppId

################################################################################################################################
# Verify the Custome Authentication Extension app registrations  
################################################################################################################################
$extApp = Get-MgApplication -Filter "displayName eq '$customExtensionAppName'"

if ( $null -ne $extApp ) {
    write-host "Application '$customExtensionAppName' already exists." -ForegroundColor Yellow
    $identifierUri = $extApp.identifierUris[0]
} else {
    write-error "The custom extension app $customExtensionAppName is not registered. run Create-CustomAuthExtension-app.ps1 script first"
    exit
}

################################################################################################################################
# Create the Custome Authentication Extension 
################################################################################################################################

$authExt = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/identity/customAuthenticationExtensions"
if ( ($authExt.value | where {$_.'@odata.type' -eq "#microsoft.graph.onTokenIssuanceStartCustomExtension"}) -gt 0 ) {
    Write-Host "Custom Authentication Extension already exists." -ForegroundColor Yellow
} else {
    Write-Host "Creating Custom Authentication Extension..." -ForegroundColor Cyan

    $customAuthExtension = [ordered]@{
        "clientConfiguration"         = [ordered]@{
            "maximumRetries"        = 1
            "timeoutInMilliseconds" = 2000
        }
        "@odata.type"                 = "#microsoft.graph.onTokenIssuanceStartCustomExtension"
        "behaviorOnError"             = $null
        "claimsForTokenConfiguration" = @(
            [ordered]@{ "claimIdInApiResponse" = "dateOfBirth" },
            [ordered]@{ "claimIdInApiResponse" = "memberSince" },
            [ordered]@{ "claimIdInApiResponse" = "customRoles" }
        )
        "authenticationConfiguration" = [ordered]@{
            "@odata.type" = "#microsoft.graph.azureAdTokenAuthentication"
            "resourceId"  = $identifierUri
        }
        "displayName"                 = "$TokenIssaunceExtName"
        "endpointConfiguration"       = [ordered]@{
            "@odata.type" = "#microsoft.graph.httpRequestEndpoint"
            "targetUrl"   = "https://$apiHostingDomain/vsdbg/api/authenticationevent/tokenIssuanceStart"
        }
        "description"                 = $null
    }
    $custExt = Invoke-MgGraphRequest -Method POST -Uri "https://graph.microsoft.com/v1.0/identity/customAuthenticationExtensions" -Body ($customAuthExtension | ConvertTo-Json -Compress) -ContentType "application/json"

    $sp = Get-MgServicePrincipal -Filter "appId eq '$clientAppId'"

$policyDefinition = @"
{
    "ClaimsMappingPolicy": {
        "Version": 1,
        "IncludeBasicClaimsForPropagatedPicker": true,
        "ClaimsSchema": [
            {
                "Source": "CustomClaimsProvider",
                "ID": "dateOfBirth",
                "JwtClaimType": "birthdate"
            },
            {
                "Source": "CustomClaimsProvider",
                "ID": "memberSince",
                "JwtClaimType": "member_since"
            },
            {
                "Source": "CustomClaimsProvider",
                "ID": "customRoles",
                "JwtClaimType": "roles"
            }
        ],
        "CustomClaimsProviderId": "$($custExt.Id))"
    }
}
"@
    Write-Host "Creating Claims Mapping Policy..." -ForegroundColor Cyan
    $policyParams = @{
        Definition  = @($policyDefinition)
        DisplayName = "CustomClaimsProviderMappingPolicy"
        IsOrganizationDefault = $false
    }
    $newPolicy = New-MgPolicyClaimMappingPolicy -BodyParameter $policyParams

    Write-Host "Policy Created Successfully! ID: $($newPolicy.Id)" -ForegroundColor Green

    Write-Host "`nLinking policy to Service Principal..." -ForegroundColor Cyan
    $linkParams = @{
        "@odata.id" = "https://graph.microsoft.com/v1.0/policies/claimsMappingPolicies/$($newPolicy.Id)"
    }

    $evtListener = [ordered]@{
        "authenticationEventsFlowId" = $null
        "handler"                    = [ordered]@{
            "configuration" = $null
            "@odata.type"   = "#microsoft.graph.onTokenIssuanceStartCustomExtensionHandler"
            "customExtension" = [ordered]@{
                "behaviorOnError"             = $null
                "endpointConfiguration"       = [ordered]@{
                    "targetUrl"   = "$apiEndpointPasswordMigration"
                    "@odata.type" = "#microsoft.graph.httpRequestEndpoint"
                }
                "id"                          = "$($custExt.Id)"
                "description"                 = $null
                "authenticationConfiguration" = [ordered]@{
                    "resourceId"  = "$identifierUri"
                    "@odata.type" = "#microsoft.graph.azureAdTokenAuthentication"
                }
                "claimsForTokenConfiguration" = @(
                    [ordered]@{ "claimIdInApiResponse" = "dateOfBirth" },
                    [ordered]@{ "claimIdInApiResponse" = "memberSince" },
                    [ordered]@{ "claimIdInApiResponse" = "customRoles" }
                )
                "clientConfiguration"         = [ordered]@{
                    "maximumRetries"        = 1
                    "timeoutInMilliseconds" = 2000
                }
                "displayName"                 = "$TokenIssaunceExtName"
            }
        }
        "conditions"                 = [ordered]@{
            "applications" = [ordered]@{
                "includeAllApplications"             = $false
                "includeApplications"                = @(
                    [ordered]@{ "appId" = "$clientAppID" }
                )
            }
        }
        "@odata.type"                = "#microsoft.graph.onTokenIssuanceStartListener"
        "displayName"                = $null
        "priority"                   = 500
    }

    Invoke-MgGraphRequest -Method POST -Uri "https://graph.microsoft.com/v1.0/identity/authenticationEventListeners" -Body $evtListener

    Write-Host "Custom Claims Provider successfully attached to Service Principal for app '$clientAppName'" -ForegroundColor Green
}
