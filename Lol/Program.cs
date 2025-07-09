using System;
using System.Collections.Generic;
using System.Management; // WMI for mouse enumeration
using System.Runtime.InteropServices;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace Lol;

internal static class Program
{
    private const uint RIDEV_INPUTSINK = 0x00000100;
    private const uint RID_INPUT = 0x10000003;
    private const int RIM_TYPEMOUSE = 0;

    private const int DefaultDpi = 800;
    // Raw input structures
    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWMOUSE
    {
        public ushort usFlags;
        public uint ulButtons;
        public ushort usButtonFlags;
        public ushort usButtonData;
        public uint ulRawButtons;
        public int lLastX;
        public int lLastY;
        public uint ulExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER header;
        public RAWMOUSE mouse;
    }

    // Win32 P/Invoke declarations
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, out RAWINPUT pData, ref uint pcbSize, uint cbSizeHeader);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpmsg);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    private const int WM_INPUT = 0x00FF;
    private const int WM_KEYDOWN = 0x0100;

    // Map device handle -> detected DPI

    private static ViGEmClient? _client;
    private static IXbox360Controller? _controller;

    private static void Main()
    {
        Console.WriteLine("Initializing ViGEm...");
        _client = new ViGEmClient();
        _controller = _client.CreateXbox360Controller();
        _controller.Connect();

        DetectMice();
        RegisterForRawInput();

        Console.WriteLine("Listening for raw mouse input. Press ESC to exit.");

        MSG msg;
        while (GetMessage(out msg, IntPtr.Zero, 0, 0))
        {
            if (msg.message == WM_INPUT)
            {
                ProcessRawInput(msg.lParam);
            }

            if (msg.message == WM_KEYDOWN && (int)msg.wParam == 0x1B) // ESC
            {
                break;
            }

            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        _controller.Disconnect();
        _client.Dispose();
    }

    private static void DetectMice()
    {
        Console.WriteLine("Connected mice and DPI (best effort):");
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PointingDevice");
            foreach (ManagementObject obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? "Unknown";
                int dpi = obj["Resolution"] is not null ? Convert.ToInt32(obj["Resolution"]) : 800; // many drivers don't expose Resolution
                Console.WriteLine($" - {name}, DPI: {dpi}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Mouse enumeration failed: {ex.Message}");
        }

        // When WM_INPUT messages arrive we only know the device handle.
        // DPI detection per-device via HID is vendor specific and omitted here.
    }

    private static void RegisterForRawInput()
    {
        var rid = new RAWINPUTDEVICE[1];
        rid[0].usUsagePage = 0x01; // generic desktop controls
        rid[0].usUsage = 0x02;     // mouse
        rid[0].dwFlags = RIDEV_INPUTSINK; // receive input even when not focused
        rid[0].hwndTarget = GetConsoleWindow();

        if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
        {
            Console.Error.WriteLine($"Failed to register raw input: {Marshal.GetLastWin32Error()}");
        }
    }

    private static void ProcessRawInput(IntPtr hRawInput)
    {
        uint dwSize = 0;
        GetRawInputData(hRawInput, RID_INPUT, out _, ref dwSize, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
        if (dwSize == 0)
            return;

        if (GetRawInputData(hRawInput, RID_INPUT, out RAWINPUT raw, ref dwSize, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) != dwSize)
            return;

        if (raw.header.dwType != RIM_TYPEMOUSE)
            return;

        IntPtr device = raw.header.hDevice;
        short rx = ApplyCurve(raw.mouse.lLastX, DefaultDpi);
        short ry = (short)-ApplyCurve(raw.mouse.lLastY, DefaultDpi); // invert Y

        _controller.SetAxisValue(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Axis.RightThumbX, (ushort)rx);
        _controller.SetAxisValue(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Axis.RightThumbY, (ushort)ry);
        _controller.SubmitReport();
    }

    private static short ApplyCurve(int delta, int dpi)
    {
        // Normalize by DPI to get inches moved, then apply a simple power curve.
        float normalized = (float)delta / dpi;
        float curved = MathF.Sign(normalized) * MathF.Pow(MathF.Abs(normalized), 1.3f);
        float value = curved * 32767f; // scale to short range
        value = Math.Clamp(value, -32767f, 32767f);
        return (short)value;
    }
}
