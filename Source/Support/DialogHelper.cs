using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NugetCleaner.Support;

public static class DialogHelper
{
    static bool isOpening = false;

    public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog, FrameworkElement element)
    {
        ContentDialogResult dialogResult = ContentDialogResult.None;
        if (!isOpening && dialog is not null && element is not null)
        {
            isOpening = true;
            dialog.XamlRoot = element.XamlRoot;
            dialog.RequestedTheme = element.ActualTheme;
            element.ActualThemeChanged += (sender, args) =>
            {
                dialog.RequestedTheme = element.ActualTheme;
            };
            dialogResult = await dialog.ShowAsync();
            isOpening = false;
        }
        return dialogResult;
    }
}
