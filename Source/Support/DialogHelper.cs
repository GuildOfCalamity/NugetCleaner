using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NugetCleaner.Support;

public static class DialogHelper
{
    static bool isOpening = false;
    static SemaphoreSlim semaSlim = new SemaphoreSlim(1, 1);

    /// <summary>
    ///   In WinUI 3, ContentDialog does not automatically determine the visual tree or parent UI element, 
    ///   unlike in earlier XAML-based frameworks. Setting XamlRoot explicitly is mandatory.
    ///   [Behavior in VS2022 Debugger vs. Native Assembly]
    ///   Visual Studio often masks threading issues or exceptions by ensuring a UI context, 
    ///   which is why the issue might only manifest when running outside the debugger.
    /// </summary>
    /// <param name="dialog"><see cref="ContentDialog"/></param>
    /// <param name="element"><see cref="FrameworkElement"/></param>
    /// <returns><see cref="ContentDialogResult"/></returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog, FrameworkElement element)
    {
        ContentDialogResult dialogResult = ContentDialogResult.None;
        try
        {
            await semaSlim.WaitAsync();
            if (!isOpening && dialog is not null && element is not null)
            {
                isOpening = true;
                if (dialog.XamlRoot is null)
                    dialog.XamlRoot = element.XamlRoot;
                dialog.RequestedTheme = element.ActualTheme;
                element.ActualThemeChanged += (sender, args) => { dialog.RequestedTheme = element.ActualTheme; };
                dialog.RequestedTheme = App.MainRoot?.ActualTheme ?? ElementTheme.Default;
                //dialogResult = await dialog.ShowAsync(ContentDialogPlacement.InPlace);
                dialogResult = await dialog.ShowAsync(ContentDialogPlacement.Popup);
                isOpening = false;
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ERROR] DialogHelper.ShowAsync: {ex.Message}"); }
        finally { semaSlim.Release(); }
        return dialogResult;
    }

    /// <summary>
    ///   Shows a <see cref="ContentDialog"/>.
    /// </summary>
    /// <param name="dialog"><see cref="ContentDialog"/></param>
    /// <param name="element"><see cref="FrameworkElement"/></param>
    /// <returns><see cref="ContentDialogResult"/></returns>
    public static ContentDialogResult ShowAsTaskAsync(ContentDialog dialog, FrameworkElement element)
    {
        ContentDialogResult dialogResult = ContentDialogResult.None;
        if (!isOpening && dialog is not null && element is not null)
        {
            isOpening = true;
            // Ensure the code runs on the UI thread!
            bool enqueued = element.DispatcherQueue.TryEnqueue(() =>
            {
                if (dialog.XamlRoot is null)
                    dialog.XamlRoot = element.XamlRoot;
                dialog.RequestedTheme = element.ActualTheme;
                element.ActualThemeChanged += (sender, args) => { dialog.RequestedTheme = element.ActualTheme; };
                dialog.ShowAsync().AsTask().ContinueWith(t =>
                {
                    if (t.Exception != null)
                        App.DebugLog($"[ERROR] ShowAsyncAlt: {t.Exception.Message}");
                    else
                        dialogResult = t.Result;

                    // Mark as not opening once the dialog is closed
                    isOpening = false;
                });
            });

            if (!enqueued)
            {
                App.DebugLog("[ERROR] DispatcherQueue.TryEnqueue failed. Unable to show ContentDialog.");
                isOpening = false;
            }
        }
        return dialogResult;
    }
}
