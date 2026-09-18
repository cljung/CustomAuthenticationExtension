using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using CustomAuthenticationExtension.Helpers;

var builder = WebApplication.CreateBuilder(args);

ILogger<Program>? logger = null;

builder.Services.Configure<ForwardedHeadersOptions>(options => {
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
});

List<string> acceptedAuds = GetConfigList("Entra:AcceptedAuds");
List<string> acceptedSigninAppIDs = GetConfigList("Entra:AcceptedSigninAppIDs");
string tenantId = GetConfigValue("Entra:TenantId")!;
string instance = GetConfigValue("Entra:Instance")!.Replace( "{tenantId}", tenantId );
string domain = GetConfigValue("Entra:Domain", tenantId)!;
string clientId = GetConfigValue("Entra:ClientID")!;

// create variable here so we can modify it further down if we should use TokenDecryption and support JWE tokens
TokenValidationParameters tokenValidationParams = new TokenValidationParameters() {
    ValidateIssuerSigningKey = true,
    ValidateIssuer = true,
    ValidIssuer = $"{instance}{tenantId}/v2.0",
    ValidateLifetime = true,
    RequireExpirationTime = true,
    AudienceValidator = CustomAudienceValidator
};

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(
        jwtOptions => {
            jwtOptions.TokenValidationParameters = tokenValidationParams;
        }, 
        identityOptions => {
            identityOptions.Domain = domain;
            identityOptions.ClientId = clientId;
            identityOptions.TenantId = tenantId;
            identityOptions.Instance = instance;
            identityOptions.AllowWebApiToBeAuthorizedByACL = true; // w/o this, token must have scp/roles - which is hasn't
        },
        JwtBearerDefaults.AuthenticationScheme,
        subscribeToJwtBearerMiddlewareDiagnosticsEvents: true
    );

builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.Configure<KestrelServerOptions>( options => { options.AllowSynchronousIO = true; } );
builder.Services.Configure<IISServerOptions>( options => { options.AllowSynchronousIO = true;} );

builder.Services.AddSingleton<IKeyVaultHelper, KeyVaultHelper>();   // handles JWE decrypt in API for password migration
builder.Services.AddSingleton<IAuth0Helper, Auth0Helper>();         // handles remote password migration validation
builder.Services.AddSingleton<IGraphMail, GraphMail>();             // handles sending OTP via email
builder.Services.AddSingleton<ITwilioHelper, TwilioHelper>();       // handles senting OTP via SMS

if (GetConfigBool("ApplicationInsights:Enabled", false)) {
    builder.Services.AddApplicationInsightsTelemetry();
    builder.Services.AddLogging( logBuilder => logBuilder.AddApplicationInsights().AddFilter<ApplicationInsightsLoggerProvider>( "", LogLevel.Trace ) );
}

var app = builder.Build();

logger = app.Services.GetRequiredService<ILogger<Program>>();
var cache = app.Services.GetRequiredService<IMemoryCache>();

// trace current config
logger.LogInformation( $"*** Configuration ***\n" 
        + $"TenantId: {tenantId}\nDomain: {domain}\nInstance: {instance}\nClientID: {clientId}\n" 
        + $"Accepted aud(s): {string.Join(",", acceptedAuds)}\nAccepted Signin AppIDs: {string.Join(",", acceptedSigninAppIDs)}" );

if (GetConfigBool("KeyVault:Enabled", false)) {
    var kvh = new KeyVaultHelper(builder.Configuration, app.Services.GetRequiredService<ILogger<IKeyVaultHelper>>());
    kvh.LoadCertificateFromKeyVault();
    // Once you add password migration and configure tokenEncryptionId in the app manifest
    // you will get an encrypted access token. In fact, it's not a JWT but a JWE.
    // We need to add the cert here so the aspnet middleware can decrypt it 
    if (null != KeyVaultHelper.Certificate) {
        tokenValidationParams.TokenDecryptionKey = new X509SecurityKey(KeyVaultHelper.Certificate);
        logger!.LogTrace($"TokenDecriptionKey set to certificate. JWE encrypted tokens supported");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
#if DEBUG
    IdentityModelEventSource.ShowPII = true;
#endif
    app.UseSwagger();
    app.UseSwaggerUI();
} else {
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// load and cache HTML file for sending OTP via email
if ( GetConfigBool( "O365Mail:Enabled", false ) ) {
    try {
        string path = System.IO.Path.Combine(builder.Environment.ContentRootPath, GetConfigValue("O365Mail:EmailTemplate")!);
        string fileContent = System.IO.File.ReadAllText(path);
        cache.Set("emailOtpTemplate", fileContent);
        logger.LogInformation($"Pre-loaded email template from: {path}. Email will be sent from: {GetConfigValue("O365Mail:SenderEmail")}");
    } catch (Exception ex) {
        logger.LogError("Failed to load email template file: " + ex.Message);
    }
}
if (GetConfigBool("Twilio:Enabled", false)) {
    logger.LogInformation($"OTP via SMS will be sent from: {GetConfigValue("Twilio:Number")}");
}

app.Run();


// This extension is built to serve multiple apps, so therefor we can have a list of accepted 'aud' claims that are valid
bool CustomAudienceValidator( IEnumerable<string> audiences, SecurityToken securityToken, TokenValidationParameters validationParameters ) {
    var castedToken = securityToken as JwtSecurityToken;

#if DEBUG
    // Security concern: you should only trace the access token here for debugging purposes
    logger!.LogTrace( securityToken.ToString() );
#endif
    List<string> intersection = acceptedAuds.Intersect(audiences, StringComparer.OrdinalIgnoreCase).ToList();
    if (intersection.Count > 0) {
        return true;
    }
    foreach ( string tokenAud in audiences ) {
        if ( acceptedAuds.Contains(tokenAud.ToLowerInvariant())) {
            return true;
        }
    }
    logger!.LogTrace( "aud not OK - " + string.Join(",", audiences) );
    return false;
}

List<string> GetConfigList( string key ) {
    return builder.Configuration.GetSection(key).Get<List<string>>() ?? new List<string>();
}
string? GetConfigValue( string key, string? defaultValue = null) {
    string? val = builder.Configuration[key]?.ToString();
    if (null == val && null != defaultValue)
        val = defaultValue;
    return val;
}
bool GetConfigBool( string key, bool defaultValue ) {
    return builder.Configuration.GetValue<bool>( key, defaultValue );
}