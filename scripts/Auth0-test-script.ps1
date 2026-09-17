# Auth0 test script

$Auth0TenantDomain = "something ... .auth0.com"
$Auth0ClientId     = "..."
$Auth0ClientSecret = "..."

$Auth0Username     = "johndoe@live.com"
$Auth0Password     = "My-Secret-Password"

$body = @{
    grant_type = "http://auth0.com/oauth/grant-type/password-realm"
    realm      = "Username-Password-Authentication"
    username   = $Auth0Username
    password   = $Auth0Password
    client_id  = $Auth0ClientId
    client_secret = $Auth0ClientSecret
    scope      = "openid profile email"
}

try {
    $response = Invoke-RestMethod -Uri "https://$Auth0TenantDomain/oauth/token" -Method Post -ContentType "application/json" -Body ($body | ConvertTo-Json -Compress) -Verbose
    Write-Host "Authentication Successful!`n" -ForegroundColor Green
    $response
} catch {
    Write-Error "Authentication Failed!"
    $_ | Format-List -Property *
}
