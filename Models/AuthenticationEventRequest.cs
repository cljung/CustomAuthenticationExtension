using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomAuthenticationExtension.Models;

public class AuthenticationEventRequest {
    public string? type { get; set; }        // "microsoft.graph.authenticationEvent.attributeCollectionStart"
    public string? source { get; set; }      // "/tenants/aaaabbbb-0000-cccc-1111-dddd2222eeee/applications/<resourceAppguid>"
    public Data? data { get; set; }

    public static AuthenticationEventRequest? DeserializeFromJson( string requestJSON ) {
        return JsonSerializer.Deserialize<AuthenticationEventRequest>( requestJSON );
    }
}

public class Data {
    [JsonPropertyName( "@odata.type" )]
    public string? odatatype { get; set; }   // "microsoft.graph.onAttributeCollectionStartCalloutData"
    public string? tenantId { get; set; }
    public string? authenticationEventListenerId { get; set; }
    public string? customAuthenticationExtensionId { get; set; }
    public string? encryptedPasswordContext { get; set; } // encrypted JWE format: user-password, username, nonce
    public AuthenticationContext? authenticationContext { get; set; }
    public UserSignUpInfo? userSignUpInfo { get; set; }
    public OtpContext? otpContext { get; set; }
}

public class AuthenticationContext {
    public string? correlationId { get; set; }
    public Client? client { get; set; }
    public string? protocol { get; set; }
    public ClientServicePrincipal? clientServicePrincipal { get; set; }
    public ResourceServicePrincipal? resourceServicePrincipal { get; set; }
    [JsonPropertyName("user")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TokenIssuanceUser? user { get; set; }
}
public class OtpContext {
    [JsonPropertyName( "identifier")] 
    public string? Identifier { get; set; }
    [JsonPropertyName( "oneTimeCode")]
    public string? OneTimeCode { get; set; }
}

public class UserSignUpInfo {
    public IDictionary<string, Attribute>? attributes { get; set; }
    public List<Identity>? identities { get; set; }
}

public class Client {
    public string? ip { get; set; }
    public string? locale { get; set; }
    public string? market { get; set; }
}

public class ClientServicePrincipal {
    public string? id { get; set; }
    public string? appId { get; set; }
    public string? appDisplayName { get; set; }
    public string? displayName { get; set; }
}

public class ResourceServicePrincipal {
    public string? id { get; set; }
    public string? appId { get; set; }
    public string? appDisplayName { get; set; }
    public string? displayName { get; set; }
}


public class Identity {
    public string? signInType { get; set; }
    public string? issuer { get; set; }
    public string? issuerAssignedId { get; set; }
}


public class Attribute {
    [JsonPropertyName( "@odata.type" )]
    public string? odatatype { get; set; }
    public object? value { get; set; }
    public string? attributeType { get; set; }

}

public class TokenIssuanceUser {
    [JsonPropertyName( "id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? id { get; set; }

    [JsonPropertyName( "userPrincipalName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? userPrincipalName { get; set; }

    [JsonPropertyName( "userType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? userType { get; set; }
    [JsonPropertyName( "createdDateTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? createdDateTime { get; set; }

    [JsonPropertyName( "displayName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? displayName { get; set; }

    [JsonPropertyName( "givenName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? givenName { get; set; }

    [JsonPropertyName( "surname")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? surname { get; set; }
    [JsonPropertyName( "mail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? mail { get; set; }

    [JsonPropertyName( "preferredLanguage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? preferredLanguage { get; set; }


}