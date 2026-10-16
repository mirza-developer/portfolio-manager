using PortfolioManager.Web.Security;

namespace PortfolioManager.Web.Services;

internal static class Guard
{
    public static void RequireAdmin(UserContext user)
    {
        if (!user.IsAdmin) throw new UnauthorizedAccessException("Only an administrator can do this.");
    }

    public static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
