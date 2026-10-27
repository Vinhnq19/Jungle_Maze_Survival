#nullable enable
using System;
using System.Globalization;
using System.Text;

namespace JungleMaze.Telemetry.Server;

/// <summary>
/// Viết một dòng JSON bằng tay vào StringBuilder dùng lại: khóa theo thứ tự gọi, số InvariantCulture,
/// số thực tối đa 3 chữ số thập phân, NaN/Infinity thành null. Chỉ dùng ở luồng nền.
/// </summary>
public sealed class TelemetryJson
{
    private readonly StringBuilder _sb = new(512);
    private bool _first = true;
    private int _depth;

    public int Length => _sb.Length;

    public TelemetryJson Begin()
    {
        _sb.Clear();
        _sb.Append('{');
        _first = true;
        _depth = 1;
        return this;
    }

    public string End()
    {
        CloseObject();
        return _sb.ToString();
    }

    private void CloseObject()
    {
        while (_depth > 0)
        {
            _sb.Append('}');
            _depth--;
        }
    }

    private void Key(string key)
    {
        if (!_first) _sb.Append(',');
        _first = false;
        _sb.Append('"').Append(key).Append("\":");
    }

    public TelemetryJson Str(string key, string? value)
    {
        Key(key);
        AppendString(value);
        return this;
    }

    public TelemetryJson Int(string key, long value)
    {
        Key(key);
        _sb.Append(value.ToString(CultureInfo.InvariantCulture));
        return this;
    }

    public TelemetryJson UInt(string key, uint value)
    {
        Key(key);
        _sb.Append(value.ToString(CultureInfo.InvariantCulture));
        return this;
    }

    /// <summary>Giá trị âm nghĩa là null (quy ước -1 của factory).</summary>
    public TelemetryJson IntOrNull(string key, long value)
    {
        Key(key);
        if (value < 0) _sb.Append("null");
        else _sb.Append(value.ToString(CultureInfo.InvariantCulture));
        return this;
    }

    /// <summary>netId: 255 nghĩa là null.</summary>
    public TelemetryJson NetIdOrNull(string key, byte value)
    {
        Key(key);
        if (value == TelemetryEvent.NoNetId) _sb.Append("null");
        else _sb.Append(value.ToString(CultureInfo.InvariantCulture));
        return this;
    }

    public TelemetryJson UIntOrNull(string key, uint? value)
    {
        Key(key);
        if (value is { } v) _sb.Append(v.ToString(CultureInfo.InvariantCulture));
        else _sb.Append("null");
        return this;
    }

    public TelemetryJson Num(string key, double value)
    {
        Key(key);
        AppendNumber(value);
        return this;
    }

    public TelemetryJson Bool(string key, bool value)
    {
        Key(key);
        _sb.Append(value ? "true" : "false");
        return this;
    }

    public TelemetryJson BoolOrNull(string key, bool? value)
    {
        Key(key);
        _sb.Append(value is null ? "null" : value.Value ? "true" : "false");
        return this;
    }

    public TelemetryJson Null(string key)
    {
        Key(key);
        _sb.Append("null");
        return this;
    }

    public TelemetryJson Guid(string key, Guid value)
    {
        Key(key);
        _sb.Append('"').Append(value.ToString("D")).Append('"');
        return this;
    }

    /// <summary>Mở mảng; gọi các Item… rồi EndArray.</summary>
    public TelemetryJson BeginArray(string key)
    {
        Key(key);
        _sb.Append('[');
        _first = true;
        return this;
    }

    public TelemetryJson ItemString(string value)
    {
        if (!_first) _sb.Append(',');
        _first = false;
        AppendString(value);
        return this;
    }

    public TelemetryJson ItemObject(string k1, string v1, string k2, long v2)
    {
        if (!_first) _sb.Append(',');
        _first = false;
        _sb.Append("{\"").Append(k1).Append("\":");
        AppendString(v1);
        _sb.Append(",\"").Append(k2).Append("\":").Append(v2.ToString(CultureInfo.InvariantCulture)).Append('}');
        return this;
    }

    public TelemetryJson EndArray()
    {
        _sb.Append(']');
        _first = false;
        return this;
    }

    private void AppendNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            _sb.Append("null");
            return;
        }
        var rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);
        if (rounded == 0) rounded = 0; // bỏ -0
        _sb.Append(rounded.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private void AppendString(string? value)
    {
        if (value is null)
        {
            _sb.Append("null");
            return;
        }
        _sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': _sb.Append("\\\""); break;
                case '\\': _sb.Append("\\\\"); break;
                case '\n': _sb.Append("\\n"); break;
                case '\r': _sb.Append("\\r"); break;
                case '\t': _sb.Append("\\t"); break;
                default:
                    if (c < 0x20) _sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else _sb.Append(c);
                    break;
            }
        }
        _sb.Append('"');
    }
}
