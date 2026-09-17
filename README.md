# Custom Authentication Extensions

## Documentation

[Attribute Collection Start and Submit](https://learn.microsoft.com/en-us/entra/identity-platform/custom-extension-attribute-collection)
[Token Issuance Start](https://learn.microsoft.com/en-us/entra/identity-platform/custom-extension-tokenissuancestart-setup)
[Password Migration](https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-migrate-passwords-just-in-time)
[Blog posts](https://blog.redbaronofazure.com/?p=7989)

## Configuration steps

### Custom authentication extensions

1. Entra Admin Center > External identities > Custom authentication exietnsions
1. +Create a custom extension
1. Select Event Type, enter Endpoint Configuration incl target URL
1. In API Authentication, easiest way is to create a new app. It will add the required API Permissions. In Entra External tenants, the appID URI needs to have format api://domain/appId for some wierd reason. Otherwise you get error 'The resourceId is not found in the IdentifierUris property of the app'
1. Enter the claim names as your API returns name. No decoration, no prefix - just as the onTokenIssuanceStart API returns them

THe permission needed by the app selected is `CustomAuthenticationExtension.Receive.Payload`. The access token passed to your API will have `àud` claim with a value of this app's appId.

### User Flows configuration

You only need to configure things on a User Flow if you will use Attribute Collection Start and Submit. If you plan to use it, it is just a drop down of the above defined extension(s) where you pick the wanted endpoint.

### Enterprise app configuration

In bind the TokenIssuanceStart API to your application, you go into Entra > Enterprise App for the app registration you will use to interactively sign in to your application.

1. Entra Admin Center > Enterprise apps > Single sign-on > Attributes / claims 
1. Open Advanced settings > Configure claims provider
1. For each claim defined when defining the extension (above) do
    1. +Ádd new claim
    1. set claim name as the name defined above (no decoration). Leave namespace blank
	1. for source, pick your defined claim. It will be listed as `customclaimsprovider.yourname`
    1. click Save

### App registration configuration

In the app registration your users will use to sign in to (the one you pick when you test run the User Flow), change the following

1. Entra Admin Center > App registrations > {your app} > Manifest
1. Set `"acceptMappedClaims": true,`
1. Click Save
1. Wait 60 seconds before testing

### Application Token Configuration

Don't go wild in your Token Issuance Start extension. Have a look at available options at Entra > App registrations > (your app) > Token configurationn. User profile attribute, group memberships, etc, can be added to the id and/or access tokens.

## Example 

### Entra Access Token when calling

```JSON
{
  "aud": "55555555-2222-3333-4444-555555555555",  // appId of the registered custom extension app with permission CustomAuthenticationExtension.Receive.Payload
  "iss": "https://11111111-2222-3333-4444-555555555555.ciamlogin.com/11111111-2222-3333-4444-555555555555/v2.0",
  "azp": "99045fe1-7639-4a75-9d4a-577b6ca3810f",  // appID for SP "Azure Active Directory Authentication Extensions"
  "oid": "34d97ed7-a7b1-4f58-a9a7-85770c2d5112",  // objectID for -"-
  "sub": "34d97ed7-a7b1-4f58-a9a7-85770c2d5112",
  "tid": "11111111-2222-3333-4444-555555555555",
}
```

### TokenIssuanceStart request

```JSON
{
  "type": "microsoft.graph.authenticationEvent.tokenIssuanceStart",
  "source": "/tenants/11111111-2222-3333-4444-555555555555/applications/99999999-2222-3333-4444-555555555555",
  "data": {
    "@odata.type": "microsoft.graph.onTokenIssuanceStartCalloutData",
    "tenantId": "11111111-2222-3333-4444-555555555555",
    "authenticationEventListenerId": "9cad4574-0b4c-4c5d-b5c4-23cc774223f7",
    "customAuthenticationExtensionId": "2629432d-285b-40a2-8f54-e629409e2527", // tokenIssuancePOlict [directoryObjects/{id}
    "authenticationContext": {
      "correlationId": "851571c4-d57b-4b8d-b472-2d4318229d8c",
      "client": {
        "ip": "321.456.789.255",
        "locale": "en-us",
        "market": "en-us"
      },
      "protocol": "OAUTH2.0",
      "clientServicePrincipal": {
        "id": "a70c1626-70cf-4657-9e4e-bbd8e8908d4e",    // objectID of the app user is signing in to
        "appId": "99999999-2222-3333-4444-555555555555", // appID -"-
        "appDisplayName": "ciam-test-app",
        "displayName": "ciam-test-app"
      },
      "resourceServicePrincipal": {
        "id": "a70c1626-70cf-4657-9e4e-bbd8e8908d4e",
        "appId": "99999999-2222-3333-4444-555555555555",
        "appDisplayName": "ciam-test-app",
        "displayName": "ciam-test-app"
      },
      "user": {
        "createdDateTime": "2023-08-31T07:27:24Z",
        "displayName": "John Doe",
        "givenName": "John",
        "id": "77777777-2222-3333-4444-555555555555",
        "mail": "johndoe@live.com",
        "preferredLanguage": "en-US",
        "surname": "Doe",
        "userPrincipalName": "77777777-2222-3333-4444-555555555555@foobar.onmicrosoft.com",
        "userType": "Member"
      }
    }
  }
}
```

### TokenIssuanceStart response

```JSON
{
  "name": {
    "@odata.type": "microsoft.graph.onTokenIssuanceStartResponseData",
    "actions": [
      {
        "@odata.type": "microsoft.graph.tokenIssuanceStart.provideClaimsForToken",
        "claims": {
          "dateOfBirth": "1980-01-01",
          "customRoles": [
            "Moderator",
            "Writer",
            "Reader"
          ],
          "memberSince": "2023-08-31"
        }
      }
    ]
  }
}
```

### Issued Access Token

```JSON
{
  "aud": "99999999-2222-3333-4444-555555555555",
  "iss": "https://11111111-2222-3333-4444-555555555555.ciamlogin.com/11111111-2222-3333-4444-555555555555/v2.0",
  ...
  "dateOfBirth": "1980-01-01",
  "customRoles": [
    "Moderator",
    "Writer",
    "Reader"
  ],
  "memberSince": "2023-08-31"
}
```

### OnPasswordSubmit event

```JSON
{
  "type": "microsoft.graph.authenticationEvent.passwordSubmit",
  "source": "/tenants/11111111-2222-3333-4444-555555555555/applications/99999999-2222-3333-4444-555555555555",
  "data": {
    "@odata.type": "microsoft.graph.onPasswordSubmitCalloutData",
    "tenantId": "11111111-2222-3333-4444-555555555555",
    "authenticationEventListenerId": "1aeebe49-233d-4a1f-afe5-5c08cb8172ff",
    "customAuthenticationExtensionId": "761fcc06-379a-4544-b2b3-f5aa1dc4ae0d",
    "encryptedPasswordContext": "eyJhbGciOiJSU0EtT0FFUCIsImVuYyI6IkExMjhDQkMtSFMyNTYiLCJ4NXQiOiJobmVNLUpnQmFDZGdhdXQwZ0VBdWZHUnVrVkUiLCJ6aXAiOiJERUYifQ.qr2ihk3dFpWUWqjjltefWeyrggkLo8wz-ZOjU0alPcrugfen2MXc93hKCTIw6k-2HsTYfQyU0Pv3ZIEWTrlyI42FhfLw-GPB9nbMzxxQaEnM6KZIrOvV4wlBim4EK-N_73LVmj8wgHyiR_5W3P8-Utls_LASwiPzFvjsqUXMD6rUflynli7Oxhi7z9oSxCNmHYuZaQX0dqmYYPCvewFIxkknv_t3pg1Xh6V52NECS0l98KWlxMXYzR8AOTVN5pPZCPD4gWe0BRIP1Q2t2cxgxvwHPnwiwxO277GKXBfVSNTHZ4Ny79hl_BgMBkb1b1WNdx82fRjU4q8UZl0JjW0MYw.f7HAt20evLIHxg-IgFXWtA.DbqgGRpnvNcJiFrNqMIWTYFHKeuya3OOe2N2O-i8jtR1nOx3enjh5ekO_ILYGFWOAQ9mEr78y5hCDcimpIODXWF8XNVQS7F0zGsdGKK8Puq-dXLYGoHIzsB1JvHJd49ig0r1vTzFD6T2ms5qdn4QwTp4rO2Rlah2BLaXJ3wT1SnejdS_4IHAC6p1ruLwECMW5UdF8VB0uUA5zM_-VQjQUMhd90cv8ajEBpv7ZLhz9M-19kF_p3HjqqHXjW4PHaaWVZ4tj_uBIOywlwhSGiR7MdWb3FXIsIr8lEm-d0M_ab4k2Vd8yc-LG_uNj9LQhprcZdRUKT7DV5UlEpSgdhLP9GddJuUo8cgW1AcBa8WpLVWa3bYh7uAs448CHx4H_ti6IuViWV-nGvaKCAH3fCOoqdBjBdB2MfsQCUD91hO8kURQh6HujFRgngoMjp9x3bpWpdJXSYh-5kAl-sW0uKCoW9VZ0SoNnKhwFAwXc0RSXsEbY6Vib3HkQUHvIr6DPpuGO-UkzQ8lvWjtMnnEOaUl7w.vQxJiX7bzFiqs6S7Oiwy4g",
    "authenticationContext": {
      "correlationId": "a2b1f0dd-19f6-43a9-be18-3ba36ad419a0",
      "client": {
        "ip": "321.456.789.255",
        "locale": "en-gb",
        "market": "en-gb"
      },
      "protocol": "OAUTH2.0",
      "clientServicePrincipal": {
        "id": "a70c1626-70cf-4657-9e4e-bbd8e8908d4e",
        "appId": "99999999-2222-3333-4444-555555555555",
        "appDisplayName": "ciam-test-app",
        "displayName": "ciam-test-app"
      },
      "resourceServicePrincipal": null,
      "user": {
        "id": "77777777-2222-3333-4444-555555555555",
        "userPrincipalName": "77777777-2222-3333-4444-555555555555@foobar.onmicrosoft.com",
        "userType": "Member",
        "createdDateTime": "2026-09-15T13:08:10Z",
        "displayName": "John Doe",
        "mail": "johndoe@live.com"
      }
    },
    "userSignUpInfo": null,
    "otpContext": null
  }
}```

#### Response

```JSON
{
  "data": {
    "@odata.type": "microsoft.graph.onPasswordSubmitResponseData",
    "actions": [
      {
        "@odata.type": "microsoft.graph.passwordSubmit.MigratePassword"
      }
    ],
    "nonce": "d7c30ed9-2d8e-40c9-aee8-a205e9734308"
  }
}
```

### Encrypted Password Token

```JWT
eyJhbGciOiJSU0EtT0FFUCIsImVuYyI6IkExMjhDQkMtSFMyNTYiLCJ4NXQiOiJobmVNLUpnQmFDZGdhdXQwZ0VBdWZHUnVrVkUiLCJ6aXAiOiJERUYifQ.qr2ihk3dFpWUWqjjltefWeyrggkLo8wz-ZOjU0alPcrugfen2MXc93hKCTIw6k-2HsTYfQyU0Pv3ZIEWTrlyI42FhfLw-GPB9nbMzxxQaEnM6KZIrOvV4wlBim4EK-N_73LVmj8wgHyiR_5W3P8-Utls_LASwiPzFvjsqUXMD6rUflynli7Oxhi7z9oSxCNmHYuZaQX0dqmYYPCvewFIxkknv_t3pg1Xh6V52NECS0l98KWlxMXYzR8AOTVN5pPZCPD4gWe0BRIP1Q2t2cxgxvwHPnwiwxO277GKXBfVSNTHZ4Ny79hl_BgMBkb1b1WNdx82fRjU4q8UZl0JjW0MYw.f7HAt20evLIHxg-IgFXWtA.DbqgGRpnvNcJiFrNqMIWTYFHKeuya3OOe2N2O-i8jtR1nOx3enjh5ekO_ILYGFWOAQ9mEr78y5hCDcimpIODXWF8XNVQS7F0zGsdGKK8Puq-dXLYGoHIzsB1JvHJd49ig0r1vTzFD6T2ms5qdn4QwTp4rO2Rlah2BLaXJ3wT1SnejdS_4IHAC6p1ruLwECMW5UdF8VB0uUA5zM_-VQjQUMhd90cv8ajEBpv7ZLhz9M-19kF_p3HjqqHXjW4PHaaWVZ4tj_uBIOywlwhSGiR7MdWb3FXIsIr8lEm-d0M_ab4k2Vd8yc-LG_uNj9LQhprcZdRUKT7DV5UlEpSgdhLP9GddJuUo8cgW1AcBa8WpLVWa3bYh7uAs448CHx4H_ti6IuViWV-nGvaKCAH3fCOoqdBjBdB2MfsQCUD91hO8kURQh6HujFRgngoMjp9x3bpWpdJXSYh-5kAl-sW0uKCoW9VZ0SoNnKhwFAwXc0RSXsEbY6Vib3HkQUHvIr6DPpuGO-UkzQ8lvWjtMnnEOaUl7w.vQxJiX7bzFiqs6S7Oiwy4g
```

```JSON
{
  "aud": "55555555-2222-3333-4444-555555555555",
  "iss": "api://dev.fawltytowers2.com/55555555-2222-3333-4444-555555555555",
  "iat": 1789477420,
  "nbf": 1789477420,
  "exp": 1789478020,
  "user-password": "My-Secret-Password",
  "username": "johndoe@live.com",
  "nonce": "d7c30ed9-2d8e-40c9-aee8-a205e9734308"
}
```

## Auth0 Migration Configuration

Migrating from Auth0 to Entra External ID requires being able to authenticate an end user via an Auth0 API. That is quite possible and requires the following:

- Create Application in the Auth0 portal. Select `Regular Web Application` or `Machine to Machine Application`
- In Application > Advanced Settings > Grant Types, check `Password` to enable 
- Copy `Domain`, `Client ID` and `Client Secret` from applications page

With this information, you can test authentication for an end user with Powershell

```Powershell
$body = @{
    grant_type = "http://auth0.com/oauth/grant-type/password-realm"
    realm      = "Username-Password-Authentication"
    username   = $username
    password   = $password
    client_id  = $clientId
    client_secret = $clientSecret
    scope      = "openid profile email"
}

try {
    $response = Invoke-RestMethod -Uri "https://$tenantDomain/oauth/token" -Method Post -ContentType "application/json" -Body ($body | ConvertTo-Json -Compress) -Verbose
    Write-Host "Authentication Successful!`n" -ForegroundColor Green
    $response
} catch {
    Write-Error "Authentication Failed!"
    $_ | Format-List -Property *
}
```

If the username or password is wrong, Auth0 will respond with 

```JSON
{
  "error": "invalid_grant",
  "error_description": "Wrong email or password." 
}
```