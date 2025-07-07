using System;
using System.Diagnostics;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace Lol;

/// <summary>
/// Manages a virtual Xbox 360 controller via ViGEm. On creation the controller
/// is allocated, connected and ready for high frequency updates.
/// </summary>
public sealed class ViGEmXbox360Output : IDisposable
{
    private readonly ViGEmClient _client;
    private readonly IXbox360Controller _controller;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new virtual Xbox 360 controller and connects it immediately.
    /// </summary>
    /// <exception cref="Exception">Throws if the controller could not be created or connected.</exception>
    public ViGEmXbox360Output()
    {
        try
        {
            _client = new ViGEmClient();
            _controller = _client.CreateXbox360Controller();

            // Manual submission yields lower overhead when frequently updating multiple fields.
            _controller.AutoSubmitReport = false;
            _controller.Connect();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ViGEm initialization failed: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Updates the right stick axes and submits the report.
    /// </summary>
    /// <param name="x">X axis value (-32768..32767).</param>
    /// <param name="y">Y axis value (-32768..32767).</param>
    public void SetRightStick(short x, short y)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ViGEmXbox360Output));

        lock (_lock)
        {
            try
            {
                _controller.SetAxisValue(Xbox360Axis.RightThumbX, x);
                _controller.SetAxisValue(Xbox360Axis.RightThumbY, y);
                _controller.SubmitReport();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update right stick: {ex}");
            }
        }
    }

    /// <summary>
    /// Disconnects and frees the virtual controller.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        lock (_lock)
        {
            try
            {
                _controller.Disconnect();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during controller disconnect: {ex}");
            }
            finally
            {
                _controller.Dispose();
                _client.Dispose();
                _disposed = true;
            }
        }
    }
}
