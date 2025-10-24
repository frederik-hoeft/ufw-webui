using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Ufw.Pipes.Shared.Model;

public readonly struct RequestMethod : IEquatable<RequestMethod>, IEqualityOperators<RequestMethod, RequestMethod, bool>
{
    private readonly Value _value;
    private static readonly FrozenDictionary<RequestMethod, string> s_forwardBindings;
    private static readonly FrozenDictionary<string, RequestMethod> s_reverseBindings;

    static RequestMethod()
    {
        KeyValuePair<RequestMethod, string>[] source =
        [
            new(Get, "GET"),
            new(Post, "POST"),
            new(Put, "PUT"),
            new(Delete, "DELETE")
        ];
        s_forwardBindings = source.ToFrozenDictionary(binding => binding.Key, binding => binding.Value);
        s_reverseBindings = source.ToFrozenDictionary(binding => binding.Value, binding => binding.Key);
    }
    private RequestMethod(Value value) => _value = value;

    public static RequestMethod Get => new(Value.Get);

    public static RequestMethod Post => new(Value.Post);

    public static RequestMethod Put => new(Value.Put);

    public static RequestMethod Delete => new(Value.Delete);

    public static bool operator ==(RequestMethod left, RequestMethod right) => left._value == right._value;

    public static bool operator !=(RequestMethod left, RequestMethod right) => left._value != right._value;

    public bool Equals(RequestMethod other) => _value == other._value;

    public override int GetHashCode() => _value.GetHashCode();

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is RequestMethod other && Equals(other);

    public override string ToString() => Enum.IsDefined(_value) ? s_forwardBindings[this] : "UNKNOWN";

    public static bool TryParse(string value, out RequestMethod method) => s_reverseBindings.TryGetValue(value, out method);

    public static bool IsDefined(RequestMethod method) => Enum.IsDefined(method._value);

    public static bool IsDefined(string method) => s_reverseBindings.ContainsKey(method);

    public static ImmutableArray<RequestMethod> GetValues() => s_forwardBindings.Keys;

    public static ImmutableArray<string> GetNames() => s_forwardBindings.Values;

    private enum Value
    {
        Get = 1,
        Post = 2,
        Put = 3,
        Delete = 4,
    }
}
