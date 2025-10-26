using System.Runtime.CompilerServices;

namespace Ufw.Ipc.Shared.Threading;

/// <summary>
/// Provides extension methods for performing bitwise operations on generic enum types.
/// </summary>
public static class EnumHelpers
{
    /// <summary>
    /// Performs a bitwise AND operation between two enum values of the same type.
    /// </summary>
    /// <param name="self">The first operand.</param>
    /// <param name="other">The second operand.</param>
    /// <returns><c>self & other</c></returns>
    public static TEnum And<TEnum>(this TEnum self, TEnum other) where TEnum : unmanaged, Enum => Unsafe.SizeOf<TEnum>() switch
    {
        1 => Unsafe.BitCast<byte, TEnum>((byte)(Unsafe.BitCast<TEnum, byte>(self) & Unsafe.BitCast<TEnum, byte>(other))),
        2 => Unsafe.BitCast<ushort, TEnum>((ushort)(Unsafe.BitCast<TEnum, ushort>(self) & Unsafe.BitCast<TEnum, ushort>(other))),
        4 => Unsafe.BitCast<uint, TEnum>(Unsafe.BitCast<TEnum, uint>(self) & Unsafe.BitCast<TEnum, uint>(other)),
        8 => Unsafe.BitCast<ulong, TEnum>(Unsafe.BitCast<TEnum, ulong>(self) & Unsafe.BitCast<TEnum, ulong>(other)),
        _ => throw UnsupportedEnumSize<TEnum>()
    };

    public static TEnum Or<TEnum>(this TEnum self, TEnum other) where TEnum : unmanaged, Enum => Unsafe.SizeOf<TEnum>() switch
    {
        1 => Unsafe.BitCast<byte, TEnum>((byte)(Unsafe.BitCast<TEnum, byte>(self) | Unsafe.BitCast<TEnum, byte>(other))),
        2 => Unsafe.BitCast<ushort, TEnum>((ushort)(Unsafe.BitCast<TEnum, ushort>(self) | Unsafe.BitCast<TEnum, ushort>(other))),
        4 => Unsafe.BitCast<uint, TEnum>(Unsafe.BitCast<TEnum, uint>(self) | Unsafe.BitCast<TEnum, uint>(other)),
        8 => Unsafe.BitCast<ulong, TEnum>(Unsafe.BitCast<TEnum, ulong>(self) | Unsafe.BitCast<TEnum, ulong>(other)),
        _ => throw UnsupportedEnumSize<TEnum>()
    };

    public static TEnum Xor<TEnum>(this TEnum self, TEnum other) where TEnum : unmanaged, Enum => Unsafe.SizeOf<TEnum>() switch
    {
        1 => Unsafe.BitCast<byte, TEnum>((byte)(Unsafe.BitCast<TEnum, byte>(self) ^ Unsafe.BitCast<TEnum, byte>(other))),
        2 => Unsafe.BitCast<ushort, TEnum>((ushort)(Unsafe.BitCast<TEnum, ushort>(self) ^ Unsafe.BitCast<TEnum, ushort>(other))),
        4 => Unsafe.BitCast<uint, TEnum>(Unsafe.BitCast<TEnum, uint>(self) ^ Unsafe.BitCast<TEnum, uint>(other)),
        8 => Unsafe.BitCast<ulong, TEnum>(Unsafe.BitCast<TEnum, ulong>(self) ^ Unsafe.BitCast<TEnum, ulong>(other)),
        _ => throw UnsupportedEnumSize<TEnum>()
    };

    public static TEnum Not<TEnum>(this TEnum self) where TEnum : unmanaged, Enum => Unsafe.SizeOf<TEnum>() switch
    {
        1 => Unsafe.BitCast<byte, TEnum>((byte)~Unsafe.BitCast<TEnum, byte>(self)),
        2 => Unsafe.BitCast<ushort, TEnum>((ushort)~Unsafe.BitCast<TEnum, ushort>(self)),
        4 => Unsafe.BitCast<uint, TEnum>(~Unsafe.BitCast<TEnum, uint>(self)),
        8 => Unsafe.BitCast<ulong, TEnum>(~Unsafe.BitCast<TEnum, ulong>(self)),
        _ => throw UnsupportedEnumSize<TEnum>()
    };

    public static bool FastEquals<TEnum>(this TEnum self, TEnum other) where TEnum : unmanaged, Enum => Unsafe.SizeOf<TEnum>() switch
    {
        1 => Unsafe.BitCast<TEnum, byte>(self) == Unsafe.BitCast<TEnum, byte>(other),
        2 => Unsafe.BitCast<TEnum, ushort>(self) == Unsafe.BitCast<TEnum, ushort>(other),
        4 => Unsafe.BitCast<TEnum, uint>(self) == Unsafe.BitCast<TEnum, uint>(other),
        8 => Unsafe.BitCast<TEnum, ulong>(self) == Unsafe.BitCast<TEnum, ulong>(other),
        _ => throw UnsupportedEnumSize<TEnum>()
    };

    internal static InvalidOperationException UnsupportedEnumSize<TEnum>() where TEnum : unmanaged, Enum =>
        new($"Unsupported enum size: {Unsafe.SizeOf<TEnum>()} bytes. Must be 1, 2, 4 or 8 bytes.");
}