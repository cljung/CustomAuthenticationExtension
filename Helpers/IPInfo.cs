using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomAuthenticationExtension.Helpers; 
public class IPInfo {
    public string? ip { get; set; }
    public string? city { get; set; }
    public string? region { get; set; }
    public string? regionName { get; set; }
    public string? country { get; set; }
    public string? countryCode { get; set; }
    public string? loc { get; set; }
    public double? lon { get; set; }
    public double? lat { get; set; }
    public string? org { get; set; }
    [JsonPropertyName("as")]
    public string? asn { get; set; }
    public string? postal { get; set; }
    public string? zip { get; set; }
    public string? timezone { get; set; }

    public static IPInfo? Get( string ipaddr ) {
        IPInfo? ipinfo = null;
        HttpClient client = new HttpClient();
        //string url = $"https://ipinfo.io/{ipaddr}";
        string url = $"http://ip-api.com/json/{ipaddr}";
        HttpResponseMessage res = client.GetAsync( url ).Result;
        string response = res.Content.ReadAsStringAsync().Result;
        if (res.IsSuccessStatusCode) {
            ipinfo = JsonSerializer.Deserialize<IPInfo>( response );
        }
        client.Dispose();
        return ipinfo;
    }
}
