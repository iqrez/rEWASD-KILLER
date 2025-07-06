using System;
using System.Windows.Forms;

namespace Mapper;

public class MainForm : Form
{
    private readonly TextBox _logBox;
    private readonly System.Windows.Forms.Timer _analogTimer;
    private RawInputHandler? _rawHandler;
    private readonly VirtualControllerManager _controller = new();

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
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _rawHandler = new RawInputHandler(Handle);
        _rawHandler.OnMouseDelta += (dx, dy) =>
        {
            Log($"Mouse delta: {dx},{dy}");
            _controller.HandleMouseDelta(dx, dy);
        };
        _rawHandler.OnMouseButton += (btn, pressed) =>
        {
            Log($"Mouse {btn} {(pressed ? "down" : "up")}");
            _controller.HandleMouseButton(btn, pressed);
        };
        _rawHandler.OnKeyEvent += (key, pressed) =>
        {
            Log($"Key {key} {(pressed ? "down" : "up")}");
            _controller.HandleKeyEvent(key, pressed);
        };
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

    protected override void WndProc(ref Message m)
    {
        _rawHandler?.ProcessMessage(m);
        base.WndProc(ref m);
    }

    private void Log(string message)
    {
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
