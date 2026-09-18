using Azure.Core;
using CustomAuthenticationExtension.Helpers;
using CustomAuthenticationExtension.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CustomAuthenticationExtension.Controllers;

[Route( "api/[action]" )]
[ApiController]
public class ApiController : Controller {
    protected IMemoryCache _cache;
    protected readonly ILogger<ApiController> _log;
    protected readonly IConfiguration _configuration;
    protected readonly IGraphMail _graphMail;
    protected readonly ITwilioHelper _twilio;
    protected readonly IKeyVaultHelper _kv;
    protected readonly IAuth0Helper _auth0;
    private Random _rnd;
    private int _rndValue;

    public ApiController( IConfiguration configuration, IMemoryCache memoryCache, ILogger<ApiController> log
                            , IGraphMail graphMail, ITwilioHelper twilio, IKeyVaultHelper kv, IAuth0Helper auth0 ) {
        _cache = memoryCache;
        _log = log;
        _configuration = configuration;
        _graphMail = graphMail;
        _twilio = twilio;
        _kv = kv;
        _auth0 = auth0;
        _rnd = new Random();
        _rndValue = _rnd.Next(1,1000);
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Helpers
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    protected ActionResult ReturnErrorMessage( string errorMessage ) {
        return BadRequest( new { error = "400", error_description = errorMessage } );
    }
    // return 200 json 
    protected ActionResult ReturnJson( string json ) {
        return new ContentResult { ContentType = "application/json", Content = json };
    }
    protected string GetRequestHostName() {
        string hostname = this.Request.Headers["x-original-host"]!;
        if (string.IsNullOrEmpty( hostname )) {
            hostname = this.Request.Host.ToString();
        }
        return hostname;
    }
    private string GetRemoteIpAddress() {
        string ipaddr = this.Request.Headers["X-Forwarded-For"]!;
        if (string.IsNullOrEmpty( ipaddr )) {
            ipaddr = HttpContext.Connection.RemoteIpAddress!.ToString();
        }
        return ipaddr;
    }
    protected void TraceHttpRequest() {
        string ipaddr = GetRemoteIpAddress();
        StringBuilder sb = new StringBuilder();
        foreach( var hdr in this.Request.Headers ) { 
            sb.AppendFormat( "{0}:\t{1}\n", hdr.Key, hdr.Value );
        }
        _log.LogTrace( "{0} {1} -> {2} {3}://{4}{5}{6} ({7})\n{8}", DateTime.UtcNow.ToString( "o" ), ipaddr
                , this.Request.Method, this.Request.Scheme, this.Request.Host, this.Request.Path, this.Request.QueryString, _rndValue, sb.ToString() );
    }
    private void SetResponseHeaderValue( string key, string value ) {
        if (this.Response.Headers.ContainsKey(key))
             this.Response.Headers[key] = value;
        else this.Response.Headers.Append( key, value );
    }
    private bool ValidateSigninAppID(AuthenticationEventRequest request, out string? errorMessage) {
        List<string> acceptedSigninAppIDs = _configuration.GetSection("Entra:AcceptedSigninAppIDs").Get<List<string>>() ?? new List<string>();
        string? appIdSignin = request!.data!.authenticationContext!.clientServicePrincipal!.appId;
        if (null == appIdSignin || acceptedSigninAppIDs.Count == 0) {
            errorMessage = $"No valid clientServicePrincipal.appId configured";
            return false;
        }
        if (null == acceptedSigninAppIDs.Find(a => string.Equals(a, appIdSignin, StringComparison.OrdinalIgnoreCase))) {
            errorMessage = $"Invalid clientServicePrincipal.appId: {appIdSignin}";
        }
        errorMessage = null;
        return true;
    }
    private bool GetConfigBool(string key, bool defaultValue) {
        return _configuration.GetValue<bool>(key, defaultValue);
    }
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // API Endpoints 
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Test method to check connectivity
    /// </summary>
    /// <returns>Returns a pong response in text/plain and information in response header</returns>
    [HttpGet( "/api/ping" )]
    [Produces("text/plain")]
    public async Task<ActionResult> Ping() {
        TraceHttpRequest();
        SetResponseHeaderValue( "x-server", Environment.MachineName );
        SetResponseHeaderValue( "x-TenantId", _configuration["Entra:TenantId"]!.ToString() );
        SetResponseHeaderValue( "x-ClientId", _configuration["Entra:ClientId"]!.ToString() );
        SetResponseHeaderValue( "x-Domain", _configuration["Entra:Domain"]!.ToString());
        SetResponseHeaderValue( "x-RemoteIpAddress", GetRemoteIpAddress() );
        SetResponseHeaderValue( "x-RequestHostName", GetRequestHostName() );
        List<string> acceptedAuds = _configuration.GetSection("Entra:AcceptedAuds").Get<List<string>>() ?? new List<string>();
        SetResponseHeaderValue("x-AcceptedAuds", string.Join(",", acceptedAuds));
        List<string> acceptedSigninAppIDs = _configuration.GetSection("Entra:AcceptedSigninAppIDs").Get<List<string>>() ?? new List<string>();        
        SetResponseHeaderValue("x-AcceptedSigninAppIDs", string.Join(",", acceptedSigninAppIDs));
        if ( null != KeyVaultHelper.Certificate ) {
            SetResponseHeaderValue("x-CertSubject", KeyVaultHelper.Certificate!.Subject);
            SetResponseHeaderValue("x-CertThumbprint", KeyVaultHelper.Certificate!.Thumbprint);
            SetResponseHeaderValue("x-CertNotAfter", KeyVaultHelper.Certificate!.NotAfter.ToUniversalTime().ToString("O") );
        }
        return new ContentResult { ContentType = "text/plain", Content = "pong - " + DateTime.UtcNow.ToString() };
    }

    /// <summary>
    /// Handles attribute collection step, before the CIAM attribute collection page renders
    /// </summary>
    /// <param name="request"></param>
    /// <returns>Response with prefilled values of type microsoft.graph.onAttributeCollectionStartAuthenticationEventResponseData</returns>
    [Authorize]
    [HttpPost( "/api/authenticationevent/attributeCollectionStart" )]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AuthenticationEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AttributeCollectionStart( [FromBody] AuthenticationEventRequest? request) {
        return await AuthenticationEventHandler( request );
    }

    /// <summary>
    /// Handles step after the user enters and submits attributes.
    /// </summary>
    /// <param name="request"></param>
    /// <returns>Returns with type microsoft.graph.onAttributeCollectionSubmitAuthenticationEventResponseData</returns>
    [Authorize]
    [HttpPost( "/api/authenticationevent/attributeCollectionSubmit" )]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AuthenticationEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AttributeCollectionSubmit( [FromBody] AuthenticationEventRequest? request) {
        return await AuthenticationEventHandler( request );
    }

    /// <summary>
    /// Handles step where Entra is abbout to issue the token and custom claims should be added
    /// </summary>
    /// <param name="request"></param>
    /// <returns>Returns type microsoft.graph.onTokenIssuanceStartResponseData with provideClaimsForToken</returns>
    [Authorize]
    [HttpPost( "/api/authenticationevent/tokenIssuanceStart" )]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AuthenticationEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> TokenIssuanceStart( [FromBody] AuthenticationEventRequest? request) {
        return await AuthenticationEventHandler( request );
    }

    /// <summary>
    /// Handles sending OTP via custom email or SMS
    /// </summary>
    /// <param name="request"></param>
    /// <returns>Returns type microsoft.graph.OnOtpSendResponseData and continueWithDefaultBehavior</returns>
    [Authorize]
    [HttpPost( "/api/authenticationevent/emailOtpSend" )]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AuthenticationEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> EmailOtpSend( [FromBody] AuthenticationEventRequest? request) {
        return await AuthenticationEventHandler( request );
    }

    /// <summary>
    /// Handles password migration
    /// </summary>
    /// <param name="request"></param>
    /// <returns>Returns type microsoft.graph.onPasswordSubmitResponseData</returns>
    [Authorize]
    [HttpPost("/api/authenticationevent/passwordmigration")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AuthenticationEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> PasswordMigration([FromBody] AuthenticationEventRequest? request) {
        if ( null != KeyVaultHelper.Certificate) {
            return await AuthenticationEventHandler(request);
        } else {
            return ReturnErrorMessage("Password Migration not supported");
        }
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Internal endpoint handlers
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private async Task<ActionResult> AuthenticationEventHandler( AuthenticationEventRequest? request) {
        TraceHttpRequest();
        Stopwatch sw = Stopwatch.StartNew();
        try {
            if ( null == request || null == request!.data || null == request!.data!.authenticationContext) {
                _log.LogError($"Error: invalid request. {sw.ElapsedMilliseconds} ms ({_rndValue})");
                return ReturnErrorMessage("Invalid request");
            }
            string body = JsonSerializer.Serialize(request);
            _log.LogTrace("Request body: " + body);
            // This Custom Authentication Extension can be used to sign in to multiple applications in the
            // same CIAM tenant, so we need to check that the app passed is a valid one
            if ( !ValidateSigninAppID( request!, out string? errorMessage )) {
                _log.LogError($"Error: {errorMessage}. {sw.ElapsedMilliseconds} ms ({_rndValue})");
                return ReturnErrorMessage(errorMessage!);
            }

            AuthenticationEventResponse? response = null;
            switch (request.type) {
                case "microsoft.graph.authenticationEvent.attributeCollectionStart":
                    response = InternalAttributeCollectionStart( request! );
                    break;
                case "microsoft.graph.authenticationEvent.attributeCollectionSubmit":
                    response = InternalAttributeCollectionSubmit( request! );
                    break;
                case "microsoft.graph.authenticationEvent.tokenIssuanceStart":
                    response = InternalTokenIssuanceStart( request! );
                    break;
                case "microsoft.graph.authenticationEvent.emailOtpSend":
                    bool o365MailEnabled = _configuration.GetValue<bool>( "O365Mail:Enabled", false );
                    if (!o365MailEnabled) {
                        return ReturnErrorMessage( "emailOtpSend disabled" );
                    }
                    response = OtpSend( request );
                    break;
                case "microsoft.graph.authenticationEvent.passwordSubmit":
                    response = InternalPasswordMigration( request! );
                    break;
                default:
                    _log.LogTrace( $"Unknown type: {request.type}\n{sw.ElapsedMilliseconds} ms" );
                    return ReturnErrorMessage( $"Unknown type: {request.type}" );
            }
            string responseBody = AuthenticationEventResponse.SerializeToJson( response );
            _log.LogTrace( $"Response: {sw.ElapsedMilliseconds} ms ({_rndValue})\n{responseBody}" );
            return ReturnJson( responseBody );
        } catch (Exception ex) {            
            _log.LogError( $"Exception: {ex.Message}. {sw.ElapsedMilliseconds} ms ({_rndValue})\n{ex.StackTrace}" );
            return ReturnErrorMessage( ex.Message );
        }
    }

    private AuthenticationEventResponse InternalAttributeCollectionStart( AuthenticationEventRequest request ) {
        // resolve coutry name from user's ip address
        if (request.data!.userSignUpInfo!.attributes!.ContainsKey("country") ) {
            Stopwatch sw = Stopwatch.StartNew();
            IPInfo? ipinfo = IPInfo.Get( request.data.authenticationContext!.client!.ip! );
            _log.LogTrace($"Get IPInfo: {sw.ElapsedMilliseconds} ms" );
            sw.Stop();
            if (ipinfo != null) {
                RegionInfo ri = new RegionInfo( ipinfo.countryCode! );
                Dictionary<string, object> inputs = new Dictionary<string, object>() { { "country", ri.EnglishName } };
                return AuthenticationEventResponse.SetPrefilledValues( AuthenticationEvent.AttributeCollectionStart, inputs );
            }        
        }
        return AuthenticationEventResponse.ContinueWithDefaultBehaviourResponse( AuthenticationEvent.AttributeCollectionStart );
    }
    private AuthenticationEventResponse InternalAttributeCollectionSubmit( AuthenticationEventRequest request ) {
        // we don't do anything here, just accept whatever user entered
        return AuthenticationEventResponse.ContinueWithDefaultBehaviourResponse( AuthenticationEvent.AttributeCollectionSubmit );
    }
    private AuthenticationEventResponse InternalTokenIssuanceStart( AuthenticationEventRequest request ) {
        // if you return a value of type int or boolean, you will get error 1003003
        // you can only return string values
        Dictionary<string, object> claims = new Dictionary<string, object>();
        claims.Add( "dateOfBirth", "1980-01-01" );
        claims.Add( "customRoles", new List<string>() { "Moderator", "Writer", "Reader" } );
        claims.Add( "memberSince", request.data!.authenticationContext!.user!.createdDateTime!.Value.ToString("yyyy-MM-dd") );
        return AuthenticationEventResponse.ProvideClaimsForTokenResponse( AuthenticationEvent.TokenIssuanceStart, claims );
    }
    private AuthenticationEventResponse OtpSend( AuthenticationEventRequest request ) {
        string receiver = request.data!.otpContext!.Identifier!;
        string appName = request.data!.authenticationContext!.clientServicePrincipal!.appDisplayName!;
        if (receiver.Contains("@") ) {            
            if ( !GetConfigBool("O365Mail:Enabled", false) ) {
                throw new Exception("Sending OTP via email is not enabled by custom authentication extension");
            }
            string emailSubject = _configuration["O365Mail:EmailSubject"]!.ToString();
            emailSubject = emailSubject.Replace("{{APP}}", appName);
            // email body is cached at startup in Program.cs 
            string emailBody = (string)_cache!.Get("emailOtpTemplate")!;
            emailBody = emailBody!.Replace("{{APP}}", appName);
            emailBody = emailBody!.Replace("{{OTP}}", request.data.otpContext.OneTimeCode);
            emailBody = emailBody!.Replace("{{TTL}}", _configuration["O365Mail:OtpTtlMinutes"]!.ToString());
            _log.LogTrace( $"Sending OTP {request.data.otpContext.OneTimeCode} to email {receiver}" );
            _graphMail.SendGraphEmailAsync(null, receiver, emailSubject, emailBody);
        }
        if (receiver.StartsWith("+")) {
            if (!GetConfigBool("Twilio:Enabled", false)) {
                throw new Exception("Sending OTP via SMS is not enabled by custom authentication extension");
            }
            string smsBody = $"{appName} one-time code: {request.data.otpContext.OneTimeCode}";
            _log.LogTrace( $"Sending OTP {request.data.otpContext.OneTimeCode} via SMS to {receiver}" );
            _twilio.SendSMSTextMessage( receiver, smsBody );
        }
        return AuthenticationEventResponse.ContinueWithDefaultBehaviourResponse( AuthenticationEvent.OtpSend );
    }
    private AuthenticationEventResponse InternalPasswordMigration( AuthenticationEventRequest request) {        
        string encryptedPasswordContext = request.data!.encryptedPasswordContext!;
        string? decryptedPayload = _kv.DecryptRsa( encryptedPasswordContext! );
        string? jsonPayload = _kv.DecodeRsa( decryptedPayload! );
        JsonNode? payload = JsonNode.Parse(jsonPayload!);
        string? username = payload!["username"]?.ToString();
        string? password = payload!["user-password"]?.ToString();
        string? nonce = payload["nonce"]?.ToString();
        PasswordMigrateResponse passwordMigrateResponse = PasswordMigrateResponse.PasswordIncorrect;
        Auth0Response? auth0Response = _auth0.Authenticate( username!, password! );
        if (null != auth0Response) {
            // short or not containing upper/lower/special
            if (password!.Length <= 8 || !(password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(c => !char.IsLetterOrDigit(c)))) {
                passwordMigrateResponse = PasswordMigrateResponse.MigrateWeakPassword;
            } else {
                passwordMigrateResponse = PasswordMigrateResponse.MigratePassword;
            }
        }
        return AuthenticationEventResponse.PasswordSubmitResponse(AuthenticationEvent.PasswordSubmit, passwordMigrateResponse, nonce! );
    }

} // cls