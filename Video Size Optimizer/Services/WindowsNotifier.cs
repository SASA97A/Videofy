using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Video_Size_Optimizer.Services;

/// <summary>
/// Shows a native Windows notification (a toast on Windows 10/11) through Shell_NotifyIcon,
/// using the icon embedded in Videofy.exe. No PowerShell, no extra packages.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsNotifier
{
    private const uint NotifyIconId = 0x5646; // "VF"
    private const int DisplayMilliseconds = 8000;

    public static async Task ShowAsync(IntPtr ownerHwnd, string title, string message)
    {
        if (ownerHwnd == IntPtr.Zero)
            throw new InvalidOperationException("No window handle to attach the notification to.");

        // Large icon for the notification body, small icon for the temporary tray entry.
        string exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Process path unknown.");
        int hr = SHDefExtractIcon(exePath, 0, 0, out IntPtr largeIcon, out IntPtr smallIcon, MakeIconSize(64, 16));
        if (hr != 0 || largeIcon == IntPtr.Zero)
            throw new InvalidOperationException($"Could not load the app icon (HRESULT 0x{hr:X8}).");

        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = ownerHwnd,
            uID = NotifyIconId,
            uFlags = NIF_ICON | NIF_TIP | NIF_INFO,
            hIcon = smallIcon != IntPtr.Zero ? smallIcon : largeIcon,
            szTip = "Videofy",
            szInfoTitle = Truncate(title, 63),
            szInfo = Truncate(message, 255),
            dwInfoFlags = NIIF_USER | NIIF_LARGE_ICON,
            hBalloonIcon = largeIcon,
        };

        try
        {
            // Remove a leftover entry from an earlier notification, if any.
            Shell_NotifyIcon(NIM_DELETE, ref data);

            if (!Shell_NotifyIcon(NIM_ADD, ref data))
                throw new InvalidOperationException("Shell_NotifyIcon failed to show the notification.");

            // Keep the tray entry alive long enough for the notification to be shown.
            await Task.Delay(DisplayMilliseconds);
        }
        finally
        {
            Shell_NotifyIcon(NIM_DELETE, ref data);
            DestroyIcon(largeIcon);
            if (smallIcon != IntPtr.Zero) DestroyIcon(smallIcon);
        }
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    private static uint MakeIconSize(int large, int small) => (uint)(large & 0xFFFF) | ((uint)(small & 0xFFFF) << 16);

    private const uint NIM_ADD = 0x0;
    private const uint NIM_DELETE = 0x2;
    private const uint NIF_ICON = 0x2;
    private const uint NIF_TIP = 0x4;
    private const uint NIF_INFO = 0x10;
    private const uint NIIF_USER = 0x4;
    private const uint NIIF_LARGE_ICON = 0x20;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHDefExtractIconW")]
    private static extern int SHDefExtractIcon(string pszIconFile, int iIndex, uint uFlags,
        out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIconSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
