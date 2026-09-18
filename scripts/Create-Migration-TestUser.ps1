
################################################################################################################################
# Variables
################################################################################################################################

# run: . .\.env.ps1   or  . .\env.Development.ps1  to load Variables

Connect-MgGraph -tenantId $tenantId -NoWelcome -Scopes "User.ReadWrite.All"

################################################################################################################################
# Create the Custome Authentication Extension app registrations  
################################################################################################################################

$currentContext = Get-MgContext
$ciamDomain = $currentContext.Account.Split("@")[1]

$userEmail = "johndoe@live.com"
$displayName = "John Doe"

$b2cExt = Get-MgApplication -Filter "startswith(displayName, 'b2c-extension')"
$extAttribute_toBeMigrated = "extension_"+$b2cExt.appId.Replace("-","")+"_toBeMigrated"

$userProfile = @"
{
    "accountEnabled": true,
    "creationType": "LocalAccount",
    "displayName": "$displayName",
    "mail": "$userEmail",
    "passwordPolicies": "DisablePasswordExpiration",
    "passwordProfile": {
        "forceChangePasswordNextSignIn": false,
        "password": "$((New-Guid).Guid.ToString())"
    },
    "$extAttribute_toBeMigrated": true,
    "identities": [
        {
            "signInType": "emailAddress",
            "issuer": "$ciamDomain",
            "issuerAssignedId": "$userEmail"
        }
    ]
}
"@

$userProfile 
Invoke-MgGraphRequest -Method POST -Uri "https://graph.microsoft.com/v1.0/users" -Body $userProfile -ContentType "application/json" -Verbose


<#
# Pre-creating a federated gmail User. The first time a user signs in with the gmail account, Entra will redirect to Google Identity
# where the user authenticates. Then the Entra user will be linked to the gmail account

$userEmail = "johndoe@gmail.com"
$displayName = "John Doe (gmail)"


$userProfileGMail = @"
{
    "accountEnabled": true,
    "displayName": "$displayName",
    "mail": "$userEmail",
    "otherMails": [ "$userEmail" ],
    "identities": [
        {
            "signInType": "federated",
            "issuer": "google.com",
            "issuerAssignedId": "$userEmail"
        }
    ]
}
"@

Invoke-MgGraphRequest -Method POST -Uri "https://graph.microsoft.com/v1.0/users" -Body $userProfileGMail -ContentType "application/json" -Verbose

#>