using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Configuration;

Console.WriteLine("GuardianAutomation: a demo of using API Bungie.net...");

var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

string clientId = config["Bungie:ClientId"]; // From Bungie.net portal
string apiKey = config["Bungie:ApiKey"]; // From Bungie.net portal
string redirectUri = config["Bungie:RedirectUri"]; // Specified by me on Bungie.net portal

Console.WriteLine("Getting authorization code...");
string code = await GetAuthorizationCodeAsync(clientId, redirectUri);

Console.WriteLine("Getting access token...");
using var client = new HttpClient();
string accessToken = await GetAccessTokenAsync(client, clientId, apiKey, redirectUri, code);

// When it's time to make an API request...:
Console.WriteLine("Attempting to call API 'GetMembershipsForCurrentUser'...");
client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
var result = await client.GetAsync("https://www.bungie.net/Platform/User/GetMembershipsForCurrentUser/");
string json = await result.Content.ReadAsStringAsync();
Console.WriteLine(json);

// Now for "Feature 1" in my Gemini collab project: find endpoint for listing all applications/clients.

static async Task<string> GetAuthorizationCodeAsync(string clientId, string redirectUri)
{
    // Intercept the redirect
    HttpListener listener = new HttpListener();
    listener.Prefixes.Add(redirectUri);
    listener.Start();

    // Prevent CSRF, start the auth request
    string state = Guid.NewGuid().ToString();
    string authUrl = $"https://www.bungie.net/en/OAuth/Authorize?client_id={clientId}&response_type=code&state={state}&redirect_uri={Uri.EscapeDataString(redirectUri)}";
    Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });

    HttpListenerContext context = await listener.GetContextAsync();
    string code = HttpUtility.ParseQueryString(context.Request.Url.Query).Get("code");
    string returnedState = HttpUtility.ParseQueryString(context.Request.Url.Query).Get("state");
    listener.Stop();

    // Validate state to prevent CSRF
    if (returnedState != state) throw new Exception("State mismatch");
    return code;
}

static async Task<string> GetAccessTokenAsync(HttpClient client, string clientId, string apiKey, string redirectUri, string code)
{
    // Exchange the 'code' for the Access Token
    client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

    var values = new Dictionary<string, string>
    {
        { "grant_type", "authorization_code" },
        { "code", code },
        { "client_id", clientId },
        { "redirect_uri", redirectUri }
    };

    var content = new FormUrlEncodedContent(values);
    var response = await client.PostAsync("https://www.bungie.net/platform/app/oauth/token/", content);
    string responseBody = await response.Content.ReadAsStringAsync();
    var tokenData = JsonSerializer.Deserialize<Dictionary<string, object>>(responseBody);
    string accessToken = tokenData["access_token"].ToString();
    return accessToken;
}
