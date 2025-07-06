using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Mapper;

public enum MouseButton
{
    Left,
    Right,
    Middle,
    XButton1,
    XButton2
}

/// <summary>
/// Helper class that registers for Raw Input and exposes high level events.
/// </summary>
public class RawInputHandler
{
    private readonly IntPtr _hwnd;

    public event Action<int, int>? OnMouseDelta;
    public event Action<MouseButton, bool>? OnMouseButton;
    public event Action<Keys, bool>? OnKeyEvent;

    private const int WM_INPUT = 0x00FF;
    private const int RIM_TYPEMOUSE = 0;
    private const int RIM_TYPEKEYBOARD = 1;
    private const uint RID_INPUT = 0x10000003;
    private const uint RIDEV_INPUTSINK = 0x00000100;

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
    private struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RAWINPUTUNION
    {
        [FieldOffset(0)] public RAWMOUSE mouse;
        [FieldOffset(0)] public RAWKEYBOARD keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER header;
        public RAWINPUTUNION data;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    private static readonly uint RAWINPUTHEADER_SIZE = (uint)Marshal.SizeOf<RAWINPUTHEADER>();

    public RawInputHandler(IntPtr hwnd)
    {
        _hwnd = hwnd;
        RegisterDevices();
    }

    private void RegisterDevices()
    {
        RAWINPUTDEVICE[] devices = new[]
        {
            new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x02, dwFlags = RIDEV_INPUTSINK, hwndTarget = _hwnd },
            new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x06, dwFlags = RIDEV_INPUTSINK, hwndTarget = _hwnd }
        };
        if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public void ProcessMessage(Message m)
    {
        if (m.Msg != WM_INPUT)
            return;

        uint size = 0;
        GetRawInputData(m.LParam, RID_INPUT, IntPtr.Zero, ref size, RAWINPUTHEADER_SIZE);
        if (size == 0)
            return;

        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(m.LParam, RID_INPUT, buffer, ref size, RAWINPUTHEADER_SIZE) != size)
                return;

            RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
            if (raw.header.dwType == RIM_TYPEMOUSE)
            {
                var mouse = raw.data.mouse;
                if (mouse.lLastX != 0 || mouse.lLastY != 0)
                    OnMouseDelta?.Invoke(mouse.lLastX, mouse.lLastY);
                if (mouse.usButtonFlags != 0)
                    HandleMouseButtons(mouse.usButtonFlags);
            }
            else if (raw.header.dwType == RIM_TYPEKEYBOARD)
            {
                var keyboard = raw.data.keyboard;
                bool pressed = (keyboard.Flags & 0x01) == 0;
                OnKeyEvent?.Invoke((Keys)keyboard.VKey, pressed);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void HandleMouseButtons(ushort flags)
    {
        const ushort LEFT_DOWN = 0x0001;
        const ushort LEFT_UP = 0x0002;
        const ushort RIGHT_DOWN = 0x0004;
        const ushort RIGHT_UP = 0x0008;
        const ushort MIDDLE_DOWN = 0x0010;
        const ushort MIDDLE_UP = 0x0020;
        const ushort BUTTON4_DOWN = 0x0040;
        const ushort BUTTON4_UP = 0x0080;
        const ushort BUTTON5_DOWN = 0x0100;
        const ushort BUTTON5_UP = 0x0200;

        if ((flags & LEFT_DOWN) != 0) OnMouseButton?.Invoke(MouseButton.Left, true);
        if ((flags & LEFT_UP) != 0) OnMouseButton?.Invoke(MouseButton.Left, false);
        if ((flags & RIGHT_DOWN) != 0) OnMouseButton?.Invoke(MouseButton.Right, true);
        if ((flags & RIGHT_UP) != 0) OnMouseButton?.Invoke(MouseButton.Right, false);
        if ((flags & MIDDLE_DOWN) != 0) OnMouseButton?.Invoke(MouseButton.Middle, true);
        if ((flags & MIDDLE_UP) != 0) OnMouseButton?.Invoke(MouseButton.Middle, false);
        if ((flags & BUTTON4_DOWN) != 0) OnMouseButton?.Invoke(MouseButton.XButton1, true);
        if ((flags & BUTTON4_UP) != 0) OnMouseButton?.Invoke(MouseButton.XButton1, false);
        if ((flags & BUTTON5_DOWN) != 0) OnMouseButton?.Invoke(MouseButton.XButton2, true);
        if ((flags & BUTTON5_UP) != 0) OnMouseButton?.Invoke(MouseButton.XButton2, false);
    }
}

