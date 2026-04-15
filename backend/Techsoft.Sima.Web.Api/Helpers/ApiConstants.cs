using Asp.Versioning;
using System.Reflection;

namespace Techsoft.Sima.Web.Api.Helpers;

public static class ApiConstants
{
    public const string APP_NAME = "Sima Web";
    public static readonly string ASSEMBLY_VERSION =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    public const string ALLOW_ALL_CORS_POLICY = "AllowAll";
    public const string FIXED_RATE_LIMITING_POLICY = "Fixed";

    public const int MIN_PAGE_SIZE = 1;
    public const int MAX_PAGE_SIZE = 100;

    private static readonly ApiVersion _version1 = new(1, 0);
    public static ApiVersion VERSION_1 => _version1;

    public static class Claims
    {
        public const string ROLES = "roles";
        public const string EMAIL = "preferred_username";
        public const string NAME = "name";
    }

    public static class Roles
    {
        public const string SUPERUSUARIO = "superusuario";
        public const string ADMINISTRADOR = "administrador";
        public const string LIDER = "lider";
        public const string USUARIO = "usuario";
    }
}
