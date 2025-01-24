using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NugetCleaner.Dialogs;

public sealed partial class AboutDialog : ContentDialog
{
    public AboutDialog()
    {
        this.InitializeComponent();
        this.Loaded += AboutDialogOnLoaded;
        this.Unloaded += AboutDialogOnUnloaded;
    }

    void AboutDialogOnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!App.IsClosing)
            StoryboardSpin?.Stop();
    }

    void AboutDialogOnLoaded(object sender, RoutedEventArgs e)
    {
        if (!App.IsClosing)
            StoryboardSpin?.Begin();
    }
}
