using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mammapuls.Api.Auth;

/// <summary>
/// Redeems the authorization code with client_secret_basic (the Vipps default);
/// the ASP.NET Core handler would otherwise send the secret in the form body (client_secret_post).
/// </summary>
internal static class VippsTokenClient
{
    public static async Task RedeemWithClientSecretBasicAsync(AuthorizationCodeReceivedContext context)
    {
        var cancellationToken = context.HttpContext.RequestAborted;
        var request = context.TokenEndpointRequest
            ?? throw new InvalidOperationException("Token endpoint request was not created.");
        var configuration = await context.Options.ConfigurationManager!.GetConfigurationAsync(cancellationToken);

        // Vipps documents base64(client_id:client_secret) without the RFC 6749 form-encoding step.
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{context.Options.ClientId}:{context.Options.ClientSecret}"));
        request.ClientId = null;
        request.ClientSecret = null;

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(request.Parameters),
        };
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await context.Backchannel.SendAsync(tokenRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            context.Fail($"Vipps token endpoint returned {(int)response.StatusCode}.");
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        context.HandleCodeRedemption(new OpenIdConnectMessage(body));
    }
}
