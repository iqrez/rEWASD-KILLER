using System.Diagnostics;
using System.Windows.Forms;

namespace Mapper;

/// <summary>
/// Simple placeholder that would map inputs to a virtual controller using ViGEm.
/// Currently just writes debug output.
/// </summary>
public class VirtualControllerManager
{
    public void HandleMouseDelta(int dx, int dy)
    {
        Debug.WriteLine($"Mouse delta {dx},{dy}");
    }

    public void HandleMouseButton(MouseButton button, bool pressed)
    {
        Debug.WriteLine($"Mouse {button} {(pressed ? "down" : "up")}");
    }

    public void HandleKeyEvent(Keys key, bool pressed)
    {
        Debug.WriteLine($"Key {key} {(pressed ? "down" : "up")}");
    }
}

