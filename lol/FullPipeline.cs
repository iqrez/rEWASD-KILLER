using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace Lol;

/// <summary>
/// Single-file application that captures raw mouse input and maps it to an
/// Xbox 360 controller using ViGEm. Includes DPI detection (with fallback),
/// configurable sensitivity curves and clean resource management.
/// </summary>
internal static class FullPipeline
{
    ///////////////// RAW INPUT SETUP ///////////////////
    private const uint RIDEV_INPUTSINK = 0x00000100;
    private const uint RID_INPUT = 0x10000003;
    private const int RIM_TYPEMOUSE = 0;

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
    private struct POINT { public int x; public int y; }

    private const int WM_INPUT = 0x00FF;
    private const int WM_KEYDOWN = 0x0100;

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

    ////////////////// DPI DETECTION //////////////////////
    private const int DefaultDpi = 800;
    private static readonly Dictionary<IntPtr, int> _dpiCache = new();

    public static int DetectDpi(IntPtr device, out string msg)
    {
        msg = string.Empty;
        string? path = GetDevicePath(device);
        if (string.IsNullOrEmpty(path))
        {
            msg = "Unable to resolve device path";
            return DefaultDpi;
        }

        using SafeFileHandle handle = CreateFile(path, FileAccess.ReadWrite, FileShare.ReadWrite,
            IntPtr.Zero, FileMode.Open, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            msg = "Failed to open HID device";
            return DefaultDpi;
        }

        if (!HidD_GetPreparsedData(handle, out IntPtr preparsed))
        {
            msg = "Could not get HID preparsed data";
            return DefaultDpi;
        }

        try
        {
            if (HidP_GetCaps(preparsed, out HIDP_CAPS caps) != HIDP_STATUS_SUCCESS)
            {
                msg = "Unable to read HID capabilities";
                return DefaultDpi;
            }

            ushort capCount = caps.NumberInputValueCaps;
            HIDP_VALUE_CAPS[] valueCaps = new HIDP_VALUE_CAPS[capCount];
            if (HidP_GetValueCaps(HIDP_REPORT_TYPE.Input, valueCaps, ref capCount, preparsed) != HIDP_STATUS_SUCCESS)
            {
                msg = "Unable to read HID value caps";
                return DefaultDpi;
            }

            var xCap = valueCaps.FirstOrDefault(c => c.UsagePage == 0x01 && c.NotRange && c.Usage == 0x30);
            if (xCap.Unit == 0)
            {
                msg = "HID descriptor lacks unit info";
                return DefaultDpi;
            }

            double unitsPerInch = HidUnitsToInches(xCap.Unit, xCap.UnitExp);
            if (unitsPerInch <= 0)
            {
                msg = "Unsupported HID units";
                return DefaultDpi;
            }

            int counts = xCap.LogicalMax - xCap.LogicalMin + 1;
            int dpi = (int)(counts / unitsPerInch);
            msg = "DPI derived from HID";
            return dpi;
        }
        finally
        {
            HidD_FreePreparsedData(preparsed);
        }
    }

    private static double HidUnitsToInches(uint unit, short unitExp)
    {
        const uint HID_UNIT_ENGLISH_LINEAR = 0x01;
        if ((unit & 0x0F) != HID_UNIT_ENGLISH_LINEAR)
            return -1;
        return Math.Pow(10, unitExp);
    }

    private const uint RIDI_DEVICENAME = 0x20000007;

    [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

    private static string? GetDevicePath(IntPtr deviceHandle)
    {
        uint size = 0;
        GetRawInputDeviceInfo(deviceHandle, RIDI_DEVICENAME, IntPtr.Zero, ref size);
        if (size == 0)
            return null;
        IntPtr buffer = Marshal.AllocHGlobal((int)size * 2);
        try
        {
            if (GetRawInputDeviceInfo(deviceHandle, RIDI_DEVICENAME, buffer, ref size) == 0)
                return null;
            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(string lpFileName, [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
        [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode, IntPtr lpSecurityAttributes, [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
        [MarshalAs(UnmanagedType.U4)] uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("hid.dll")]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle hObject, out IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS caps);

    [DllImport("hid.dll")]
    private static extern int HidP_GetValueCaps(HIDP_REPORT_TYPE reportType, [Out] HIDP_VALUE_CAPS[] valueCaps, ref ushort valueCapsLength, IntPtr preparsedData);

    private const int HIDP_STATUS_SUCCESS = 0x00110000;

    private enum HIDP_REPORT_TYPE { Input = 0 }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDP_CAPS
    {
        public short Usage;
        public short UsagePage;
        public short InputReportByteLength;
        public short OutputReportByteLength;
        public short FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public short[] Reserved;
        public short NumberLinkCollectionNodes;
        public short NumberInputButtonCaps;
        public short NumberInputValueCaps;
        public short NumberInputDataIndices;
        public short NumberOutputButtonCaps;
        public short NumberOutputValueCaps;
        public short NumberOutputDataIndices;
        public short NumberFeatureButtonCaps;
        public short NumberFeatureValueCaps;
        public short NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDP_VALUE_CAPS
    {
        public short UsagePage;
        public byte ReportID;
        public byte IsAlias;
        public short BitField;
        public short LinkCollection;
        public short LinkUsage;
        public short LinkUsagePage;
        public byte IsRange;
        public byte IsStringRange;
        public byte IsDesignatorRange;
        public byte IsAbsolute;
        public int Reserved;
        public short UsageMin;
        public short UsageMax;
        public short StringMin;
        public short StringMax;
        public short DesignatorMin;
        public short DesignatorMax;
        public short DataIndexMin;
        public short DataIndexMax;
        public short LogicalMin;
        public short LogicalMax;
        public short PhysicalMin;
        public short PhysicalMax;
        public short Usage;
        public short Reserved1;
        public int UnitsExp;
        public uint Units;
        public short ReportSize;
        public short ReportCount;
        public short Reserved2;
        public short BitSize;
        public short Reserved3;
        public short ReportID2;
        public short Reserved4;
        public short Reserved5;
        public short Reserved6;
        public byte IsStringVar;
        public byte IsDesignatorVar;
        public byte IsAbsolute2;
        public byte Reserved7;
        public int Reserved8;
        public int Reserved9;
        public int Reserved10;
        public int Reserved11;

        public bool NotRange => IsRange == 0;
        public uint Unit => Units;
        public short UnitExp => (short)UnitsExp;
    }

    private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

    ////////////////// SENSITIVITY MAPPING //////////////////

    private enum CurveType { Linear, Exponential, Lut }

    private sealed class SensitivityMapper
    {
        private readonly float _sensitivity;
        private readonly bool _invertY;
        private readonly CurveType _curve;
        private readonly float _exponent;
        private readonly IReadOnlyList<float>? _lut;

        public SensitivityMapper(CurveType curve, float sensitivity = 1f, bool invertY = true,
            float exponent = 1.5f, IReadOnlyList<float>? lut = null)
        {
            _curve = curve;
            _sensitivity = sensitivity;
            _invertY = invertY;
            _exponent = exponent;
            _lut = lut;
        }

        public (short X, short Y) Map(int dx, int dy, int dpi)
        {
            float x = ApplyCurve(dx, dpi);
            float y = ApplyCurve(dy, dpi);
            if (_invertY) y = -y;
            return (Clamp(x), Clamp(y));
        }

        private float ApplyCurve(int delta, int dpi)
        {
            float normalized = delta * _sensitivity / dpi;
            float value = normalized;
            switch (_curve)
            {
                case CurveType.Linear:
                    value = normalized;
                    break;
                case CurveType.Exponential:
                    value = MathF.Sign(normalized) * MathF.Pow(MathF.Abs(normalized), _exponent);
                    break;
                case CurveType.Lut:
                    value = _lut == null || _lut.Count == 0 ? normalized : Lookup(normalized);
                    break;
            }
            return value * short.MaxValue;
        }

        private float Lookup(float v)
        {
            bool neg = v < 0f;
            float abs = MathF.Min(MathF.Abs(v), 1f);
            int maxIndex = _lut!.Count - 1;
            float scaled = abs * maxIndex;
            int idx = (int)scaled;
            if (idx >= maxIndex)
                return neg ? -_lut[maxIndex] : _lut[maxIndex];
            float t = scaled - idx;
            float a = _lut[idx];
            float b = _lut[idx + 1];
            float result = a + (b - a) * t;
            return neg ? -result : result;
        }

        private static short Clamp(float v)
        {
            if (v > short.MaxValue) return short.MaxValue;
            if (v < short.MinValue) return short.MinValue;
            return (short)v;
        }
    }

    /////////////////// ViGEm OUTPUT ///////////////////////

    private sealed class ViGEmXbox360Output : IDisposable
    {
        private readonly ViGEmClient _client;
        private readonly IXbox360Controller _controller;
        private readonly object _lock = new();

        public ViGEmXbox360Output()
        {
            _client = new ViGEmClient();
            _controller = _client.CreateXbox360Controller();
            _controller.Connect();
        }

        public void Send(short x, short y)
        {
            lock (_lock)
            {
                _controller.SetAxisValue(Xbox360Axis.RightThumbX, x);
                _controller.SetAxisValue(Xbox360Axis.RightThumbY, y);
                _controller.SubmitReport();
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _controller.Disconnect();
                _client.Dispose();
            }
        }
    }

    /////////////////// MAIN ///////////////////////////////

    private static ViGEmXbox360Output? _output;
    private static SensitivityMapper? _mapper;

    private static void Main()
    {
        Console.WriteLine("Initializing ViGEm...");
        _output = new ViGEmXbox360Output();
        _mapper = new SensitivityMapper(CurveType.Exponential, sensitivity: 1f, exponent: 1.3f);

        RegisterForRawInput();
        Console.WriteLine("Listening for raw mouse input. Press ESC to exit.");

        MSG msg;
        while (GetMessage(out msg, IntPtr.Zero, 0, 0))
        {
            if (msg.message == WM_INPUT)
            {
                ProcessRawInput(msg.lParam);
            }
            if (msg.message == WM_KEYDOWN && (int)msg.wParam == 0x1B)
            {
                break;
            }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        _output.Dispose();
    }

    private static void RegisterForRawInput()
    {
        var rid = new RAWINPUTDEVICE[1];
        rid[0].usUsagePage = 0x01;
        rid[0].usUsage = 0x02;
        rid[0].dwFlags = RIDEV_INPUTSINK;
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
        if (!_dpiCache.TryGetValue(device, out int dpi))
        {
            string message;
            dpi = DetectDpi(device, out message);
            _dpiCache[device] = dpi;
            Console.WriteLine($"Device 0x{device.ToInt64():X}: DPI {dpi} ({message})");
        }

        var (rx, ry) = _mapper!.Map(raw.mouse.lLastX, raw.mouse.lLastY, dpi);
        _output!.Send(rx, ry);
    }
}
