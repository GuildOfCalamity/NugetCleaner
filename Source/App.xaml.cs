using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NugetCleaner.Dialogs;
using NugetCleaner.Support;
using Windows.ApplicationModel.Activation;
using Windows.Management.Deployment;
using Windows.Storage;

namespace NugetCleaner;

public partial class App : Application
{
    #region [Properties]
    public static Window? m_window;
    public static int m_width { get; set; } = 800;
    public static int m_height { get; set; } = 550;
    public static bool IsClosing { get; set; } = false;
    public static FrameworkElement? MainRoot { get; set; }
    public static IntPtr WindowHandle { get; set; }
    public static AppSettings? Profile { get; set; }
    public static AppWindow? AppWin { get; set; }
    public static Version WindowsVersion => Extensions.GetWindowsVersionUsingAnalyticsInfo();
    public static bool IsWindowMaximized { get; set; }
    public static FontFamily? EmbeddedFont { get; set; }

    private NotificationManager? notificationManager;
    // NOTE: If you would like to deploy this app as "Packaged", then open the csproj and change
    //  <WindowsPackageType>None</WindowsPackageType> to <WindowsPackageType>MSIX</WindowsPackageType>
    // https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/#advantages-and-disadvantages-of-packaging-your-app
#if IS_UNPACKAGED // We're using a custom PropertyGroup Condition we defined in the csproj to help us with the decision.
    public static bool IsPackaged { get => false; }
#else
    public static bool IsPackaged { get => true; }
#endif
    #endregion

    public App()
    {
        Debug.WriteLine($"[INFO] {System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}__{System.Reflection.MethodBase.GetCurrentMethod()?.Name} [{DateTime.Now.ToString("hh:mm:ss.fff tt")}]");
        App.Current.DebugSettings.FailFastOnErrors = false;
        #region [Exception handlers]
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
        AppDomain.CurrentDomain.FirstChanceException += CurrentDomainFirstChanceException;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        UnhandledException += ApplicationUnhandledException;
        #endregion
        this.InitializeComponent();
        
        // For packaged app only.
        if (App.IsPackaged)
        {
            ApplicationData.Current.DataChanged += OnAppDataChanged;
            notificationManager = new NotificationManager();
        }
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        m_window = new MainWindow();

        #region [Determining AppInstance activation kind]
        bool isRedirect = false;
        // Find out what kind of activation this is.
        var eventArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
        if (eventArgs.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.File)
        {
            var fileActivationArguments = eventArgs.Data as Windows.ApplicationModel.Activation.FileActivatedEventArgs;
            Debug.WriteLine($"[INFO] {fileActivationArguments?.Files[0].Path}");
            // This is a file activation: here we'll get the file information,
            // and register the file name as our instance key.
            if (eventArgs.Data is IFileActivatedEventArgs fileArgs)
            {
                IStorageItem file = fileArgs.Files[0];
                var keyInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey(file.Name);
                // If we successfully registered the file name, we must be the
                // only instance running that was activated for this file.
                if (keyInstance != null && !keyInstance.IsCurrent)
                {
                    isRedirect = true;
                    keyInstance.RedirectActivationToAsync(eventArgs).GetAwaiter().GetResult();
                }
            }
        }
        else
        {
            Debug.WriteLine($"[INFO] ActivationKind => {eventArgs.Kind}");
        }
        #endregion

        AppWin = GetAppWindow(m_window);
        if (AppWin != null)
        {
            // Gets or sets a value that indicates whether this window will appear in various system representations, such as ALT+TAB and taskbar.
            AppWin.IsShownInSwitchers = true;

            // We don't have the Closing event exposed by default, so we'll use the AppWindow to compensate.
            AppWin.Closing += (s, e) =>
            {
                App.IsClosing = true;
                Debug.WriteLine($"[INFO] Application closing detected at {DateTime.Now.ToString("hh:mm:ss.fff tt")}");
                if (Profile is not null)
                {
                    Process proc = Process.GetCurrentProcess();
                    Profile!.Metrics = $"Process used {proc.PrivateMemorySize64 / 1024 / 1024}MB of memory and {proc.TotalProcessorTime.ToReadableString()} TotalProcessorTime on {Environment.ProcessorCount} possible cores.";
                    Profile!.LastUse = DateTime.Now;
                    Profile!.Version = GetCurrentAssemblyVersion();
                    Profile?.Save();
                }
            };

            // Destroying is always called, but Closing is only called when the application is shutdown normally.
            AppWin.Destroying += (s, e) =>
            {
                Debug.WriteLine($"[INFO] Application destroying detected at {DateTime.Now.ToString("hh:mm:ss.fff tt")}");
            };

            // The changed event contains the valuables, such as: position, size, visibility, z-order and presenter.
            AppWin.Changed += (s, args) =>
            {
                if (args.DidSizeChange)
                {
                    Debug.WriteLine($"[INFO] Window size changed to {s.Size.Width},{s.Size.Height}");
                    if (s.Presenter is not null && s.Presenter is OverlappedPresenter op)
                        IsWindowMaximized = op.State is OverlappedPresenterState.Maximized;

                    if (!IsWindowMaximized && Profile is not null)
                    {
                        // Update width and height for profile settings.
                        Profile!.WindowHeight = s.Size.Height;
                        Profile!.WindowWidth = s.Size.Width;
                    }
                }

                if (args.DidPositionChange)
                {
                    if (s.Position.X > 0 && s.Position.Y > 0)
                    {
                        // This property is initially null. Once a window has been shown it always has a
                        // presenter applied, either one applied by the platform or applied by the app itself.
                        if (s.Presenter is not null && s.Presenter is OverlappedPresenter op)
                        {
                            if (op.State == OverlappedPresenterState.Minimized)
                            {
                                Debug.WriteLine($"[INFO] Window minimized");
                            }
                            else if (op.State != OverlappedPresenterState.Maximized && Profile is not null)
                            {
                                Debug.WriteLine($"[INFO] Updating window position to {s.Position.X},{s.Position.Y} and size to {s.Size.Width},{s.Size.Height}");
                                // Update X and Y for profile settings.
                                Profile!.WindowLeft = s.Position.X;
                                Profile!.WindowTop = s.Position.Y;
                            }
                            else
                            {
                                Debug.WriteLine($"[INFO] Ignoring position saving (window maximized or restored)");
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[INFO] Ignoring zero/negative positional values");
                    }
                }
            };

            // Set the application icon.
            if (IsPackaged)
            {
                AppWin.SetIcon(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, $"Assets/AppIcon.ico"));
                GetMsixPackageInfo(Windows.ApplicationModel.Package.Current.Id.FullName);
            }
            else
                AppWin.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, $"Assets/AppIcon.ico"));

            AppWin.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
        }

        // For AppNotificationManager
        if (App.IsPackaged)
        {
            notificationManager?.Init();
            Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Register(Assembly.GetExecutingAssembly().GetName().Name, new Uri("ms-appx:///Assets/NoticeIcon.png"));
        }

        

        m_window.Activate();

        // Save the FrameworkElement for any future content dialogs.
        MainRoot = m_window.Content as FrameworkElement;

        Profile = AppSettings.Load(true);
        // Update settings with examples if it's new.
        if (string.IsNullOrEmpty(Profile.Username))
        {
            Profile.Username = "SomeUser";
            Profile.Password = "SomePassword";     // This will be encrypted during save
            Profile.ApiKey = "ABC-12345-API-67890";
            Profile.ApiSecret = "secretApiKey123"; // This will also be encrypted
            Profile.Save();

            AppWin?.Resize(new Windows.Graphics.SizeInt32(m_width, m_height));
            CenterWindow(m_window);
        }
        else
        {
            // User's monitor setup could change on next run so verify that we're
            // not trying to place the window on a monitor that no longer exists.
            // This can happen frequently with laptops that are docked/undocked.
            var displayArea = GetDisplayArea(m_window);
            if (displayArea != null)
            {
                var monitorCount = GetMonitorCount();
                if (Profile.WindowLeft >= (displayArea.OuterBounds.Width * monitorCount))
                {
                    Profile.WindowLeft = 100;
                    DebugLog($"Current setting would cause window to appear outside display bounds, resetting to {Profile.WindowLeft}.");
                }
                else
                {
                    DebugLog($"Display area bounds: {displayArea.OuterBounds.Width * monitorCount},{displayArea.OuterBounds.Height}");
                }
            }
            AppWin?.MoveAndResize(new Windows.Graphics.RectInt32(Profile.WindowLeft, Profile.WindowTop, Profile.WindowWidth, Profile.WindowHeight), Microsoft.UI.Windowing.DisplayArea.Primary);
        }

        //ListInstalledMsixPackages();
        //EmbeddedFont = EmbeddedHelper.LoadEmbeddedFontFamily("Hack.ttf", "Hack");
    }

    public static void OnNotificationInvoked(Microsoft.Windows.AppNotifications.AppNotificationManager sender, Microsoft.Windows.AppNotifications.AppNotificationActivatedEventArgs args)
    {
        Debug.WriteLine($"[INFO] OnNotificationInvoked: {args.Argument}");
        foreach (var a in args.Arguments)
        {
            Debug.WriteLine($" - {a.Key}:{a.Value}");
        }
    }

    #region [Window Helpers]
    /// <summary>
    /// This code example demonstrates how to retrieve an AppWindow from a WinUI3 window.
    /// The AppWindow class is available for any top-level HWND in your app.
    /// AppWindow is available only to desktop apps (both packaged and unpackaged), it's not available to UWP apps.
    /// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/windowing/windowing-overview
    /// https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.create?view=windows-app-sdk-1.3
    /// </summary>
    public Microsoft.UI.Windowing.AppWindow? GetAppWindow(object window)
    {
        // Retrieve the window handle (HWND) of the current (XAML) WinUI3 window.
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        // For other classes to use (mostly P/Invoke).
        App.WindowHandle = hWnd;

        // Retrieve the WindowId that corresponds to hWnd.
        Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);

        // Lastly, retrieve the AppWindow for the current (XAML) WinUI3 window.
        Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

        return appWindow;
    }

    /// <summary>
    /// If <see cref="App.WindowHandle"/> is set then a call to User32 <see cref="SetForegroundWindow(nint)"/> 
    /// will be invoked. I tried using the native OverlappedPresenter.Restore(true), but that does not work.
    /// </summary>
    public static void ActivateMainWindow()
    {
        if (App.WindowHandle != IntPtr.Zero)
        {
            //if (!IsWindowVisible(App.WindowHandle))
            _ = SetForegroundWindow(App.WindowHandle);
        }
        
        if (AppWin is not null && AppWin.Presenter is not null && AppWin.Presenter is OverlappedPresenter op)
        {
            op.Restore(true);
        }
    }

    /// <summary>
    /// Centers a <see cref="Microsoft.UI.Xaml.Window"/> based on the <see cref="Microsoft.UI.Windowing.DisplayArea"/>.
    /// </summary>
    /// <remarks>This must be run on the UI thread.</remarks>
    public static void CenterWindow(Window window)
    {
        if (window == null) { return; }

        try
        {
            System.IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            if (Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId) is Microsoft.UI.Windowing.AppWindow appWindow &&
                Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest) is Microsoft.UI.Windowing.DisplayArea displayArea)
            {
                Windows.Graphics.PointInt32 CenteredPosition = appWindow.Position;
                CenteredPosition.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                CenteredPosition.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
                appWindow.Move(CenteredPosition);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] {MethodBase.GetCurrentMethod()?.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// The <see cref="Microsoft.UI.Windowing.DisplayArea"/> exposes properties such as:
    /// OuterBounds     (Rect32)
    /// WorkArea.Width  (int)
    /// WorkArea.Height (int)
    /// IsPrimary       (bool)
    /// DisplayId.Value (ulong)
    /// </summary>
    /// <param name="window"></param>
    /// <returns><see cref="DisplayArea"/></returns>
    public Microsoft.UI.Windowing.DisplayArea? GetDisplayArea(Window window)
    {
        try
        {
            System.IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var da = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            return da;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] {MethodBase.GetCurrentMethod()?.Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the current state of the given window.
    /// </summary>
    /// <param name="window">The <see cref="Microsoft.UI.Xaml.Window"/> to check.</param>
    /// <returns>"Maximized","Minimized","Restored","FullScreen","Unknown"</returns>
    /// <remarks>The "Restored" state is equivalent to "Normal" in a WinForm app.</remarks>
    public string GetWindowState(Window window)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        var appWindow = GetAppWindow(window);

        if (appWindow is null)
            return "Unknown";

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            return presenter.State switch
            {
                OverlappedPresenterState.Maximized => "Maximized",
                OverlappedPresenterState.Minimized => "Minimized",
                OverlappedPresenterState.Restored => "Restored",
                _ => "Unknown"
            };
        }

        // If it's not an OverlappedPresenter, check for FullScreen mode.
        if (appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
            return "FullScreen";

        return "Unknown";
    }

    /// <summary>
    /// Checks if the given window is maximized on its current monitor.
    /// </summary>
    /// <param name="window">The WinUI 3 window to check.</param>
    /// <returns>true if the window is maximized on its current monitor, false otherwise</returns>
    public bool IsWindowMaximizedOnCurrentMonitor(Window window)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        // Get the current window bounds
        var windowBounds = window.Bounds;

        // Get the monitor that the window is currently on
        var displayArea = GetDisplayArea(window);
        if (displayArea == null)
        {
            throw new InvalidOperationException("Could not determine the monitor where the window is located.");
        }

        // Get the work area (visible bounds) of the monitor
        var workArea = displayArea.WorkArea;

        // Allow for minor differences due to DPI scaling
        const double tolerance = 2.0;

        return Math.Abs(windowBounds.Width - workArea.Width) < tolerance &&
               Math.Abs(windowBounds.Height - workArea.Height) < tolerance;
    }

    /// <summary>
    /// To my knowledge there is no way to get this natively via the WinUI3 SDK, so I'm adding a P/Invoke.
    /// </summary>
    /// <returns>the amount of displays the system recognizes</returns>
    public static int GetMonitorCount()
    {
        int count = 0;

        MonitorEnumProc callback = (IntPtr hDesktop, IntPtr hdc, ref ScreenRect prect, int d) => ++count > 0;

        if (EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, 0))
        {
            Debug.WriteLine($"[INFO] You have {count} {(count > 1 ? "monitors" : "monitor")}.");
            return count;
        }
        else
        {
            Debug.WriteLine("[WARNING] An error occurred while enumerating monitors.");
            return 1;
        }
    }
    #endregion

    #region [Domain Events]
    void ApplicationUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // https://docs.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception.
        Exception? ex = e.Exception;
        Debug.WriteLine($"[UnhandledException]: {ex?.Message}");
        Debug.WriteLine($"Unhandled exception of type {ex?.GetType()}: {ex}");
        DebugLog($"Unhandled Exception StackTrace: {Environment.StackTrace}");
        e.Handled = true;
    }

    void CurrentDomainOnProcessExit(object? sender, EventArgs e)
    {
        if (!IsClosing)
            IsClosing = true;

        if (sender is null)
            return;

        if (App.IsPackaged)
            notificationManager?.Unregister();

        if (sender is AppDomain ad)
        {
            Debug.WriteLine($"[OnProcessExit]", $"{nameof(App)}");
            Debug.WriteLine($"DomainID: {ad.Id}", $"{nameof(App)}");
            Debug.WriteLine($"FriendlyName: {ad.FriendlyName}", $"{nameof(App)}");
            Debug.WriteLine($"BaseDirectory: {ad.BaseDirectory}", $"{nameof(App)}");
        }
    }

    void CurrentDomainFirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        Debug.WriteLine($"[ERROR] First chance exception from {sender?.GetType()}: {e.Exception.Message}");
        // Ignore profile encryption property tests.
        if (!string.IsNullOrEmpty(e.Exception.Message) && !e.Exception.Message.Contains("The input is not a valid Base-64 string"))
        {
            DebugLog($"First chance exception from {sender?.GetType()}: {e.Exception.Message}");
            if (e.Exception.InnerException != null)
                DebugLog($"  ⇨ InnerException: {e.Exception.InnerException.Message}");
            DebugLog($"First chance exception StackTrace: {Environment.StackTrace}");
        }
    }

    void CurrentDomainUnhandledException(object? sender, System.UnhandledExceptionEventArgs e)
    {
        Exception? ex = e.ExceptionObject as Exception;
        Debug.WriteLine($"[ERROR] Thread exception of type {ex?.GetType()}: {ex}");
        DebugLog($"Thread exception of type {ex?.GetType()}: {ex}");
    }

    void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (e.Exception is AggregateException aex)
        {
            aex?.Flatten().Handle(ex =>
            {
                Debug.WriteLine($"[ERROR] Unobserved task exception: {ex?.Message}");
                DebugLog($"Unobserved task exception: {ex?.Message}");
                return true;
            });
        }
        e.SetObserved(); // suppress and handle manually
    }

    void OnAppDataChanged(Windows.Storage.ApplicationData sender, object args)
    {
        Debug.WriteLine($"AppVersion {sender.Version} change event. AppDataArgs: {args}");
    }

    /// <summary>
    /// Simplified debug logger for app-wide use.
    /// </summary>
    /// <param name="message">the text to append to the file</param>
    public static void DebugLog(string message)
    {
        try
        {
            if (App.IsPackaged)
                System.IO.File.AppendAllText(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Debug.log"), $"[{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff tt")}] {message}{Environment.NewLine}");
            else
                System.IO.File.AppendAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Debug.log"), $"[{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff tt")}] {message}{Environment.NewLine}");
        }
        catch (Exception)
        {
            Debug.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff tt")}] {message}");
        }
    }
    #endregion

    #region [Reflection Helpers]
    /// <summary>
    /// Returns the declaring type's namespace.
    /// </summary>
    public static string? GetCurrentNamespace() => System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace;

    /// <summary>
    /// Returns the declaring type's namespace.
    /// </summary>
    public static string? GetFormattedNamespace() => System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Namespace?.SeparateCamelCase();

    /// <summary>
    /// Returns the declaring type's full name.
    /// </summary>
    public static string? GetCurrentFullName() => System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Assembly.FullName;

    /// <summary>
    /// Returns the declaring type's assembly name.
    /// </summary>
    public static string? GetCurrentAssemblyName() => System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;

    /// <summary>
    /// Returns the AssemblyVersion, not the FileVersion.
    /// </summary>
    public static string? GetCurrentAssemblyVersion() => $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";
    #endregion

    #region [Dialog Helper]
    static SemaphoreSlim semaSlim = new SemaphoreSlim(1, 1);
    /// <summary>
    /// The <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/> looks much better than the
    /// <see cref="Windows.UI.Popups.MessageDialog"/> and is part of the native Microsoft.UI.Xaml.Controls.
    /// The <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/> does not offer a <see cref="Windows.UI.Popups.UICommandInvokedHandler"/>
    /// callback, but in this example I've replaced that functionality with actions. Both can be shown asynchronously.
    /// </summary>
    /// <remarks>
    /// There is no need to call <see cref="WinRT.Interop.InitializeWithWindow.Initialize"/> when using the <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/>,
    /// but a <see cref="Microsoft.UI.Xaml.XamlRoot"/> must be defined since it inherits from <see cref="Microsoft.UI.Xaml.Controls.Control"/>.
    /// The <see cref="SemaphoreSlim"/> was added to prevent "COMException: Only one ContentDialog can be opened at a time."
    /// </remarks>
    public static async Task ShowContentDialog(string title, string message, string primaryText, string cancelText, double minWidth, Action? onPrimary, Action? onCancel, Uri? imageUri)
    {
        if (App.MainRoot?.XamlRoot == null) { return; }

        await semaSlim.WaitAsync();

        #region [Initialize Assets]
        double fontSize = 14;
        double brdrThickness = 4;
        if (minWidth <= 0)
            minWidth = 400;
        Microsoft.UI.Xaml.Media.FontFamily fontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe");
        Microsoft.UI.Xaml.Media.Brush brdrBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255,20,20,20));

        if (App.Current.Resources.TryGetValue("FontSizeMedium", out object _))
            fontSize = (double)App.Current.Resources["FontSizeMedium"];

        //if (App.Current.Resources.TryGetValue("PrimaryFont", out object _))
        //    fontFamily = (Microsoft.UI.Xaml.Media.FontFamily)App.Current.Resources["PrimaryFont"];

        if (EmbeddedFont is not null)
            fontFamily = EmbeddedFont;

        if (App.Current.Resources.TryGetValue("GradientBarBrush", out object _))
            brdrBrush = (Microsoft.UI.Xaml.Media.Brush)App.Current.Resources["GradientBarBrush"];

        StackPanel panel = new StackPanel()
        {
            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical,
            Spacing = 10d
        };

        if (imageUri is not null)
        {
            panel.Children.Add(new Image
            {
                Margin = new Thickness(1, -50, 1, 1), // Move the image into the title area.
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
                Width = 40,
                Height = 40,
                Source = new BitmapImage(imageUri)
            });
        }

        panel.Children.Add(new TextBlock()
        {
            Text = message,
            FontSize = fontSize,
            FontFamily = fontFamily,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
        });

        var tb = new TextBox()
        {
            Text = message,
            MinWidth = minWidth,
            FontSize = fontSize,
            FontFamily = fontFamily,
            TextWrapping = TextWrapping.Wrap
        };
        tb.Loaded += (s, e) => { tb.SelectAll(); };
        #endregion

        // NOTE: Content dialogs will automatically darken the background.
        ContentDialog contentDialog = new ContentDialog()
        {
            Title = title,
            MinWidth = minWidth + brdrThickness,
            BorderBrush = brdrBrush,
            BorderThickness = new Thickness(brdrThickness),
            PrimaryButtonText = primaryText,
            CloseButtonText = cancelText,
            Content = panel,
            XamlRoot = App.MainRoot?.XamlRoot,
            RequestedTheme = App.MainRoot?.ActualTheme ?? ElementTheme.Default
        };

        try
        {
            ContentDialogResult result = await contentDialog.ShowAsync();

            switch (result)
            {
                case ContentDialogResult.Primary:
                    onPrimary?.Invoke();
                    break;
                //case ContentDialogResult.Secondary:
                //    onSecondary?.Invoke();
                //    break;
                case ContentDialogResult.None: // Cancel
                    onCancel?.Invoke();
                    break;
                default:
                    Debug.WriteLine($"Dialog result not defined.");
                    break;
            }
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Debug.WriteLine($"[ERROR] ShowDialogBox(HRESULT={ex.ErrorCode}): {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] ShowDialogBox: {ex.Message}");
        }
        finally
        {
            semaSlim.Release();
        }
    }
    #endregion

    #region [Package Helpers]
    public static void ListInstalledMsixPackages()
    {
        try
        {
            var packageManager = new PackageManager();
            var packages = packageManager.FindPackages();

            Debug.WriteLine("[INFO] Installed Packages:");
            foreach (var package in packages)
            {
                Debug.WriteLine($" - {package.Id.FullName}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to list packages: {ex.Message}");
        }
    }

    public static void GetMsixPackageInfo(string packageFullName)
    {
        try
        {
            var packageManager = new PackageManager();
            var package = packageManager.FindPackage(packageFullName);

            if (package != null)
            {
                Debug.WriteLine($"[INFO] Package Name......: {package.Id.Name}");
                Debug.WriteLine($"[INFO] Package FullName..: {package.Id.FullName}");
                Debug.WriteLine($"[INFO] Version...........: {package.Id.Version.Major}.{package.Id.Version.Minor}.{package.Id.Version.Build}.{package.Id.Version.Revision}");
                Debug.WriteLine($"[INFO] Publisher.........: {package.Id.Publisher}");
                Debug.WriteLine($"[INFO] Installed Location: {package.InstalledLocation.Path}");
                Debug.WriteLine($"[INFO] DateCreated.......: {package.InstalledLocation.DateCreated}");
                //Debug.WriteLine($"[INFO] EffectivePath.....: {package.EffectivePath}");
                //Debug.WriteLine($"[INFO] InstalledPath.....: {package.InstalledPath}");
            }
            else
            {
                Debug.WriteLine($"[WARNING] Package '{packageFullName}' not found.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to retrieve package '{packageFullName}' info: {ex.Message}");
        }
    }
    #endregion

    #region [User32 Imports]
#pragma warning disable CS0414
    static int SW_HIDE = 0;
    static int SW_SHOWNORMAL = 1;
    static int SW_SHOWMINIMIZED = 2;
    static int SW_SHOWMAXIMIZED = 3;
    static int SW_SHOWNOACTIVATE = 4;
    static int SW_SHOW = 5;
    static int SW_MINIMIZE = 6;
    static int SW_SHOWMINNOACTIVE = 7;
    static int SW_SHOWNA = 8;
    static int SW_RESTORE = 9;
    static int SW_SHOWDEFAULT = 10;
    static int SW_FORCEMINIMIZE = 11;
#pragma warning restore CS0414
    [DllImport("User32.dll")]
    internal static extern bool ShowWindow(IntPtr handle, int nCmdShow);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetActiveWindow();

    [StructLayout(LayoutKind.Sequential)]
    struct ScreenRect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }
    delegate bool MonitorEnumProc(IntPtr hDesktop, IntPtr hdc, ref ScreenRect pRect, int dwData);

    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lpRect, MonitorEnumProc callback, int dwData);
    #endregion
}
