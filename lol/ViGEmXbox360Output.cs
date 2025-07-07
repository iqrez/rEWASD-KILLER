using System;
using System.Diagnostics;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

main
/// </summary>
public sealed class ViGEmXbox360Output : IDisposable
{
    private readonly ViGEmClient _client;
    private readonly IXbox360Controller _controller;
 main
    public ViGEmXbox360Output()
    {
        try
        {
            _client = new ViGEmClient();
            _controller = _client.CreateXbox360Controller();
main
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ViGEm initialization failed: {ex}");
            throw;
        }
    }

    /// <summary>
 main
        {
            try
            {
                _controller.SetAxisValue(Xbox360Axis.RightThumbX, x);
                _controller.SetAxisValue(Xbox360Axis.RightThumbY, y);
> main
                _controller.SubmitReport();
            }
            catch (Exception ex)
            {
 main
            }
        }
    }

    /// <summary>
 main
}
