using System;
using System.Diagnostics;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace ViGEmOutput;

/// <summary>
/// Manages a virtual Xbox 360 controller using ViGEm. Designed for
/// high-frequency updates from input threads.
/// </summary>
public sealed class ViGEmXbox360Output : IDisposable
{
    private readonly ViGEmClient _client;
    private readonly IXbox360Controller _controller;
    private readonly object _sync = new();
    private bool _connected;
    private bool _disposed;

    /// <summary>
    /// Initializes the ViGEm client and connects a virtual controller.
    /// Throws if the ViGEm bus is unavailable.
    /// </summary>
    public ViGEmXbox360Output()
    {
        try
        {
            _client = new ViGEmClient();
            _controller = _client.CreateXbox360Controller();
            // Disable auto-submit to batch updates and reduce syscalls.
            _controller.AutoSubmitReport = false;
            _controller.Connect();
            _connected = true;
            Debug.WriteLine("ViGEm Xbox 360 controller connected.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ViGEm initialization failed: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Update the right stick axes in standard range (-32768..32767).
    /// Safe to call from multiple threads.
    /// </summary>
    public void SetRightStick(short x, short y)
    {
        if (!_connected || _disposed)
            return;

        lock (_sync)
        {
            try
            {
                _controller.SetAxisValue(Xbox360Axis.RightThumbX, x);
                _controller.SetAxisValue(Xbox360Axis.RightThumbY, y);
                // Submit once per update for lowest latency.
                _controller.SubmitReport();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to send right stick update: {ex}");
                _connected = false;
            }
        }
    }

    /// <summary>
    /// Disconnects the virtual controller.
    /// </summary>
    public void Disconnect()
    {
        lock (_sync)
        {
            if (!_connected)
                return;
            try
            {
                _controller.Disconnect();
                Debug.WriteLine("ViGEm controller disconnected.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while disconnecting: {ex}");
            }
            finally
            {
                _connected = false;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        Disconnect();
        _controller.Dispose();
        _client.Dispose();
        _disposed = true;
    }
}
