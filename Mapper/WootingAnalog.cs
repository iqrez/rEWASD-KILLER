using System;
using System.Runtime.InteropServices;

namespace Mapper;

/// <summary>
/// Provides P/Invoke declarations for the Wooting Analog SDK.
/// Only the minimal functions required for polling analog values are defined.
/// The native DLL (wooting_analog_wrapper.dll) must reside in the application directory.
/// </summary>
public static class WootingAnalog
{
    /// <summary>
    /// Initializes communication with the Wooting keyboard.
    /// </summary>
    [DllImport("wooting_analog_wrapper.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool wooting_analog_initialise();

    /// <summary>
    /// Returns the analog value for the provided keycode.
    /// Keycodes are based on the HID usage ID.
    /// </summary>
    [DllImport("wooting_analog_wrapper.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern float wooting_analog_read_full(int keycode);
}
