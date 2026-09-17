using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using CustomAuthenticationExtension.Models;

namespace CustomAuthenticationExtension.Helpers;

public interface IAuth0Helper {
    public Auth0Response? Authenticate(string username, string password);
}
public class Auth0Helper : IAuth0Helper {
    private readonly ILogger<IAuth0Helper> _log;
    private readonly string _tenantDomain;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _scopes;

    public Auth0Helper( IConfiguration configuration, ILogger<IAuth0Helper> log ) {
        _log = log;
        _tenantDomain = configuration["Auth0:TenantDomain"]!.ToString();
        _clientId = configuration["Auth0:ClientID"]!.ToString();
        _clientSecret = configuration["Auth0:ClientSecret"]!.ToString();
        _scopes = configuration.GetValue("Auth0:scopes", "openid profile email")!;
    }
    public Auth0Response? Authenticate( string username, string password ) {
        Auth0Response? authResp = null;
        string url = $"https://{_tenantDomain}/oauth/token";
        Auth0Request authReq = new Auth0Request() { client_id = _clientId, client_secret = _clientSecret, scope = _scopes
                                                    , username =username, password = password };
        string jsonString = JsonSerializer.Serialize(authReq);
        using (HttpContent content = new StringContent(jsonString, System.Text.Encoding.UTF8, "application/json")) {
            using (HttpClient client = new HttpClient()) {
                HttpResponseMessage res = client.PostAsync(url, content).Result;
                string response = res.Content.ReadAsStringAsync().Result;
                if (res.IsSuccessStatusCode) {
                    _log.LogTrace($"Auth0 successful response for user {username}");
                    authResp = JsonSerializer.Deserialize<Auth0Response>(response);
                } else {
                    _log.LogTrace($"Auth0 unsuccessful response {res.StatusCode.ToString()}: {response}" );
                }
            }
        }
        return authResp;
    }
}
public class Auth0Request {
    public string grant_type { get; set; } = "http://auth0.com/oauth/grant-type/password-realm";
    public string realm { get; set; } = "Username-Password-Authentication";
    public string scope { get; set; } = "openid profile email";
    public string client_id { get; set; } = string.Empty;
    public string client_secret { get; set; } = string.Empty;
    public string username { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
}
public class Auth0Response {
    public string? access_token { get; set; }
    public string? id_token { get; set; }
    public string? scope { get; set; }
    public int expires_in { get; set; }
    public string? token_type { get; set; }
}
public class Auth0Error {
    public string? error { get; set; }
    public string? error_description { get; set; }
}