using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Core;
// Reference to the Core library for Wooting SDK P/Invokes

namespace InputToControllerMapper;

public class MainForm : Form
{
    private readonly TextBox _logBox;
    private readonly System.Windows.Forms.Timer _analogTimer;

    // Win32 constants
    private const int WM_INPUT = 0x00FF;
    private const uint RIDEV_INPUTSINK = 0x00000100;
    private const uint RID_INPUT = 0x10000003;
    private static readonly uint RAWINPUTHEADER_SIZE = (uint)Marshal.SizeOf<RAWINPUTHEADER>();

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    public MainForm()
    {
        Text = "Input Logger";
        Width = 800;
        Height = 600;

        _logBox = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical
        };
        Controls.Add(_logBox);

        _analogTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _analogTimer.Tick += AnalogTimer_Tick;
        _analogTimer.Start();

        // Initialize Wooting SDK
        try
        {
            WootingAnalog.wooting_analog_initialise();
        }
        catch (DllNotFoundException ex)
        {
            Log($"Wooting SDK DLL not found: {ex.Message}");
        }

        RegisterForRawMouse();
    }

    private void AnalogTimer_Tick(object? sender, EventArgs e)
    {
        // Example: read analog value for the "W" key (HID usage ID 0x1A)
        float value = 0f;
        try
        {
            value = WootingAnalog.wooting_analog_read_full(0x1A);
        }
        catch (Exception ex)
        {
            Log($"Analog read failed: {ex.Message}");
        }

        Log($"Analog W: {value:F3}");
    }

    private void RegisterForRawMouse()
    {
        RAWINPUTDEVICE[] rid = new[]
        {
            new RAWINPUTDEVICE
            {
                usUsagePage = 0x01, // Generic desktop controls
                usUsage = 0x02,     // Mouse
                dwFlags = RIDEV_INPUTSINK,
                hwndTarget = Handle
            }
        };

        if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
        {
            Log($"RegisterRawInputDevices failed with error {Marshal.GetLastWin32Error()}");
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT)
        {
            uint dwSize = 0;
            // First call to obtain the required buffer size
            GetRawInputData(m.LParam, RID_INPUT, IntPtr.Zero, ref dwSize, RAWINPUTHEADER_SIZE);
            if (dwSize > 0)
            {
                IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                try
                {
                    if (GetRawInputData(m.LParam, RID_INPUT, buffer, ref dwSize, RAWINPUTHEADER_SIZE) == dwSize)
                    {
                        RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
                        Log($"Mouse: X={raw.mouse.lLastX} Y={raw.mouse.lLastY}");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        base.WndProc(ref m);
    }

    private void Log(string message)
    {
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
