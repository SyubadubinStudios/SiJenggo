using System.Numerics;
using System.Runtime.CompilerServices;

namespace SiJenggo.Core;

public static class MathX
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MoveTowards(float current, float target, float maxDelta)
    {
        float d = target - current;
        return MathF.Abs(d) <= maxDelta ? target : current + MathF.Sign(d) * maxDelta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Saturate(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

    /// <summary>Frame-rate independent exponential smoothing factor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Damp(float sharpness, float dt) => 1f - MathF.Exp(-sharpness * dt);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SmoothStep(float t)
    {
        t = Saturate(t);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Distance on the ground plane (X/Z), ignoring height.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GroundDistance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Order-dependent hash mix for desync checksums.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Mix(uint hash, float value) => (hash ^ BitConverter.SingleToUInt32Bits(value)) * 16777619u;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Mix(uint hash, int value) => (hash ^ (uint)value) * 16777619u;
}
