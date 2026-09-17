using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Jose;

namespace CustomAuthenticationExtension.Helpers;

public interface IKeyVaultHelper {
    public void LoadCertificateFromKeyVault();
    public string? DecryptRsa(string encryptedToken);
    public string? DecodeRsa(string token);
    public string? Decrypt( string encryptedText );
    public string? Encrypt( string plainText );
}

public class KeyVaultHelper : IKeyVaultHelper {
    private readonly ILogger<IKeyVaultHelper>? _log;
    private static ClientSecretCredential? _clientSecretCredential = null;
    public static X509Certificate2? Certificate { get; set; } = null;
    private static System.Security.Cryptography.RSA? _rsaPrivate = null;
    private static System.Security.Cryptography.RSA? _rsaPublic = null;
    private static bool _enabled = false; 
    private static string _tenantId = "";
    private static string _clientId = "";
    private static string _clientSecret = "";
    private static string _keyVaultUrl = "";
    private static string _certificateName = "";
    public KeyVaultHelper( IConfiguration configuration, ILogger<IKeyVaultHelper>? log = null ) {
        _log = log;
        if (null == _clientSecretCredential) {
            _enabled = configuration.GetValue<bool>("KeyVault:Enabled", false);
            if (!_enabled) {
                InternalTrace( LogLevel.Information, "KeyVaultHelper is not enabled");
            }
            _tenantId = configuration.GetValue<string>("KeyVault:TenantId", "")!.ToString();
            if (string.IsNullOrWhiteSpace(_tenantId)) _tenantId = configuration["Entra.Workforce:TenantId"]!.ToString();
            _clientId = configuration.GetValue<string>("KeyVault:ClientId", "")!.ToString();
            if ( string.IsNullOrWhiteSpace(_clientId) ) _clientId = configuration["Entra.Workforce:ClientId"]!.ToString();
            _clientSecret = configuration.GetValue<string>("KeyVault:ClientSecret", "")!.ToString();
            if (string.IsNullOrWhiteSpace(_clientSecret)) _clientSecret = configuration["Entra.Workforce:ClientSecret"]!.ToString();
            _clientSecretCredential = new ClientSecretCredential(_tenantId, _clientId, _clientSecret);
            _keyVaultUrl = configuration["KeyVault:KeyVaultUrl"]!.ToString();
            _certificateName = configuration["KeyVault:CertificateName"]!.ToString();
            InternalTrace( LogLevel.Information, "KeyVaultHelper is enabled");

        }
    }
    private void InternalTrace( LogLevel level, string message ) {
        if (null == _log) {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write(level.ToString().Substring(0, 4).ToLowerInvariant());
            Console.ResetColor();
            Console.WriteLine( $": {message}");
        } else {
            switch( level ) {
                case LogLevel.Critical: _log.LogCritical(message); break;
                case LogLevel.Error: _log.LogError(message); break;
                case LogLevel.Warning: _log.LogWarning(message); break;
                case LogLevel.Information: _log.LogInformation(message); break;
                case LogLevel.Trace: _log.LogTrace(message); break;
                case LogLevel.Debug: _log.LogDebug(message); break;
            }
        }
    }
    public void LoadCertificateFromKeyVault() {
        if (!_enabled) return;
        if (null != _rsaPrivate) return;
        var secretClient = new SecretClient(new Uri(_keyVaultUrl), _clientSecretCredential);
        KeyVaultSecret secret = secretClient.GetSecret(_certificateName);
        if (string.IsNullOrEmpty(secret.Value)) return;
        // Secret value is base64-encoded certificate (PFX/PKCS12)
        byte[] certBytes = Convert.FromBase64String(secret.Value);
        // Load certificate with private key
        Certificate = new X509Certificate2( certBytes, string.Empty, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable );
        InternalTrace( LogLevel.Trace, $"Certificate {_certificateName} loaded. Thumbprint: {Certificate.Thumbprint}. Valid between {Certificate.NotBefore.ToUniversalTime().ToString("o")} - {Certificate.NotAfter.ToUniversalTime().ToString("o")}");
        if (!Certificate.HasPrivateKey) return;
        _rsaPrivate = Certificate.GetRSAPrivateKey();
        _rsaPublic = Certificate.GetRSAPublicKey();
    }
    public string? DecryptRsa( string encryptedToken) {
        if (!_enabled) return null;
        ArgumentNullException.ThrowIfNull(_rsaPrivate);
        return JWT.Decrypt(encryptedToken, _rsaPrivate);
    }
    public string? DecodeRsa(string token) {
        if (!_enabled) return null;
        return Jose.JWT.Decode(token, null, JwsAlgorithm.none);
    }
    public string? Decrypt( string encryptedText) {
        if (!_enabled) return null;
        ArgumentNullException.ThrowIfNull( _rsaPrivate );
        byte[] cipherBytes = Convert.FromBase64String(encryptedText);
        byte[] plainBytes = _rsaPrivate.Decrypt(cipherBytes, RSAEncryptionPadding.OaepSHA256 );
        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
    public string? Encrypt( string plainText ) {
        if (!_enabled) return null;
        ArgumentNullException.ThrowIfNull( _rsaPublic );
        byte[] plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = _rsaPublic!.Encrypt(plainBytes, RSAEncryptionPadding.OaepSHA256);
        return Convert.ToBase64String(cipherBytes);
    }
}
