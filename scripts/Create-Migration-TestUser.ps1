
################################################################################################################################
# Variables
################################################################################################################################

# run: . .\.env.ps1   or  . .\env.Development.ps1  to load Variables

Connect-MgGraph -tenantId $tenantId -NoWelcome -Scopes "User.ReadWrite.All"

################################################################################################################################
# Create the Custome Authentication Extension app registrations  
################################################################################################################################

$ciamHostname = "cljungciamdevse"
$userEmail = "johndoe@live.com"
$displayName = "John Doe"
$extAttribute_toBeMigrated = ""

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
            "issuer": "$ciamHostname.onmicrosoft.com",
            "issuerAssignedId": "$userEmail"
        }
    ]
}
"@

Invoke-MgGraphRequest -Method POST -Uri "https://graph.microsoft.com/v1.0/users" -Body $userProfile


