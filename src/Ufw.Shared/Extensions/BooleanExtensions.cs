using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Ufw.Shared.Extensions;

[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "C# 14 extension methods false positive")]
public static class BooleanExtensions
{
    extension(bool self)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WithDefaults<T1>([MaybeNull] out T1? value)
            where T1 : allows ref struct
        {
            value = default;
            return self;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WithDefaults<T1, T2>([MaybeNull] out T1? value1, [MaybeNull] out T2? value2)
            where T1 : allows ref struct
            where T2 : allows ref struct
        {
            value1 = default; value2 = default;
            return self;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WithDefaults<T1, T2, T3>([MaybeNull] out T1? value1, [MaybeNull] out T2? value2, [MaybeNull] out T3? value3)
            where T1 : allows ref struct
            where T2 : allows ref struct
            where T3 : allows ref struct
        {
            value1 = default; value2 = default; value3 = default;
            return self;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WithDefaults<T1, T2, T3, T4>([MaybeNull] out T1? value1, [MaybeNull] out T2? value2, [MaybeNull] out T3? value3, [MaybeNull] out T4? value4)
            where T1 : allows ref struct
            where T2 : allows ref struct
            where T3 : allows ref struct
            where T4 : allows ref struct
        {
            value1 = default; value2 = default; value3 = default; value4 = default;
            return self;
        }
    }
}
