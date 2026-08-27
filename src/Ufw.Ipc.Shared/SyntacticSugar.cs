using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Ufw.Ipc.Shared;

public static class SyntacticSugar
{
    public static object? __ => null;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static object? Do(Action action)
    {
        Debug.Assert(action != null);
        action();
        return __;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static object? Do<T>(Action<T> action, T arg)
    {
        Debug.Assert(action != null);
        action(arg);
        return __;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static object? Do<T1, T2>(Action<T1, T2> action, T1 arg1, T2 arg2)
    {
        Debug.Assert(action != null);
        action(arg1, arg2);
        return __;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static object? Do<T1, T2, T3>(Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3)
    {
        Debug.Assert(action != null);
        action(arg1, arg2, arg3);
        return __;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static object? Do<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        Debug.Assert(action != null);
        action(arg1, arg2, arg3, arg4);
        return __;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pass()
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pass<T>(T _) => Pass();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pass<T1, T2>(T1 _1, T2 _2) => Pass();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pass<T1, T2, T3>(T1 _1, T2 _2, T3 _3) => Pass();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pass<T1, T2, T3, T4>(T1 _1, T2 _2, T3 _3, T4 _4) => Pass();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: MaybeNull]
    public static T? NullableOf<T>(T? value) => value;
}
