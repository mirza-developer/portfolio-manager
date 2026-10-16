using MudBlazor;

namespace PortfolioManager.Web.Components.Shared;

/// <summary>The one place that asks 'are you sure?'. Every delete goes through it.</summary>
public sealed class ConfirmService(IDialogService dialogs)
{
    public async Task<bool> AskAsync(string title, string message) =>
        await dialogs.ShowMessageBox(title, message, yesText: "Yes", cancelText: "Cancel") == true;
}
