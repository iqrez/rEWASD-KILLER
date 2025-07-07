using System;
using System.Runtime.InteropServices;

namespace RawMouseInput
{
    // Raw input bypasses OS mouse acceleration for lowest latency.
    // Avoid heavy processing in the message loop to prevent missed events.
    // Minimal console application for capturing raw mouse input via WM_INPUT.
    // Target: .NET 8 (use `dotnet build` with `net8.0` if compiling).
    internal class Program
    {
        private const int RIDEV_INPUTSINK = 0x00000100; // Receive input even when not in focus.
        private const int RID_INPUT = 0x10000003;       // Raw input from device.
        private const int RIM_TYPEMOUSE = 0;            // Mouse input type.

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

        [DllImport("User32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("User32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, out RAWINPUT pData, ref uint pcbSize, uint cbSizeHeader);

        private static void Main()
        {
            // Register for raw mouse input
            var rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01; // Generic desktop controls
            rid[0].usUsage = 0x02; // Mouse
            rid[0].dwFlags = RIDEV_INPUTSINK; // Receive input even when not focused
            rid[0].hwndTarget = GetConsoleWindow();

            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            {
                Console.Error.WriteLine("Failed to register raw input devices. Error: {0}", Marshal.GetLastWin32Error());
                return;
            }

            Console.WriteLine("Listening for raw mouse input. Press ESC to exit.");

            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0))
            {
                if (msg.message == WM_INPUT)
                {
                    ProcessRawInput(msg.lParam);
                }

                if (msg.message == WM_KEYDOWN && (int)msg.wParam == 0x1B) // ESC key
                {
                    break;
                }

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        private static void ProcessRawInput(IntPtr hRawInput)
        {
            uint dwSize = 0;
            GetRawInputData(hRawInput, RID_INPUT, out _, ref dwSize, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            if (dwSize == 0)
                return;

            if (GetRawInputData(hRawInput, RID_INPUT, out RAWINPUT raw, ref dwSize, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) != dwSize)
            {
                Console.Error.WriteLine("GetRawInputData did not return correct size. Error: {0}", Marshal.GetLastWin32Error());
                return;
            }

            if (raw.header.dwType == RIM_TYPEMOUSE)
            {
                // Output delta X, delta Y, timestamp and device handle.
                Console.WriteLine($"Device: 0x{raw.header.hDevice.ToInt64():X}, dX: {raw.mouse.lLastX}, dY: {raw.mouse.lLastY}, time: {DateTime.Now:O}");
            }
        }

        #region Win32 Message Loop Interop

        private const int WM_INPUT = 0x00FF;
        private const int WM_KEYDOWN = 0x0100;

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

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage([In] ref MSG lpmsg);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        #endregion
    }
}

