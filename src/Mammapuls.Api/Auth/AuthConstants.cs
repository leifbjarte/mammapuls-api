namespace Mammapuls.Api.Auth;

public static class AuthConstants
{
    public const string VippsScheme = "Vipps";
    public const string CallbackPath = "/signin-vipps";
    public const string CookieName = "__Host-mammapuls";

    public const string UserIdClaim = "uid";
    public const string AuthTimeClaim = "auth_time";

    public const string CsrfHeaderName = "X-Requested-With";

    public const string DataProtectionConnectionName = "dataprotection";
}
