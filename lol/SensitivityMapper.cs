// SensitivityCurve and mapping module for converting raw mouse delta into Xbox 360 right stick values.
// Target framework: latest .NET (net9.0 or later)
// No smoothing or filtering is done in order to keep latency low.
// The class can be integrated with a ViGEm output layer (see VirtualControllerManager).

namespace Lol;

/// <summary>
/// Supported response curves for converting mouse movement into stick values.
/// Linear applies a direct scale, Exponential multiplies by pow(abs(x), Exponent),
/// and Lut uses a lookup table for fully custom curves.
/// </summary>
public enum CurveType
{
    Linear,
    Exponential,
    Lut
}

/// <summary>
/// Maps raw mouse deltas to Xbox 360 controller stick ranges.
/// The mapping can be tuned with a sensitivity factor and a chosen curve type.
/// </summary>
public class SensitivityMapper
{
    private readonly float _sensitivity;
    private readonly bool _invertY;
    private readonly CurveType _curve;
    private readonly float _exponent;
    private readonly IReadOnlyList<float>? _lut;

    /// <summary>
    /// Creates a mapper.
    /// </summary>
    /// <param name="curve">Curve type to use.</param>
    /// <param name="sensitivity">Multiplier applied before the curve.</param>
    /// <param name="invertY">True to invert Y axis (standard for controllers).</param>
    /// <param name="exponent">Exponent for the exponential curve.</param>
    /// <param name="lut">Optional lookup table entries in range 0..1.</param>
    public SensitivityMapper(
        CurveType curve,
        float sensitivity = 1f,
        bool invertY = true,
        float exponent = 1.5f,
        IReadOnlyList<float>? lut = null)
    {
        _curve = curve;
        _sensitivity = sensitivity;
        _invertY = invertY;
        _exponent = exponent;
        _lut = lut;
    }

    /// <summary>
    /// Converts raw mouse delta into stick values in the range -32768..32767.
    /// </summary>
    /// <param name="dx">Mouse delta X.</param>
    /// <param name="dy">Mouse delta Y.</param>
    /// <param name="dpi">Mouse DPI used for scaling.</param>
    /// <returns>Tuple with X and Y values ready for ViGEm.</returns>
    public (short X, short Y) Map(int dx, int dy, int dpi)
    {
        float x = ApplyCurve(dx, dpi);
        float y = ApplyCurve(dy, dpi);
        if (_invertY) y = -y;

        return (Clamp(x), Clamp(y));
    }

    private float ApplyCurve(int delta, int dpi)
    {
        // Normalize delta by DPI then apply sensitivity.
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
                if (_lut == null || _lut.Count == 0)
                {
                    value = normalized;
                }
                else
                {
                    value = Lookup(normalized);
                }
                break;
        }

        // scale to stick range
        return value * short.MaxValue;
    }

    private float Lookup(float v)
    {
        // Lookup table works on absolute value 0..1, symmetrical for negative inputs.
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

/*
Usage examples
---------------

// Linear curve at half sensitivity:
var linear = new SensitivityMapper(CurveType.Linear, sensitivity: 0.5f);

// Exponential curve for faster turn speed on big motions:
var expo = new SensitivityMapper(CurveType.Exponential, sensitivity: 1f, exponent: 2f);

// LUT curve where input 0..1 maps to custom output 0..1 (symmetric around zero):
var lutValues = new float[] { 0f, 0.1f, 0.3f, 0.6f, 1f };
var lut = new SensitivityMapper(CurveType.Lut, sensitivity: 1f, lut: lutValues);

// Converting deltas (dx, dy) from RawInput:
(short rx, short ry) = linear.Map(dx, dy, dpi: 800);
// Pass rx and ry into a ViGEm virtual controller.
*/
