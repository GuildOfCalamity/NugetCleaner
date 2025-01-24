using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;

using Windows.ApplicationModel.Activation;
using Windows.Foundation;

using NugetCleaner.Support;
using Microsoft.UI.Content;
using Windows.Graphics;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace NugetCleaner;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private SUBCLASSPROC mainWindowSubClassProc;
    private ContentCoordinateConverter coordinateConverter;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ElementTheme WindowTheme { get; private set; }

    DesktopAcrylicController? _acrylicController;

    bool _isSpecialEnabled;
    public bool IsSpecialEnabled
    {
        get => _isSpecialEnabled;
        set
        {
            if (_isSpecialEnabled != value)
            {
                _isSpecialEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSpecialEnabled)));
            }
        }
    }
    public MainWindow()
    {
        Debug.WriteLine($"[INFO] {System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}__{System.Reflection.MethodBase.GetCurrentMethod()?.Name} [{DateTime.Now.ToString("hh:mm:ss.fff tt")}]");
        
        this.InitializeComponent();
        this.Closed += MainWindow_Closed;

        if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
        {
            this.ExtendsContentIntoTitleBar = true;
            this.Title = $"{App.GetCurrentAssemblyName()}";
            SetTitleBar(CustomTitleBar);
        }

        CreateGradientBackdrop(root, new System.Numerics.Vector2(0.9f, 1));

        #region [Window Messages]
        // Create a coordinate converter for displaying our MenuFlyout.
        coordinateConverter = ContentCoordinateConverter.CreateForWindowId(AppWindow.Id);
        // Add a delegate procedure for the app's main window.
        mainWindowSubClassProc = new SUBCLASSPROC(MainWindowSubClassProc);
        Comctl32Helper.SetWindowSubclass((IntPtr)AppWindow.Id.Value, Marshal.GetFunctionPointerForDelegate(mainWindowSubClassProc), 0, IntPtr.Zero);
        #endregion
    }

    #region [Window Messages]
    IntPtr MainWindowSubClassProc(IntPtr hWnd, WindowMessage wMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        /* [typical startup sequence]
        WindowMessageReceived: 'WM_SETICON'
        WindowMessageReceived: 'WM_SHOWWINDOW'
        WindowMessageReceived: 'WM_WINDOWPOSCHANGING'
        WindowMessageReceived: 'WM_ACTIVATEAPP'
        WindowMessageReceived: 'WM_NCACTIVATE'
        WindowMessageReceived: 'WM_GETICON'
        WindowMessageReceived: 'WM_ACTIVATE'
        WindowMessageReceived: 'WM_GETTEXTLENGTH'
        WindowMessageReceived: 'WM_GETTEXT'
        WindowMessageReceived: 'WM_NCPAINT'
        WindowMessageReceived: 'WM_ERASEBKGND'
        */

        // Don't process messages if we're shutting down.
        if (App.IsClosing)
            return Comctl32Helper.DefSubclassProc(hWnd, wMsg, wParam, lParam);

        switch (wMsg)
        {
            // Sent to a window when the size or position of the window is about to change.
            // An application can use this message to override the window's default maximized
            // size and position, or its default minimum or maximum tracking size.
            // https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-getminmaxinfo
            case WindowMessage.WM_GETMINMAXINFO:
                {
                    Debug.WriteLine($"[INFO] Get min/max info message detected: '{wMsg}' ");

                    if (Content is not null && Content.XamlRoot is not null)
                    {
                        MINMAXINFO minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                        minMaxInfo.ptMinTrackSize.X = (int)(960 * Content.XamlRoot.RasterizationScale);
                        minMaxInfo.ptMinTrackSize.Y = (int)(600 * Content.XamlRoot.RasterizationScale);
                        Marshal.StructureToPtr(minMaxInfo, lParam, true);
                    }
                    break;
                }

            // A message that is sent to all top-level windows when the SystemParametersInfo
            // function changes a system-wide setting or when policy settings have changed.
            // https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-settingchange
            case WindowMessage.WM_SETTINGCHANGE:
                {
                    Debug.WriteLine($"[INFO] Setting change message detected: '{wMsg}' ");

                    if (Application.Current.RequestedTheme is ApplicationTheme.Light)
                    {
                        WindowTheme = ElementTheme.Light;
                    }
                    else
                    {
                        WindowTheme = ElementTheme.Dark;
                    }
                    break;
                }

            // Broadcast to every window following a theme change event.
            // Examples of theme change events are the activation of a theme,
            // the deactivation of a theme, or a transition from one theme to another.
            // https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-themechanged
            case WindowMessage.WM_THEMECHANGED:
                {
                    Debug.WriteLine($"[INFO] Theme change message detected: '{wMsg}' ");
                    break;
                }

            // An application sends the WM_COPYDATA message to pass data to another application.
            // https://learn.microsoft.com/en-us/windows/win32/dataxchg/wm-copydata
            case WindowMessage.WM_COPYDATA:
                {
                    Debug.WriteLine($"[INFO] Copy data message detected: '{wMsg}' ");

                    COPYDATASTRUCT copyDataStruct = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);
                    
                    if ((ActivationKind)copyDataStruct.dwData is ActivationKind.Launch)
                    {
                        App.ActivateMainWindow();

                        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
                        {
                            //await ContentDialogHelper.ShowAsync(new AppRunningDialog(), Content as FrameworkElement);
                            await App.ShowContentDialog(
                                "ActivationKind.Launch", 
                                "WindowMessage.WM_COPYDATA => ActivationKind.Launch", 
                                "OK", 
                                "",
                                400,
                                null, 
                                null, 
                                null);
                        });
                    }
                    else if ((ActivationKind)copyDataStruct.dwData is ActivationKind.CommandLineLaunch ||
                             (ActivationKind)copyDataStruct.dwData is ActivationKind.ShareTarget)
                    {
                        string[] startupArgs = copyDataStruct.lpData.Split(' ');
                    
                        if (startupArgs.Length is 2 && (startupArgs[0] is "JumpList" || startupArgs[0] is "SecondaryTile"))
                        {
                            if (startupArgs[1] is "Store" )
                            {
                                //DispatcherQueue.TryEnqueue(() => { NavigateTo(typeof(StorePage)); });
                            }
                            if (startupArgs[1] is "AppUpdate")
                            {
                                //DispatcherQueue.TryEnqueue(() => { NavigateTo(typeof(AppUpdatePage)); });
                            }
                            else if (startupArgs[1] is "WinGet")
                            {
                                //DispatcherQueue.TryEnqueue(() => { NavigateTo(typeof(WinGetPage)); });
                            }
                            else if (startupArgs[1] is "UWPApp")
                            {
                                //DispatcherQueue.TryEnqueue(() => { NavigateTo(typeof(UWPAppPage)); });
                            }
                            else if (startupArgs[1] is "Download")
                            {
                                //DispatcherQueue.TryEnqueue(() => { NavigateTo(typeof(DownloadPage)); });
                            }
                        }
                        else if (startupArgs.Length is 3)
                        {
                            //DispatcherQueue.TryEnqueue(() => { NavigateTo(typeof(StorePage)); });
                        }
                    
                        App.ActivateMainWindow();
                    }
                    else if ((ActivationKind)copyDataStruct.dwData is ActivationKind.ToastNotification)
                    {
                        ToastHelper.ShowStandardToast("ActivationKind.ToastNotification", copyDataStruct.lpData);
                        //App.ActivateMainWindow();
                    }

                    break;
                }

            // A window receives this message when the user chooses a command from the Window menu
            // (formerly known as the system or control menu) or when the user chooses the maximize
            // button, minimize button, restore button, or close button.
            // https://learn.microsoft.com/en-us/windows/win32/menurc/wm-syscommand
            case WindowMessage.WM_SYSCOMMAND:
                {
                    Debug.WriteLine($"[INFO] System command message detected: '{wMsg}' ");

                    SYSTEMCOMMAND sysCommand = (SYSTEMCOMMAND)(wParam.ToInt32() & 0xFFF0);

                    if (sysCommand is SYSTEMCOMMAND.SC_MOUSEMENU)
                    {
                        if (Content is not null && Content.XamlRoot is not null)
                        {
                            FlyoutShowOptions options = new FlyoutShowOptions();
                            options.Position = new Point(0, 15);
                            options.ShowMode = FlyoutShowMode.Standard;
                            try
                            {
                                if (!TitlebarMenuFlyout.IsOpen && !App.IsClosing)
                                    TitlebarMenuFlyout.ShowAt(null, options);
                            }
                            catch (Exception) { }
                        }
                        return 0;
                    }
                    else if (sysCommand is SYSTEMCOMMAND.SC_KEYMENU)
                    {
                        if (Content is not null && Content.XamlRoot is not null)
                        {
                            FlyoutShowOptions options = new FlyoutShowOptions();
                            options.Position = new Point(0, 45);
                            options.ShowMode = FlyoutShowMode.Standard;
                            try
                            {
                                if (!TitlebarMenuFlyout.IsOpen && !App.IsClosing)
                                    TitlebarMenuFlyout.ShowAt(null, options);
                            }
                            catch (Exception) { }
                        }
                        return 0;
                    }
                    break;
                }

            // Posted when the user presses the left mouse button while the cursor is within the nonclient area of a window.
            // This message is posted to the window that contains the cursor. If a window has captured the mouse, this message
            // is not posted. https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nclbuttondown
            case WindowMessage.WM_NCLBUTTONDOWN:
                {
                    Debug.WriteLine($"[INFO] Left mouse button message detected: '{wMsg}' ");

                    if (Content is not null && Content.XamlRoot is not null)
                    {
                        if (TitlebarMenuFlyout.IsOpen && !App.IsClosing)
                        {
                            TitlebarMenuFlyout.Hide();
                        }
                    }
                    break;
                }

            // Posted when the user releases the right mouse button while the cursor is within the nonclient area of a window.
            // This message is posted to the window that contains the cursor. If a window has captured the mouse, this message
            // is not posted. https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-ncrbuttonup
            case WindowMessage.WM_NCRBUTTONUP:
                {
                    Debug.WriteLine($"[INFO] Right mouse button message detected: '{wMsg}' ");

                    if (Content is not null && Content.XamlRoot is not null)
                    {
                        try
                        {
                            PointInt32 screenPoint = new PointInt32(lParam.ToInt32() & 0xFFFF, lParam.ToInt32() >> 16);
                            Point localPoint = coordinateConverter.ConvertScreenToLocal(screenPoint);
                            FlyoutShowOptions options = new FlyoutShowOptions();
                            options.ShowMode = FlyoutShowMode.Standard;
                            options.Position = new Point(localPoint.X / Content.XamlRoot.RasterizationScale, localPoint.Y / Content.XamlRoot.RasterizationScale);
                            if (!App.IsClosing)
                                TitlebarMenuFlyout.ShowAt(Content, options);
                        }
                        catch (Exception) { }
                    }
                    return 0;
                }
            default:
                //Debug.WriteLine($"[{DateTime.Now.ToLongTimeString()}] MainWindowSubClass: '{wMsg}' ");
                break;
        }
        return Comctl32Helper.DefSubclassProc(hWnd, wMsg, wParam, lParam);
    }

    /// <summary>
    /// Shows our custom <see cref="TitlebarMenuFlyout"/> when the user right-click anywhere on the TitleBar area.
    /// </summary>
    IntPtr NonClientPointerSourceSubClassProc(IntPtr hWnd, WindowMessage wMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        switch (wMsg)
        {
            // Posted when the user presses the left mouse button while the cursor is within the nonclient area of a window.
            // This message is posted to the window that contains the cursor. If a window has captured the mouse, this message
            // is not posted.
            case WindowMessage.WM_NCLBUTTONDOWN:
                {
                    if (Content is not null && Content.XamlRoot is not null)
                    {
                        if (TitlebarMenuFlyout.IsOpen && !App.IsClosing)
                        {
                            TitlebarMenuFlyout.Hide();
                        }
                    }
                    break;
                }
            // Posted when the user releases the right mouse button while the cursor is within the nonclient area of a window.
            // This message is posted to the window that contains the cursor. If a window has captured the mouse, this message
            // is not posted.
            case WindowMessage.WM_NCRBUTTONUP:
                {
                    if (Content is not null && Content.XamlRoot is not null)
                    {
                        try
                        {
                            PointInt32 screenPoint = new PointInt32(lParam.ToInt32() & 0xFFFF, lParam.ToInt32() >> 16);
                            Point localPoint = coordinateConverter.ConvertScreenToLocal(screenPoint);
                            FlyoutShowOptions options = new FlyoutShowOptions();
                            options.ShowMode = FlyoutShowMode.Standard;
                            options.Position = new Point(localPoint.X / Content.XamlRoot.RasterizationScale, localPoint.Y / Content.XamlRoot.RasterizationScale);
                            if (!App.IsClosing)
                                TitlebarMenuFlyout.ShowAt(Content, options);
                        }
                        catch (Exception) { }
                    }
                    return 0;
                }
            default:
                //Debug.WriteLine($"[{DateTime.Now.ToLongTimeString()}] NonClientPointerSubClass: '{wMsg}' ");
                break;

        }
        return Comctl32Helper.DefSubclassProc(hWnd, wMsg, wParam, lParam);
    }
    #endregion

    void CreateGradientBackdrop(FrameworkElement fe, System.Numerics.Vector2 endPoint)
    {
        // Get the FrameworkElement's compositor.
        var compositor = ElementCompositionPreview.GetElementVisual(fe).Compositor;
        if (compositor == null) { return; }
        var gb = compositor.CreateLinearGradientBrush();

        // Define gradient stops.
        var gradientStops = gb.ColorStops;

        // If we found our App.xaml brushes then use them.
        if (App.Current.Resources.TryGetValue("GC1", out object clr1) &&
            App.Current.Resources.TryGetValue("GC2", out object clr2) &&
            App.Current.Resources.TryGetValue("GC3", out object clr3) &&
            App.Current.Resources.TryGetValue("GC4", out object clr4))
        {
            //var clr1 = (Windows.UI.Color)App.Current.Resources["GC1"];
            //var clr2 = (Windows.UI.Color)App.Current.Resources["GC2"];
            //var clr3 = (Windows.UI.Color)App.Current.Resources["GC3"];
            //var clr4 = (Windows.UI.Color)App.Current.Resources["GC4"];
            gradientStops.Insert(0, compositor.CreateColorGradientStop(0.0f, (Windows.UI.Color)clr1));
            gradientStops.Insert(1, compositor.CreateColorGradientStop(0.3f, (Windows.UI.Color)clr2));
            gradientStops.Insert(2, compositor.CreateColorGradientStop(0.6f, (Windows.UI.Color)clr3));
            gradientStops.Insert(3, compositor.CreateColorGradientStop(1.0f, (Windows.UI.Color)clr4));
        }
        else
        {
            gradientStops.Insert(0, compositor.CreateColorGradientStop(0.0f, Windows.UI.Color.FromArgb(55, 255, 0, 0)));   // Red
            gradientStops.Insert(1, compositor.CreateColorGradientStop(0.3f, Windows.UI.Color.FromArgb(55, 255, 216, 0))); // Yellow
            gradientStops.Insert(2, compositor.CreateColorGradientStop(0.6f, Windows.UI.Color.FromArgb(55, 0, 255, 0)));   // Green
            gradientStops.Insert(3, compositor.CreateColorGradientStop(1.0f, Windows.UI.Color.FromArgb(55, 0, 0, 255)));   // Blue
        }

        // Set the direction of the gradient.
        gb.StartPoint = new System.Numerics.Vector2(0, 0);
        //gb.EndPoint = new System.Numerics.Vector2(1, 1);
        gb.EndPoint = endPoint;

        // Create a sprite visual and assign the gradient brush.
        var spriteVisual = Compositor.CreateSpriteVisual();
        spriteVisual.Brush = gb;

        // Set the size of the sprite visual to cover the entire window.
        spriteVisual.Size = new System.Numerics.Vector2((float)fe.ActualSize.X, (float)fe.ActualSize.Y);

        // Handle the SizeChanged event to adjust the size of the sprite visual when the window is resized.
        fe.SizeChanged += (s, e) =>
        {
            spriteVisual.Size = new System.Numerics.Vector2((float)fe.ActualWidth, (float)fe.ActualHeight);
        };

        // Set the sprite visual as the background of the FrameworkElement.
        ElementCompositionPreview.SetElementChildVisual(fe, spriteVisual);
    }

    void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        // Make sure the Acrylic controller is disposed so it doesn't try to access a closed window.
        if (_acrylicController is not null)
        {
            _acrylicController.Dispose();
            _acrylicController = null;
        }
    }

    /// <summary>
    /// Communal event for <see cref="MenuFlyoutItem"/> clicks.
    /// The action performed will be based on the tag data.
    /// </summary>
    async void MenuFlyoutItemOnClick(object sender, RoutedEventArgs e)
    {
        var mfi = sender as MenuFlyoutItem;

        // Auto-hide if tag was passed like this ⇒ Tag="{x:Bind TitlebarMenuFlyout}"
        if (mfi is not null && mfi.Tag is not null && mfi.Tag is MenuFlyout mf) { mf?.Hide(); return; }

        if (mfi is not null && mfi.Tag is not null)
        {
            var tag = $"{mfi.Tag}";
            if (!string.IsNullOrEmpty(tag) && tag.Equals("ActionClose", StringComparison.OrdinalIgnoreCase))
            {
                if (this.Content is not null && !App.IsClosing)
                {
                    ContentDialogResult result = await DialogHelper.ShowAsync(new Dialogs.CloseAppDialog(), Content as FrameworkElement);
                    if (result is ContentDialogResult.Primary)
                    {   // The closing event will be picked up in App.xaml.cs
                        this.Close();
                    }
                    else if (result is ContentDialogResult.None)
                    {
                        Debug.WriteLine($"[INFO] User canceled the dialog.");
                    }
                }
            }
            else if (!string.IsNullOrEmpty(tag) && tag.Equals("ActionFirstRun", StringComparison.OrdinalIgnoreCase))
            {
                // Reset first run flag
                App.Profile!.FirstRun = true;
            }
            else
                    {
                Debug.WriteLine($"[WARNING] No action has been defined for '{tag}'.");
            }
        }
        else
        {
            Debug.WriteLine($"[WARNING] Tag data is empty for this MenuFlyoutItem.");
        }
    }
}
