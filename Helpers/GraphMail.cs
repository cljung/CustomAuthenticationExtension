using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Graph.Users.Item.SendMail;

namespace CustomAuthenticationExtension.Helpers; 

public interface IGraphMail {
    public Task<bool> SendGraphEmailAsync(string? fromEmail, string toEmail, string subject, string body);
}
public class GraphMail : IGraphMail {
    private readonly IConfiguration _configuration;
    private readonly ILogger<GraphMail> _log;
    private readonly string? _tenantId;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly string? _senderEmail;
    private static bool _enabled = false;
    public GraphMail(IConfiguration configuration, ILogger<GraphMail> log) {
        _configuration = configuration;
        _log = log;
        _enabled = configuration.GetValue<bool>("O365Mail:Enabled", false);
        if (!_enabled) {
            _log.LogInformation("O365Mail is not enabled");
            return;
        }
        _tenantId = configuration.GetValue<string>("O365Mail:TenantId", "")!.ToString();
        if (string.IsNullOrWhiteSpace(_tenantId)) _tenantId = configuration["Entra.Workforce:TenantId"]!.ToString();
        _clientId = configuration.GetValue<string>("O365Mail:ClientId", "")!.ToString();
        if (string.IsNullOrWhiteSpace(_clientId)) _clientId = configuration["Entra.Workforce:ClientId"]!.ToString();
        _clientSecret = configuration.GetValue<string>("O365Mail:ClientSecret", "")!.ToString();
        if (string.IsNullOrWhiteSpace(_clientSecret)) _clientSecret = configuration["Entra.Workforce:ClientSecret"]!.ToString();
        _senderEmail = configuration["O365Mail:SenderEmail"]!.ToString();
        _log.LogInformation("O365Mail is enabled");
    }
    public async Task<bool> SendGraphEmailAsync(string? fromEmail, string toEmail, string subject, string body) {
        var scopes = new[] { "https://graph.microsoft.com/.default" };
        var credential = new ClientSecretCredential(_tenantId, _clientId, _clientSecret);
        var graphClient = new GraphServiceClient(credential, scopes);

        if (string.IsNullOrEmpty(fromEmail)) {
            fromEmail = _senderEmail;
        }
        var requestBody = new SendMailPostRequestBody {
            Message = new Message {
                Subject = subject,
                Body = new ItemBody { ContentType = BodyType.Html, Content = body },
                ToRecipients = new List<Recipient> { new Recipient { EmailAddress = new EmailAddress { Address = toEmail } } }
            },
            SaveToSentItems = true
        };

        try {
            await graphClient.Users[fromEmail].SendMail.PostAsync(requestBody);
            return true;
        } catch (ODataError odataError) {
            _log.LogError($"Graph API Error: {odataError.Error?.Message}");
            return false;
        } catch (Exception ex) {
            _log.LogError($"Error: {ex.Message}");
            return false;
        }
    }
}
