using System.Numerics;
using System.Runtime.CompilerServices;

namespace SiJenggo.Core;

/// <summary>
/// xoshiro128** generator. The simulation uses only this (never <see cref="Random.Shared"/>)
/// so two machines fed the same seed and inputs stay in lockstep for online co-op.
/// It is a mutable struct: keep it in a field and pass it by ref.
/// </summary>
public struct DeterministicRandom
{
    private uint _s0, _s1, _s2, _s3;

    public DeterministicRandom(ulong seed)
    {
        // SplitMix64 to spread the seed over the state.
        ulong z = seed;
        _s0 = (uint)Next(ref z);
        _s1 = (uint)Next(ref z);
        _s2 = (uint)Next(ref z);
        _s3 = (uint)Next(ref z);
        if ((_s0 | _s1 | _s2 | _s3) == 0)
        {
            _s0 = 1;
        }

        static ulong Next(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong r = x;
            r = (r ^ (r >> 30)) * 0xBF58476D1CE4E5B9UL;
            r = (r ^ (r >> 27)) * 0x94D049BB133111EBUL;
            return r ^ (r >> 31);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint NextUInt()
    {
        uint result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        uint t = _s1 << 9;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 11);
        return result;
    }

    /// <summary>Uniform float in [0, 1).</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

    public float Range(float min, float max) => min + (max - min) * NextFloat();

    /// <summary>Uniform int in [min, max).</summary>
    public int Range(int min, int max) => max <= min ? min : min + (int)(NextUInt() % (uint)(max - min));

    public bool Chance(float probability) => NextFloat() < probability;

    public readonly uint StateHash => _s0 ^ (_s1 * 31) ^ (_s2 * 131) ^ (_s3 * 1313);
}
