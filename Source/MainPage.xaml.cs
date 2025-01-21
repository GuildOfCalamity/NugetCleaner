using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

using NugetCleaner.Support;

namespace NugetCleaner;

/// <summary>
/// NOTE: Be sure last access timestamps are enabled on your system by running 
/// "fsutil behavior query disablelastaccess" using an admin command prompt.
/// The results should report "DisableLastAccess = 2  (System Managed, Disabled)".
/// </summary>
public sealed partial class MainPage : Page
{
    #region [Properties]
    static bool _loaded = false;
    static bool _running = false;
    static bool _reportMode = true;
    static Brush? _lvl1;
    static Brush? _lvl2;
    static Brush? _lvl3;
    static Brush? _lvl4;
    static Brush? _lvl5;
    static CancellationTokenSource _cts = new();
    public string? PackagePath { get; set; }
    public ObservableCollection<TargetItem> LogMessages { get; set; } = new();
    #endregion

    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += MainPageOnLoaded;
        btnRun.PointerEntered += ButtonRunOnPointerEntered;
        btnRun.PointerExited += ButtonRunOnPointerExited;
        ScanEngine.OnTargetAdded += ScanEngineOnTargetAdded;
        ScanEngine.OnScanComplete += ScanEngineOnScanComplete;
        ScanEngine.OnScanError += ScanEngineOnScanError;

        #region [Init MessageLevel Brushes]
        if (App.Current.Resources.TryGetValue("GradientDebugBrush", out object _))
            _lvl1 = (Microsoft.UI.Xaml.Media.Brush)App.Current.Resources["GradientDebugBrush"];
        else
            _lvl1 = new SolidColorBrush(Colors.Gray);

        if (App.Current.Resources.TryGetValue("GradientInfoBrush", out object _))
            _lvl2 = (Microsoft.UI.Xaml.Media.Brush)App.Current.Resources["GradientInfoBrush"];
        else
            _lvl2 = new SolidColorBrush(Colors.DodgerBlue);

        if (App.Current.Resources.TryGetValue("GradientImportantBrush", out object _))
            _lvl3 = (Microsoft.UI.Xaml.Media.Brush)App.Current.Resources["GradientImportantBrush"];
        else
            _lvl3 = new SolidColorBrush(Colors.Yellow);

        if (App.Current.Resources.TryGetValue("GradientWarningBrush", out object _))
            _lvl4 = (Microsoft.UI.Xaml.Media.Brush)App.Current.Resources["GradientWarningBrush"];
        else
            _lvl4 = new SolidColorBrush(Colors.Orange);

        if (App.Current.Resources.TryGetValue("GradientErrorBrush", out object _))
            _lvl5 = (Microsoft.UI.Xaml.Media.Brush)App.Current.Resources["GradientErrorBrush"];
        else
            _lvl5 = new SolidColorBrush(Colors.Red);
        #endregion
    }

    #region [Helpers]

    void UpdateMessage(string msg, MessageLevel level = MessageLevel.Information)
    {
        if (App.IsClosing)
            return;

        _ = tbMessages.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            switch (level)
            {
                case MessageLevel.Debug: tbMessages.Foreground = _lvl1; break;
                case MessageLevel.Information: tbMessages.Foreground = _lvl2; break;
                // Levels above information will trigger the InfoBar.
                case MessageLevel.Important:
                    {
                        tbMessages.Foreground = _lvl3;
                        infoBar.IsOpen = true;
                        infoBar.Message = msg;
                        infoBar.Severity = InfoBarSeverity.Informational;
                        break;
                    }
                case MessageLevel.Warning:
                    {
                        tbMessages.Foreground = _lvl4;
                        infoBar.IsOpen = true;
                        infoBar.Message = msg;
                        infoBar.Severity = InfoBarSeverity.Warning;
                        break;
                    }
                case MessageLevel.Error:
                    {
                        tbMessages.Foreground = _lvl5;
                        infoBar.IsOpen = true;
                        infoBar.Message = msg;
                        infoBar.Severity = InfoBarSeverity.Error;
                        break;
                    }
            }
            tbMessages.Text = msg;
        });
    }

    void AddLogItem(TargetItem ti)
    {
        if (App.IsClosing)
            return;

        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, delegate() 
        { 
            LogMessages.Insert(0, ti); 
        });
    }

    string GetGlobalPackagesFolder()
    {
        var text = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (string.IsNullOrEmpty(text))
        {
            // Try backup technique
            text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
            if (Directory.Exists(text))
                return text;

            UpdateMessage("NUGET_PACKAGES folder was not detected on this machine.", MessageLevel.Error);

            return string.Empty;
        }
        else
        {
            VerifyPathIsRooted("NUGET_PACKAGES", text);
        }

        if (!string.IsNullOrEmpty(text))
        {
            text = text.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return Path.GetFullPath(text);
        }

        return text;
    }

    void VerifyPathIsRooted(string key, string path)
    {
        if (!Path.IsPathRooted(path))
        {
            UpdateMessage("Global packages folder must contain an absolute path.", MessageLevel.Error);
        }
    }

    #endregion

    #region [Events]

    void MainPageOnLoaded(object sender, RoutedEventArgs e)
    {
        sldrDays.Value = App.Profile!.Days;
        _loaded = true;
        PackagePath = tbNugetPath.Text = GetGlobalPackagesFolder();
        if (App.Profile.LastSize > 0)
            UpdateMessage($"Last calculated size was {App.Profile.LastSize.HumanReadableSize()} ({App.Profile.LastCount} items)", MessageLevel.Information);
    }

    void SliderDaysChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loaded)
        {
            App.Profile!.Days = (int)e.NewValue;
            UpdateMessage($"Days changed to {App.Profile.Days}");
        }
    }

    async void RunButtonOnClick(object sender, RoutedEventArgs e)
    {
        #region [check current state]
        if (!_running)
        {
            _cts = new CancellationTokenSource();

            if (!_reportMode)
            {
                await App.ShowDialogBox($"Confirmation", $"This could result is deleted files, they{Environment.NewLine}will not be moved to the recycling bin.{Environment.NewLine}{Environment.NewLine}Are you sure?", "YES", "NO",
                    delegate 
                    {
                        Debug.WriteLine("[INFO] User agrees to proceed.");
                    },
                    delegate 
                    {
                        Debug.WriteLine("[INFO] User canceled the process.");
                        _cts.Cancel();
                    }, 
                    new Uri($"ms-appx:///Assets/WarningIcon.png"));
            }
            LogMessages.Clear();
            btnRun.Content = "Cancel";
            UpdateMessage("Scanning...");
        }
        else
        {
            UpdateMessage("Canceling...");
            _cts?.Cancel();
            btnRun.IsEnabled = false;
            DispatcherQueue?.EnqueueAsync(async () =>
            {
                await Task.Delay(2000);
                btnRun.IsEnabled = true;
                if (_reportMode)
                    btnRun.Content = "Scan Packages";
                else
                    btnRun.Content = "Clean Packages";
            });
            return;
        }
        #endregion

        #region [offload scan in new thread]
        Task.Run(async delegate()
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                _running = circles.IsRunning = true;
                btnRun.Content = "Cancel";
                tsReport.IsEnabled = sldrDays.IsEnabled = tbNugetPath.IsEnabled = !_running;
            });

            if (!string.IsNullOrEmpty(PackagePath))
                ScanEngine.Run(PackagePath, App.Profile!.Days, _reportMode, _cts.Token);
            else
                UpdateMessage($"{nameof(PackagePath)} cannot be empty!", MessageLevel.Error);

        }).GetAwaiter().OnCompleted(() =>
        {
            _running = circles.IsRunning = false;
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (_reportMode)
                    btnRun.Content = "Scan Packages";
                else
                    btnRun.Content = "Clean Packages";

                tsReport.IsEnabled = sldrDays.IsEnabled = tbNugetPath.IsEnabled = !_running;

                if (_cts.IsCancellationRequested)
                    UpdateMessage($"The process was canceled!", MessageLevel.Warning);
                else if (LogMessages.Count == 0)
                    UpdateMessage($"No matches discovered. Try adjusting the days slider and try again.", MessageLevel.Important);
            });
        });
        #endregion
    }

    void ButtonRunOnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => this.ProtectedCursor = null;

    void ButtonRunOnPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);


    void ReportOnSwitchToggled(object sender, RoutedEventArgs e)
    {
        var ts = sender as ToggleSwitch;
        if (ts != null && _loaded && ts.IsOn)
        {
            btnRun.Content = "Scan Packages";
            gbHeader.Text = "Scanning Options";
            _reportMode = true;
        }
        else if (ts != null && _loaded && !ts.IsOn)
        {
            btnRun.Content = "Clean Packages";
            gbHeader.Text = "Cleaning Options";
            _reportMode = false;
        }
    }

    void ReportOnChecked(object sender, RoutedEventArgs e)
    {
        if (_loaded && !_running)
        {
            btnRun.Content = "Scan Packages";
            gbHeader.Text = "Scanning Options";
            _reportMode = true;
        }
    }

    void ReportOnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_loaded && !_running)
        { 
            btnRun.Content = "Clean Packages";
            gbHeader.Text = "Cleaning Options";
            _reportMode = false;
        }
    }

    void ScanEngineOnScanError(Exception ex) => UpdateMessage($"{ex.Message}", MessageLevel.Warning);

    void ScanEngineOnScanComplete(long size)
    {
        App.Profile!.LastSize = size;
        App.Profile!.LastCount = LogMessages.Count;

        if (_reportMode)
            UpdateMessage($"Reclaimed size if deleted: {size.HumanReadableSize()}", MessageLevel.Important);
        else
            UpdateMessage($"Total bytes reclaimed: {size.HumanReadableSize()}", MessageLevel.Important);
    }

    void ScanEngineOnTargetAdded(TargetItem ti) => AddLogItem(ti);

    #endregion

}
