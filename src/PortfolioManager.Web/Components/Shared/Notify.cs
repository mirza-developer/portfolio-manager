using MudBlazor;

namespace PortfolioManager.Web.Components.Shared;

public static class Notify
{
    public static void Success(this ISnackbar snackbar, string message) => snackbar.Add(message, Severity.Success);

    /// <summary>Business-rule errors show their message; anything unexpected shows a generic text.</summary>
    public static void Error(this ISnackbar snackbar, Exception ex) => snackbar.Add(
        ex is InvalidOperationException or UnauthorizedAccessException or KeyNotFoundException
            ? ex.Message
            : "Something went wrong. Please try again.",
        Severity.Error);
}
