using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace NugetCleaner.Support;

/// <summary>
/// Helper class to set TaskBar progress/state on Windows 7+.
/// The exposed calls utilize <see cref="ITaskbarList3"/>.
/// </summary>
public static class TaskbarProgress
{
    [GuidAttribute("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [ClassInterfaceAttribute(ClassInterfaceType.None)]
    [ComImportAttribute()]
    class TaskbarInstance { }

    static readonly bool taskbarSupported = IsWindows7OrLater;
    static readonly ITaskbarList3? taskbarInstance = taskbarSupported ? (ITaskbarList3)new TaskbarInstance() : null;

    /// <summary>
    /// Sets the state of the taskbar progress.
    /// </summary>
    /// <param name="windowHandle">current form handle</param>
    /// <param name="taskbarState">desired state</param>
    public static void SetState(IntPtr windowHandle, TaskbarStates taskbarState)
    {
        if (taskbarSupported)
        {
            taskbarInstance?.SetProgressState(windowHandle, taskbarState);
        }
    }

    /// <summary>
    /// Sets the value of the taskbar progress.
    /// </summary>
    /// <param name="windowHandle">currnet form handle</param>
    /// <param name="progressValue">desired progress value</param>
    /// <param name="progressMax">maximum progress value</param>
    public static void SetValue(IntPtr windowHandle, double progressValue, double progressMax)
    {
        if (taskbarSupported)
        {
            taskbarInstance?.SetProgressValue(windowHandle, (ulong)progressValue, (ulong)progressMax);
        }
    }

    /// <summary>
    /// Determines if current operating system is Windows 7+.
    /// </summary>
    public static bool IsWindows7OrLater => Environment.OSVersion.Version >= new Version(6, 1);

    /// <summary>
    /// Deteremines if current operating system is Vista+.
    /// </summary>
    public static bool IsWindowsVistaOrLater => Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version >= new Version(6, 0, 6000);


    #region [Enumerations]
    /// <summary>
    /// Available taskbar progress states
    /// </summary>
    public enum TaskbarStates
    {
        /// <summary>No progress displayed</summary>
        NoProgress = 0,
        /// <summary>Indeterminate</summary>
        Indeterminate = 0x1,
        /// <summary>Normal</summary>
        Normal = 0x2,
        /// <summary>Error</summary>
        Error = 0x4,
        /// <summary>Paused</summary>
        Paused = 0x8
    }

    /// <summary>
    /// Flags for SetTabProperties (STPF)
    /// </summary>
    /// <remarks>The native enum was called STPFLAG.</remarks>
    [Flags]
    internal enum STPF
    {
        NONE = 0x00000000,
        USEAPPTHUMBNAILALWAYS = 0x00000001,
        USEAPPTHUMBNAILWHENACTIVE = 0x00000002,
        USEAPPPEEKALWAYS = 0x00000004,
        USEAPPPEEKWHENACTIVE = 0x00000008,
    }

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/ne-shobjidl_core-thumbbuttonmask
    /// </summary>
    enum THUMBBUTTONMASK
    {
        THB_BITMAP = 0x1,
        THB_ICON = 0x2,
        THB_TOOLTIP = 0x4,
        THB_FLAGS = 0x8
    };

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/ne-shobjidl_core-thumbbuttonflags
    /// </summary>
    enum THUMBBUTTONFLAGS
    {
        THBF_ENABLED = 0,
        THBF_DISABLED = 0x1,
        THBF_DISMISSONCLICK = 0x2,
        THBF_NOBACKGROUND = 0x4,
        THBF_HIDDEN = 0x8,
        THBF_NONINTERACTIVE = 0x10
    };

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/ns-shobjidl_core-thumbbutton
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct THUMBBUTTON
    {
        public THUMBBUTTONMASK dwMask;
        public uint iId;               
        public uint iBitmap;           
        public IntPtr hIcon;           
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szTip;           
        public THUMBBUTTONFLAGS dwFlags;
    }
    #endregion


    #region [Interface Definitions]

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-itaskbarlist
    /// </summary>
    [ComImportAttribute()]
    [GuidAttribute("56FDF342-FD6D-11d0-958A-006097C9A090")]
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList
    {
        /// <summary>
        /// This function must be called first to validate use of other members.
        /// </summary>
        void HrInit();

        /// <summary>
        /// This function adds a tab for hwnd to the taskbar.
        /// </summary>
        /// <param name="hwnd">The HWND for which to add the tab.</param>
        void AddTab(IntPtr hwnd);

        /// <summary>
        /// This function deletes a tab for hwnd from the taskbar.
        /// </summary>
        /// <param name="hwnd">The HWND for which the tab is to be deleted.</param>
        void DeleteTab(IntPtr hwnd);

        /// <summary>
        /// This function activates the tab associated with hwnd on the taskbar.
        /// </summary>
        /// <param name="hwnd">The HWND for which the tab is to be activated.</param>
        void ActivateTab(IntPtr hwnd);

        /// <summary>
        /// This function marks hwnd in the taskbar as the active tab.
        /// </summary>
        /// <param name="hwnd">The HWND to activate.</param>
        void SetActiveAlt(IntPtr hwnd);
    }

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-itaskbarlist2
    /// </summary>
    [ComImportAttribute()]
    [GuidAttribute("602D4995-B13A-429b-A66E-1935E44F4317")]
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList2
    {
        [PreserveSig]
        void HrInit();
        [PreserveSig]
        void AddTab(IntPtr hwnd);
        [PreserveSig]
        void DeleteTab(IntPtr hwnd);
        [PreserveSig]
        void ActivateTab(IntPtr hwnd);
        [PreserveSig]
        void SetActiveAlt(IntPtr hwnd);

        /// <summary>
        /// Marks a window as full-screen.
        /// </summary>
        /// <param name="hwnd">The handle of the window to be marked.</param>
        /// <param name="fFullscreen">A Boolean value marking the desired full-screen status of the window.</param>
        /// <remarks>
        /// Setting the value of fFullscreen to true, the Shell treats this window as a full-screen window, and the taskbar
        /// is moved to the bottom of the z-order when this window is active.  Setting the value of fFullscreen to false
        /// removes the full-screen marking, but <i>does not</i> cause the Shell to treat the window as though it were
        /// definitely not full-screen.  With a false fFullscreen value, the Shell depends on its automatic detection facility
        /// to specify how the window should be treated, possibly still flagging the window as full-screen.
        /// </remarks>
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
    }

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-itaskbarlist3
    /// </summary>
    [ComImportAttribute()]
    [GuidAttribute("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList3
    {
        // ITaskbarList
        [PreserveSig]
        void HrInit();
        [PreserveSig]
        void AddTab(IntPtr hwnd);
        [PreserveSig]
        void DeleteTab(IntPtr hwnd);
        [PreserveSig]
        void ActivateTab(IntPtr hwnd);
        [PreserveSig]
        void SetActiveAlt(IntPtr hwnd);

        // ITaskbarList2
        [PreserveSig]
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

        // ITaskbarList3
        [PreserveSig]
        void SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);
        [PreserveSig]
        void SetProgressState(IntPtr hwnd, TaskbarStates state);
    }

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-itaskbarlist4
    /// </summary>
    [ComImportAttribute()]
    [GuidAttribute("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList4
    {
        // ITaskbarList
        [PreserveSig] void HrInit();
        [PreserveSig] void AddTab(IntPtr hwnd);
        [PreserveSig] void DeleteTab(IntPtr hwnd);
        [PreserveSig] void ActivateTab(IntPtr hwnd);
        [PreserveSig] void SetActiveAlt(IntPtr hwnd);

        // ITaskbarList2
        [PreserveSig] void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

        // ITaskbarList3
        [PreserveSig] void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
        [PreserveSig] void SetProgressState(IntPtr hwnd, TaskbarStates tbpFlags);
        [PreserveSig] int RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
        [PreserveSig] int UnregisterTab(IntPtr hwndTab);
        [PreserveSig] int SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
        [PreserveSig] int SetTabActive(IntPtr hwndTab, IntPtr hwndMDI, uint dwReserved);
        [PreserveSig] int ThumbBarAddButtons(IntPtr hwnd, uint cButtons, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] THUMBBUTTON[] pButtons);
        [PreserveSig] int ThumbBarUpdateButtons(IntPtr hwnd, uint cButtons, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] THUMBBUTTON[] pButtons);
        [PreserveSig] int ThumbBarSetImageList(IntPtr hwnd, [MarshalAs(UnmanagedType.IUnknown)] object himl);
        [PreserveSig] int SetOverlayIcon(IntPtr hwnd, IntPtr hIcon, [MarshalAs(UnmanagedType.LPWStr)] string pszDescription);
        [PreserveSig] int SetThumbnailTooltip(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string pszTip);
        [PreserveSig] int SetThumbnailClip(IntPtr hwnd, RefRECT prcClip); // Using RefRECT to make passing NULL possible.  Removes clipping from the HWND.

        // ITaskbarList4
        int SetTabProperties(IntPtr hwndTab, STPF stpFlags);
    }
    
    #endregion

}

#region [Support Classes]

[StructLayout(LayoutKind.Sequential), SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses")]
internal class RefRECT
{
    int _left;
    int _top;
    int _right;
    int _bottom;
    public RefRECT(int left, int top, int right, int bottom)
    {
        this._left = left;
        this._top = top;
        this._right = right;
        this._bottom = bottom;
    }
    public int Width
    {
        get => (this._right - this._left);
    }
    public int Height
    {
        get => (this._bottom - this._top);
    }
    public int Left
    {
        get => this._left;
        set => this._left = value;
    }
    public int Right
    {
        get => this._right;
        set => this._right = value;
    }
    public int Top
    {
        get => this._top;
        set => this._top = value;
    }
    public int Bottom
    {
        get => this._bottom;
        set => this._bottom = value;
    }
    public void Offset(int dx, int dy)
    {
        this._left += dx;
        this._top += dy;
        this._right += dx;
        this._bottom += dy;
    }
}

#endregion