using System.Diagnostics.CodeAnalysis;

namespace Ufw.Systemd.Interop.IO;

internal sealed class Out<T>
{
    public T? Value { get; private set; }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasValue { get; private set; }

    public bool TryGetValue([NotNullWhen(true)] out T? value)
    {
        value = Value;
        return HasValue;
    }

    public void SetValue(T value)
    {
        Value = value;
        HasValue = true;
    }

    public static implicit operator T?(Out<T> outValue)
    {
        ArgumentNullException.ThrowIfNull(outValue);
        return outValue.Value;
    }
}
