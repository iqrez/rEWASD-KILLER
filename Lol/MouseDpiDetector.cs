using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Lol
{
    /// <summary>
    /// Utility class that attempts to determine the hardware DPI of a mouse using
    /// information from the Raw Input subsystem and HID APIs. If the DPI cannot be
    /// obtained the method falls back to a default value.
    /// </summary>
    public static class MouseDpiDetector
    {
        /// <summary>
        /// Tries to read the hardware DPI for the mouse associated with the given
        /// raw input device handle.
        /// </summary>
        /// <param name="deviceHandle">Handle from Raw Input (RAWINPUTHEADER.hDevice).</param>
        /// <param name="message">Warning or informational message.</param>
        /// <returns>The detected DPI or a default of 800.</returns>
        public static int DetectDpi(IntPtr deviceHandle, out string message)
        {
            message = string.Empty;
            string? devicePath = GetDevicePath(deviceHandle);
            if (string.IsNullOrEmpty(devicePath))
            {
                message = "Unable to resolve HID device path from raw handle. Falling back to default DPI.";
                return DefaultDpi;
            }

            // Open the HID device for feature queries.
            using SafeFileHandle handle = CreateFile(devicePath, FileAccess.ReadWrite, FileShare.ReadWrite,
                IntPtr.Zero, FileMode.Open, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                message = "Failed to open HID device. Falling back to default DPI.";
                return DefaultDpi;
            }

            if (!HidD_GetPreparsedData(handle, out IntPtr preparsed))
            {
                message = "Could not obtain HID preparsed data. Falling back to default DPI.";
                return DefaultDpi;
            }

            try
            {
                // HID descriptors rarely include DPI information; attempt to parse if present.
                if (HidP_GetCaps(preparsed, out HIDP_CAPS caps) != HIDP_STATUS_SUCCESS)
                {
                    message = "Could not read HID capabilities. Falling back to default DPI.";
                    return DefaultDpi;
                }

                ushort capCount = caps.NumberInputValueCaps;
                HIDP_VALUE_CAPS[] valueCaps = new HIDP_VALUE_CAPS[capCount];
                if (HidP_GetValueCaps(HIDP_REPORT_TYPE.Input, valueCaps, ref capCount, preparsed) != HIDP_STATUS_SUCCESS)
                {
                    message = "Could not read HID value caps. Falling back to default DPI.";
                    return DefaultDpi;
                }

                // Look for the X axis usage and inspect its unit properties.
                var xCap = valueCaps.FirstOrDefault(c => c.UsagePage == 0x01 && c.NotRange && c.Usage == 0x30);
                if (xCap.Unit == 0)
                {
                    // Most mice leave Unit==0 meaning the resolution is unspecified.
                    message = "HID descriptor does not expose DPI. Falling back to default DPI.";
                    return DefaultDpi;
                }

                double unitsPerInch = HidUnitsToInches(xCap.Unit, xCap.UnitExp);
                if (unitsPerInch <= 0)
                {
                    message = "Unsupported HID unit for DPI. Falling back to default DPI.";
                    return DefaultDpi;
                }

                // DPI = counts per inch. Logical range represents counts.
                int counts = xCap.LogicalMax - xCap.LogicalMin + 1;
                int dpi = (int)(counts / unitsPerInch);
                message = "DPI derived from HID descriptor.";
                return dpi;
            }
            finally
            {
                HidD_FreePreparsedData(preparsed);
            }
        }

        /// <summary>
        /// Attempts to convert HID units to inches using the HID unit code and exponent.
        /// </summary>
        private static double HidUnitsToInches(uint unit, short unitExp)
        {
            // HID spec defines resolution units. For mice this is rarely populated.
            const uint HID_UNIT_ENGLISH_LINEAR = 0x01; // inches, feet, etc.

            if ((unit & 0x0F) != HID_UNIT_ENGLISH_LINEAR)
                return -1; // Unsupported or not specified.

            // Typical mice use inches with exponent -2 meaning counts per 1/100 inch.
            double unitExponent = Math.Pow(10, unitExp);
            double unitsPerInch = 1.0 * unitExponent; // 1 unit == 1 * 10^exp inches
            if (unitsPerInch <= 0)
                return -1;
            return unitsPerInch;
        }

        private const int DefaultDpi = 800;

        #region Raw Input helpers
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
        #endregion

        #region HID interop
        private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

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

        private enum HIDP_REPORT_TYPE
        {
            Input = 0,
        }

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
        #endregion
    }
}
