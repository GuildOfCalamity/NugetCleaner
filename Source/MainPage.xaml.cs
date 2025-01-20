using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    static Brush _lvl1 = new SolidColorBrush(Colors.Gray);
    static Brush _lvl2 = new SolidColorBrush(Colors.DodgerBlue);
    static Brush _lvl3 = new SolidColorBrush(Colors.Yellow);
    static Brush _lvl4 = new SolidColorBrush(Colors.Orange);
    static Brush _lvl5 = new SolidColorBrush(Colors.Red);
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
                case MessageLevel.Important:
                    {
                        tbMessages.Foreground = _lvl3;
                        infoBar.IsOpen = true;
                        infoBar.Message = msg;
                        break;
                    }
                case MessageLevel.Warning: tbMessages.Foreground = _lvl4; break;
                case MessageLevel.Error:
                    {
                        tbMessages.Foreground = _lvl5;
                        infoBar.IsOpen = true;
                        infoBar.Message = msg;
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
        string text = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        if (string.IsNullOrEmpty(text))
        {
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
        _loaded = true;
        PackagePath = tbNugetPath.Text = GetGlobalPackagesFolder();
    }

    void SliderDaysChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loaded)
        {
            App.Profile.Days = (int)e.NewValue;
            UpdateMessage($"Days changed to {App.Profile.Days}");
        }
    }

    void RunButtonOnClick(object sender, RoutedEventArgs e)
    {
        #region [check current state]
        if (!_running)
        {
            _cts = new CancellationTokenSource();
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
            });

            if (!string.IsNullOrEmpty(PackagePath))
                ScanEngine.Run(PackagePath, App.Profile.Days, _reportMode, _cts.Token);
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

                if (LogMessages.Count == 0)
                    UpdateMessage($"No matches discovered. Try adjusting the days slider and try again.", MessageLevel.Important);
            });
        });
        #endregion
    }

    void ButtonRunOnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => this.ProtectedCursor = null;

    void ButtonRunOnPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

    void ReportOnChecked(object sender, RoutedEventArgs e)
    {
        if (_loaded && !_running)
        {
            btnRun.Content = "Scan Packages";
            _reportMode = true;
        }
    }

    void ReportOnUnchecked(object sender, RoutedEventArgs e)
    {
        if (_loaded && !_running)
        { 
            btnRun.Content = "Clean Packages";
            _reportMode = false;
        }
    }

    void ScanEngineOnScanError(Exception ex) => UpdateMessage($"{ex.Message}", MessageLevel.Warning);

    void ScanEngineOnScanComplete(string msg) => UpdateMessage(msg, MessageLevel.Important);

    void ScanEngineOnTargetAdded(TargetItem ti) => AddLogItem(ti);

    #endregion
}
