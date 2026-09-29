namespace Mammapuls.Api.Auth;

/// <summary>Open-redirect guard: only local paths or URLs on an allowlisted SPA origin.</summary>
public static class ReturnUrl
{
    public static bool IsAllowed(string? url, IReadOnlyCollection<string> allowedOrigins)
    {
        if (string.IsNullOrEmpty(url) || url.Any(char.IsControl))
        {
            return false;
        }

        if (url[0] == '/')
        {
            return url.Length == 1 || (url[1] != '/' && url[1] != '\\');
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && string.IsNullOrEmpty(uri.UserInfo)
            && allowedOrigins.Any(origin => string.Equals(
                origin.TrimEnd('/'), uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase));
    }

    public static string Default(IReadOnlyList<string> allowedOrigins) =>
        allowedOrigins.Count > 0 ? allowedOrigins[0] : "/";
}
