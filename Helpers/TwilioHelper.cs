using System.Net;

namespace CustomAuthenticationExtension.Helpers;

public interface ITwilioHelper {
    public Task<bool> SendSMSTextMessage(string toNumber, string message);
}
public class TwilioHelper : ITwilioHelper {
    private string? _twilioSid;
    private string? _twilioToken;
    private string? _fromNumber;
    private string? _basicToken;
    public TwilioHelper ( IConfiguration configuration ) {
        _twilioSid = configuration["Twilio:Sid"]!.ToString();
        _twilioToken = configuration["Twilio:Token"]!.ToString();
        _fromNumber = configuration["Twilio:Number"]!.ToString();
        _basicToken = System.Convert.ToBase64String( System.Text.ASCIIEncoding.ASCII.GetBytes( _twilioSid + ":" + _twilioToken ) );
    }

    public async Task<bool> SendSMSTextMessage( string toNumber, string message) {
        bool rc = false;
        var keyValues = new List<KeyValuePair<string, string>>();
        keyValues.Add(new KeyValuePair<string, string>("From", _fromNumber! ));
        keyValues.Add(new KeyValuePair<string, string>("To", toNumber));
        keyValues.Add(new KeyValuePair<string, string>("Body", message));
        using ( var client = new HttpClient() ) {
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + _basicToken );
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twilio.com/2010-04-01/Accounts/" + _twilioSid + "/Messages.json");
            request.Content = new FormUrlEncodedContent(keyValues);
            var res = client.Send(request);
            rc = (res.StatusCode == HttpStatusCode.Created);
        }
        return rc;
    }
}
