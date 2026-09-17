using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomAuthenticationExtension.Models;

public enum AuthenticationEvent {
    Unknown,
    AttributeCollectionStart,
    AttributeCollectionSubmit,
    TokenIssuanceStart,
    OtpSend,
    PasswordSubmit
}
public enum PasswordMigrateResponse {
    MigratePassword,
    MigrateWeakPassword,
    PasswordIncorrect,
    BlockUser
}
public class AuthenticationEventResponse {
    [JsonIgnore]
    private static readonly JsonSerializerOptions CompactJsonOptions = new() {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    [JsonPropertyName("data")]
    public AuthenticationEventResponseData? data { get; set; }

    public static string SerializeToJson( AuthenticationEventResponse response ) {
        return JsonSerializer.Serialize(response, CompactJsonOptions);
    }
    public static AuthenticationEventResponse ContinueWithDefaultBehaviourResponse( AuthenticationEvent authEventType ) {
        string type = string.Empty;
        string responseType = string.Empty;
        switch (authEventType) {
            case AuthenticationEvent.AttributeCollectionStart:
                type = "microsoft.graph.onAttributeCollectionStartAuthenticationEventResponseData";
                responseType = "microsoft.graph.attributeCollectionStart.continueWithDefaultBehavior";
                break;
            case AuthenticationEvent.AttributeCollectionSubmit:
                type = "microsoft.graph.onAttributeCollectionSubmitAuthenticationEventResponseData";
                responseType = "microsoft.graph.attributeCollectionSubmit.continueWithDefaultBehavior";
                break;
            case AuthenticationEvent.OtpSend:
                type = "microsoft.graph.OnOtpSendResponseData";
                responseType = "microsoft.graph.OtpSend.continueWithDefaultBehavior";
                break;
            default:
                throw new ArgumentException( $"Unsupported authenticationEvent.type; {authEventType.ToString()}" );
        }
        return new AuthenticationEventResponse {
            data = new AuthenticationEventResponseData {
                type = type,
                actions = new List<ContinueWithDefaultBehavior>{
                                    new ContinueWithDefaultBehavior { odatatype = responseType }
                                }
            }
        };
    }
    public static AuthenticationEventResponse SetPrefilledValues( AuthenticationEvent authEventType, IDictionary<string, object> claims ) {
        if (authEventType != AuthenticationEvent.AttributeCollectionStart) {
            throw new ArgumentException( $"Unsupported authenticationEvent.type; {authEventType.ToString()}" );
        }
        return new AuthenticationEventResponse {
            data = new AuthenticationEventResponseData {
                type = "microsoft.graph.onAttributeCollectionStartAuthenticationEventResponseData",
                actions = new List<PrefillAction>{
                                    new PrefillAction { odatatype = "microsoft.graph.attributeCollectionStart.setPrefillValues"
                                    , inputs = claims
                                    }
                                }
            }
        };
    }

    public static AuthenticationEventResponse ShowBlockPage( AuthenticationEvent authEventType, string title, string message ) {
        if (authEventType != AuthenticationEvent.AttributeCollectionStart) {
            throw new ArgumentException( $"Unsupported authenticationEvent.type; {authEventType.ToString()}" );
        }
        return new AuthenticationEventResponse {
            data = new AuthenticationEventResponseData {
                type = "microsoft.graph.onAttributeCollectionStartAuthenticationEventResponseData",
                actions = new List<ShowBlockPage>{
                                    new ShowBlockPage { odatatype = "microsoft.graph.attributeCollectionStart.showBlockPage"
                                    , title = title, message = message
                                    }
                                }
            }
        };
    }

    public static AuthenticationEventResponse ProvideClaimsForTokenResponse( AuthenticationEvent authEventType, IDictionary<string, object> claims ) {
        if (authEventType != AuthenticationEvent.TokenIssuanceStart) {
            throw new ArgumentException( $"Unsupported authenticationEvent.type; {authEventType.ToString()}" );
        }
        return new AuthenticationEventResponse {
            data = new AuthenticationEventResponseData {
                type = "microsoft.graph.onTokenIssuanceStartResponseData",
                actions = new List<ProvideClaimsAction>{
                                    new ProvideClaimsAction {
                                        odatatype = "microsoft.graph.tokenIssuanceStart.provideClaimsForToken"
                                        , claims = claims
                                    }
                                }
            }
        };
    }

    public static AuthenticationEventResponse PasswordSubmitResponse(AuthenticationEvent authEventType, PasswordMigrateResponse passwordAction, string nonce) {
        if (authEventType != AuthenticationEvent.PasswordSubmit) {
            throw new ArgumentException($"Unsupported authenticationEvent.type; {authEventType.ToString()}");
        }
        string odatatype = "";
        switch( passwordAction ) {
            case PasswordMigrateResponse.MigratePassword:
                odatatype = "microsoft.graph.passwordSubmit.MigratePassword";
                break;
            case PasswordMigrateResponse.MigrateWeakPassword:
                odatatype = "microsoft.graph.passwordSubmit.UpdatePassword";
                break;
            case PasswordMigrateResponse.PasswordIncorrect:
                odatatype = "microsoft.graph.passwordSubmit.Retry";
                break;
            case PasswordMigrateResponse.BlockUser:
                odatatype = "microsoft.graph.passwordSubmit.Block";
                break;
            default:
                throw new ArgumentException($"Unsupported PasswordMigrateResponse; {passwordAction.ToString()}");
        }
        return new AuthenticationEventResponse {
            data = new AuthenticationEventResponseData {
                type = "microsoft.graph.onPasswordSubmitResponseData",
                actions = new List<ProvideClaimsAction>{ new ProvideClaimsAction { odatatype = odatatype }},
                nonce = nonce
            }
        };
    }

}


public class AuthenticationEventResponseData {
    [JsonPropertyName( "@odata.type" )]
    public string? type { get; set; }

    [JsonPropertyName( "actions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? actions { get; set; }
    public string? nonce { get; set; }  // for password migration
}


public class ContinueWithDefaultBehavior {
    [JsonPropertyName( "@odata.type" )]
    public string? odatatype { get; set; }
}
public class ShowBlockPage {
    [JsonPropertyName( "@odata.type" )]
    public string? odatatype { get; set; }
    public string? title { get; set; }
    public string? message { get; set; }
}

public class PrefillAction {
    [JsonPropertyName( "@odata.type" )]
    public string? odatatype { get; set; }
    public IDictionary<string, object>? inputs { get; set; }
}
public class ProvideClaimsAction {
    [JsonPropertyName( "@odata.type" )]
    public string? odatatype { get; set; }
    public IDictionary<string, object>? claims { get; set; }
}
