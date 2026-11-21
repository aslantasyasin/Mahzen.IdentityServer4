using System.Text.RegularExpressions;
using IdentityServer.Helper;
using Microsoft.AspNetCore.Http;

namespace IdentityServer.Services.Login;

public static class LoginSource
{
    public const string ClientIdHeader = "X-ClientId";

    private static readonly Regex SafeClientId = new("^[A-Za-z0-9_.:-]{1,128}$", RegexOptions.Compiled);

    public static string Resolve(HttpContext httpContext, string internalApiKey)
    {
        var headers = httpContext?.Request.Headers;
        var clientId = headers?[ClientIdHeader].ToString();
        var provided = headers?[InternalCallerOnlyAttribute.HeaderName].ToString();

        if (!string.IsNullOrEmpty(internalApiKey) &&
            InternalCallerOnlyAttribute.KeysMatch(internalApiKey, provided) &&
            !string.IsNullOrEmpty(clientId) && SafeClientId.IsMatch(clientId))
            return "client:" + clientId;

        return "ip:" + (httpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }
}
