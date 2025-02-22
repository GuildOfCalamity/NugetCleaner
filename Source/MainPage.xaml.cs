using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

using NugetCleaner.Support;

namespace NugetCleaner;

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
    static Compositor? _compositor;
    static float ctrlOffsetX = 0; // Store the grid's initial offset for later animation.
    static CancellationTokenSource _cts = new();
    public string? PackagePath { get; set; }
    public ObservableCollection<TargetItem> LogMessages { get; set; } = new();
    #endregion

    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += MainPageOnLoaded;
        btnRun.Loaded += ButtonRunOnLoaded;
        btnRun.PointerEntered += ButtonRunOnPointerEntered;
        btnRun.PointerExited += ButtonRunOnPointerExited;
        ScanEngine.OnTargetAdded += ScanEngineOnTargetAdded;
        ScanEngine.OnScanComplete += ScanEngineOnScanComplete;
        ScanEngine.OnScanError += ScanEngineOnScanError;

        #region [MessageLevel Brushes]
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

        //DispatcherQueue.InvokeOnUI(() => { tbMessages.Text = msg; });

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

    async void MainPageOnLoaded(object sender, RoutedEventArgs e)
    {
        sldrDays.Value = App.Profile!.Days;
        _loaded = true;
        PackagePath = tbNugetPath.Text = GetGlobalPackagesFolder();

        #region [User Messages]
        if (App.Profile.LastSize > 0)
            UpdateMessage($"Last calculated size was {App.Profile.LastSize.HumanReadableSize()} ({App.Profile.LastCount} items)", MessageLevel.Information);

        if (App.Profile.FirstRun && this.Content is not null)
        {
            App.Profile.FirstRun = false;
            ContentDialogResult result = await DialogHelper.ShowAsync(new Dialogs.AboutDialog(), this.Content as FrameworkElement);
            if (result is ContentDialogResult.Primary) { Debug.WriteLine("[INFO] User clicked 'OK'."); }
            else if (result is ContentDialogResult.None) { Debug.WriteLine("[INFO] User clicked 'Cancel'."); }
            #region [previous technique]
            //await App.ShowContentDialog(
            //    $"About {App.GetCurrentNamespace()?.SeparateCamelCase()}", 
            //    $"A cleaner utility for outdated NuGet packages, which can consume a large amount of space on your local storage.{Environment.NewLine}{Environment.NewLine}The \"Report Only\" mode will scan for and display package total sizes based on the stale amount, in days.{Environment.NewLine}{Environment.NewLine}Application version {App.GetCurrentAssemblyVersion()}",
            //    "OK",
            //    "",
            //    400,
            //    null,
            //    null,
            //    new Uri($"ms-appx:///Assets/AppIcon.png"));
            #endregion
        }
        #endregion
    }

    async void RunButtonOnClick(object sender, RoutedEventArgs e)
    {
        #region [Check current state]
        if (!_running)
        {
            _cts = new CancellationTokenSource();

            if (!_reportMode)
            {
                if (!App.IsClosing)
                {
                    ContentDialogResult result = await DialogHelper.ShowAsync(new Dialogs.CleanupDialog(), this.Content as FrameworkElement);
                    if (result is ContentDialogResult.Primary)
                    {
                        Debug.WriteLine("[INFO] User agrees to proceed.");
                        LogMessages.Clear();
                        UpdateMessage("Cleaning…");
                    }
                    else if (result is ContentDialogResult.None)
                    {
                        Debug.WriteLine("[INFO] User canceled the process.");
                        _cts.Cancel();
                    }
                    #region [previous technique]
                    //await App.ShowContentDialog(
                    //    $"Confirmation",
                    //    $"This could result is deleted files, they will not be moved to the recycling bin.{Environment.NewLine}{Environment.NewLine}Are you sure?",
                    //    "Yes",
                    //    "No",
                    //    300,
                    //    delegate { Debug.WriteLine("[INFO] User agrees to proceed."); LogMessages.Clear(); },
                    //    delegate { Debug.WriteLine("[INFO] User canceled the process."); _cts.Cancel(); },
                    //    new Uri($"ms-appx:///Assets/AlertIcon.png"));
                    #endregion
                }
            }
            else
            {
                LogMessages.Clear();
                UpdateMessage("Scanning…");
            }
            btnRun.Content = "Cancel";
        }
        else
        {
            UpdateMessage("Canceling…");
            _cts?.Cancel();
            btnRun.IsEnabled = _running = false;
            DispatcherQueue?.EnqueueAsync(async () =>
            {
                await Task.Delay(1500);
                btnRun.IsEnabled = true;
                if (_reportMode)
                    btnRun.Content = "Scan Packages";
                else
                    btnRun.Content = "Clean Packages";
            });
            return;
        }
        #endregion

        #region [Offload scan in new thread]
        Task.Run(delegate()
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                TaskbarProgress.SetState(App.WindowHandle, TaskbarProgress.TaskbarStates.Indeterminate);
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
            _running = false;
            DispatcherQueue?.TryEnqueue(() =>
            {
                TaskbarProgress.SetState(App.WindowHandle, TaskbarProgress.TaskbarStates.NoProgress);
                circles.IsRunning = _running;
                tsReport.IsEnabled = sldrDays.IsEnabled = tbNugetPath.IsEnabled = !_running;
                
                if (_reportMode)
                    btnRun.Content = "Scan Packages";
                else
                    btnRun.Content = "Clean Packages";

                if (_cts.IsCancellationRequested)
                    UpdateMessage($"The process was canceled!", MessageLevel.Warning);
                else if (LogMessages.Count == 0)
                    UpdateMessage($"No matches discovered. Try adjusting the days slider and try again.", MessageLevel.Important);
                else if (LogMessages.Count > 1)
                    ToastHelper.ShowStandardToast("Results", $"There are {LogMessages.Count.ToString("#,###,##0")} stale NuGet packages {(_reportMode ? "than can be" : "that were")} removed.");
            });
        });
        #endregion
    }

    /// <summary>
    /// I'm leaving this in as a template in the event you add additional control events for the user.
    /// </summary>
    void SampleClickEventLogic()
    {
        bool AppWideBusyFlag = false;
        _cts = new CancellationTokenSource();


        #region [*************** Technique #1 ***************]

        // Capture the UI context before forking.
        var syncContext = System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext();

        var scanTask = Task.Run(async () =>
        {
            AppWideBusyFlag = true;

            await Task.Delay(5000);

        }, _cts.Token);

        /** 
         **   You wouldn't use both of these ContinueWith examples below, just select the one you're comfortable with.
         **   One demonstrates with synchronization context, and one demonstrates without synchronization context.
         **/
        #region [With Synchronization Context]
        // We're guaranteed the UI context when we come back, so any
        // FrameworkElement/UIElement/DependencyObject update can
        // be done directly via the control's properties.
        scanTask.ContinueWith(tsk =>
        {
            AppWideBusyFlag = false;

            if (tsk.IsCanceled)
                tbMessages.Text = "Scan canceled.";
            else if (tsk.IsFaulted)
                tbMessages.Text = $"Error: {tsk.Exception?.GetBaseException().Message}";
            else
                tbMessages.Text = "Scan complete!";
        }, syncContext);
        #endregion

        #region [Without Synchronization Context]
        // We're not guaranteed the UI context when we come back, so any
        // FrameworkElement/UIElement/DependencyObject update should be
        // done via the main DispatcherQueue or the control's Dispatcher.
        scanTask.ContinueWith(tsk =>
        {
            AppWideBusyFlag = false;

            if (tsk.IsCanceled)
                DispatcherQueue.InvokeOnUI(() => tbMessages.Text = "Scan canceled.");
            else if (tsk.IsFaulted)
                DispatcherQueue.InvokeOnUI(() => tbMessages.Text = $"Error: {tsk.Exception?.GetBaseException().Message}");
            else
                DispatcherQueue.InvokeOnUI(() => tbMessages.Text = "Scan complete!");
        }, syncContext);
        #endregion

        #endregion [*************** Technique #1 ***************]


        #region [*************** Technique #2 ***************]

        var dummyTask = SampleAsyncMethod(_cts.Token);

        /** Happens when successful **/
        dummyTask.ContinueWith(task =>
        {
            var list = task.Result; // Never access Task.Result unless the Task was successful.
            foreach (var thing in list) { LogMessages.Add(thing); }
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

        /** Happens when faulted **/
        dummyTask.ContinueWith(task =>
        {
            foreach (var ex in task.Exception!.Flatten().InnerExceptions) { tbMessages.Text = ex.Message; }
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());

        /** Happens when canceled **/
        dummyTask.ContinueWith(task =>
        {
            tbMessages.Text = "Dummy Task Canceled!";
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnCanceled, TaskScheduler.FromCurrentSynchronizationContext());

        /** Always happens **/
        dummyTask.ContinueWith(task =>
        {
            AppWideBusyFlag = false;
        }, TaskScheduler.FromCurrentSynchronizationContext());

        // Just a place-holder.
        async Task<List<TargetItem>> SampleAsyncMethod(CancellationToken cancelToken = new CancellationToken())
        {
            await Task.Delay(3000, cancelToken);
            cancelToken.ThrowIfCancellationRequested();
            return new List<TargetItem>();
        }
        #endregion [*************** Technique #2 ***************]

    }

    void SliderDaysChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loaded)
        {
            App.Profile!.Days = (int)e.NewValue;
            UpdateMessage($"Days changed to {App.Profile.Days}");
        }
    }

    void ButtonRunOnLoaded(object sender, RoutedEventArgs e)
    {
        // It seems when the button's offset is modified from Grid/Stack centering,
        // we must force an animation to run to setup the initial starting conditions.
        // If you skip this step then you'll have to mouse-over the grid twice to
        // see the intended animation (for first run only).
        ctrlOffsetX = ((Button)sender).ActualOffset.X;
        AnimateButtonX((Button)sender, ctrlOffsetX);
    }

    void ButtonRunOnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        this.ProtectedCursor = null;
        AnimateButtonX((Button)sender, ctrlOffsetX + 4f);
    }

    void ButtonRunOnPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        AnimateButtonX((Button)sender, ctrlOffsetX);
    }

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

    void ScanEngineOnScanError(Exception ex) => UpdateMessage($"{ex.Message}", MessageLevel.Warning);

    void ScanEngineOnScanComplete(long size)
    {
        if (size != 0) { App.Profile!.LastSize = size; }
        if (LogMessages.Count != 0) { App.Profile!.LastCount = LogMessages.Count; }

        if (_reportMode)
            UpdateMessage($"Reclaimed size if deleted: {size.HumanReadableSize()}", MessageLevel.Important);
        else
            UpdateMessage($"Total bytes reclaimed: {size.HumanReadableSize()}", MessageLevel.Important);
    }

    void ScanEngineOnTargetAdded(TargetItem ti) => AddLogItem(ti);

    #endregion

    /// <summary>
    /// Ensures the button starts with Offset.X = 0
    /// </summary>
    public void InitializeButtonOffsetX(Button button)
    {
        if (button is null) { return; }
        Visual buttonVisual = ElementCompositionPreview.GetElementVisual(button);
        buttonVisual.Offset = new System.Numerics.Vector3(0, buttonVisual.Offset.Y, buttonVisual.Offset.Z);
    }

    /// <summary>
    /// Applies a <see cref="SpringScalarNaturalMotionAnimation"/> to <paramref name="button"/> based on the <paramref name="offset"/>.
    /// </summary>
    public void AnimateButtonX(Button button, float offset)
    {
        if (button is null)
            return;

        if (_compositor is null)
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;

        // Create spring animation
        SpringScalarNaturalMotionAnimation _springAnimation = _compositor.CreateSpringScalarAnimation();
        _springAnimation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        _springAnimation.Target = "Offset.X";   // Move horizontally
        _springAnimation.InitialVelocity = 50f; // Movement speed
        _springAnimation.FinalValue = offset;   // Set the final target X position
        _springAnimation.DampingRatio = 0.3f;   // Lower values are more "springy"
        _springAnimation.Period = TimeSpan.FromMilliseconds(50);

        // Get the button's visual and apply the animation
        Visual buttonVisual = ElementCompositionPreview.GetElementVisual(button);
        buttonVisual.StartAnimation("Offset.X", _springAnimation);
    }
}
