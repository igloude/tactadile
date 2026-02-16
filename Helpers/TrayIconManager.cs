using System.Runtime.InteropServices;
using Tactadile.Native;

namespace Tactadile.Helpers;

public sealed class TrayIconManager : IDisposable
{
    private const int CMD_SETTINGS = 1;
    private const int CMD_ABOUT = 2;
    private const int CMD_EXIT = 3;
    private const uint TRAY_ICON_ID = 1;

    private IntPtr _hwnd;
    private IntPtr _hIcon;
    private NOTIFYICONDATA _nid;
    private readonly WndProc _wndProcDelegate; // prevent GC — same pattern as keyboard hook

    private readonly Action _onShowSettings;
    private readonly Action _onShowAbout;
    private readonly Action _onExit;

    public TrayIconManager(Action onShowSettings, Action onShowAbout, Action onExit)
    {
        _onShowSettings = onShowSettings;
        _onShowAbout = onShowAbout;
        _onExit = onExit;
        _wndProcDelegate = WndProcCallback;
    }

    public void Show()
    {
        var hInstance = NativeMethods.GetModuleHandle(null);

        var wc = new WNDCLASS
        {
            lpfnWndProc = _wndProcDelegate,
            lpszClassName = "Tactadile_TrayWnd",
            hInstance = hInstance
        };
        NativeMethods.RegisterClass(ref wc);

        _hwnd = NativeMethods.CreateWindowEx(
            0, wc.lpszClassName, "", 0,
            0, 0, 0, 0,
            NativeConstants.HWND_MESSAGE,
            IntPtr.Zero, hInstance, IntPtr.Zero);

        _hIcon = LoadAppIcon();

        _nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = TRAY_ICON_ID,
            uFlags = (uint)(NativeConstants.NIF_MESSAGE | NativeConstants.NIF_ICON | NativeConstants.NIF_TIP),
            uCallbackMessage = (uint)NativeConstants.WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "Tactadile"
        };

        NativeMethods.Shell_NotifyIcon(NativeConstants.NIM_ADD, ref _nid);

        // Set to version 4 for modern callback behavior
        _nid.uVersion = (uint)NativeConstants.NOTIFYICON_VERSION_4;
        NativeMethods.Shell_NotifyIcon(NativeConstants.NIM_SETVERSION, ref _nid);
    }

    public void ShowNotification(string title, string message)
    {
        _nid.uFlags = (uint)(NativeConstants.NIF_INFO);
        _nid.szInfoTitle = title;
        _nid.szInfo = message;
        NativeMethods.Shell_NotifyIcon(NativeConstants.NIM_MODIFY, ref _nid);

        // Reset flags
        _nid.uFlags = (uint)(NativeConstants.NIF_MESSAGE | NativeConstants.NIF_ICON | NativeConstants.NIF_TIP);
    }

    private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == (uint)NativeConstants.WM_TRAYICON)
        {
            int eventId = (int)(lParam.ToInt64() & 0xFFFF);
            if (eventId == NativeConstants.WM_RBUTTONUP)
                ShowContextMenu();
            else if (eventId == NativeConstants.WM_LBUTTONDBLCLK)
                _onShowSettings();
        }
        else if (msg == NativeConstants.WM_COMMAND)
        {
            int cmd = (int)(wParam.ToInt64() & 0xFFFF);
            switch (cmd)
            {
                case CMD_SETTINGS: _onShowSettings(); break;
                case CMD_ABOUT: _onShowAbout(); break;
                case CMD_EXIT: _onExit(); break;
            }
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        NativeMethods.GetCursorPos(out POINT pt);

        var hMenu = NativeMethods.CreatePopupMenu();
        NativeMethods.AppendMenu(hMenu, NativeConstants.MF_STRING, CMD_SETTINGS, "Settings");
        NativeMethods.AppendMenu(hMenu, NativeConstants.MF_STRING, CMD_ABOUT, "About");
        NativeMethods.AppendMenu(hMenu, NativeConstants.MF_SEPARATOR, 0, null);
        NativeMethods.AppendMenu(hMenu, NativeConstants.MF_STRING, CMD_EXIT, "Exit");

        // Required workaround: SetForegroundWindow before TrackPopupMenu
        // so the menu dismisses when the user clicks elsewhere
        NativeMethods.SetForegroundWindow(_hwnd);

        int cmd = NativeMethods.TrackPopupMenu(hMenu,
            NativeConstants.TPM_LEFTALIGN | NativeConstants.TPM_BOTTOMALIGN | NativeConstants.TPM_RETURNCMD,
            pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);

        NativeMethods.DestroyMenu(hMenu);

        if (cmd > 0)
        {
            NativeMethods.PostMessage(_hwnd, NativeConstants.WM_COMMAND,
                (IntPtr)cmd, IntPtr.Zero);
        }
    }

    private static IntPtr LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
        if (File.Exists(iconPath))
        {
            var hIcon = NativeMethods.LoadImage(
                IntPtr.Zero, iconPath,
                NativeConstants.IMAGE_ICON,
                16, 16,
                NativeConstants.LR_LOADFROMFILE);
            if (hIcon != IntPtr.Zero)
                return hIcon;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        NativeMethods.Shell_NotifyIcon(NativeConstants.NIM_DELETE, ref _nid);

        if (_hIcon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }

        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }
}
