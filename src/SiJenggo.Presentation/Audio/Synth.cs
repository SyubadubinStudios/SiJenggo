using SiJenggo.Core;
using SiJenggo.Game.Data;

namespace SiJenggo.Presentation.Audio;

public enum Sfx
{
    Punch,
    Kick,
    Heavy,
    Slash,
    Blunt,
    Fire,
    Lightning,
    Wind,
    Shockwave,
    Gadget,
    Whiff,
    Jump,
    Land,
    Pickup,
    RageOn,
    Ultimate,
    KnockOut,
    Counter,
    Break,
    UiMove,
    UiConfirm,
    UiBack,
    BossRoar,
    Go,
    Coin,
    Thunder,
}

/// <summary>
/// Every sound in the game is synthesised at startup: no audio files ship with the build, and the
/// music adapts because it is generated in stems (drums+bass, rhythm, lead) that are mixed live.
/// </summary>
public static class Synth
{
    public const int Rate = 44100;

    private static float Noise(ref DeterministicRandom rng) => rng.NextFloat() * 2f - 1f;

    private static float Env(float t, float attack, float decay) => t < attack ? t / attack : MathF.Exp(-(t - attack) / decay);

    /// <summary>Mono effect sample buffer.</summary>
    public static float[] Effect(Sfx sfx)
    {
        var rng = new DeterministicRandom((ulong)sfx + 17);
        float duration = sfx switch
        {
            Sfx.BossRoar => 1.6f,
            Sfx.Ultimate or Sfx.RageOn => 1.2f,
            Sfx.Thunder => 2.2f,
            Sfx.Lightning or Sfx.Shockwave or Sfx.Fire => 0.7f,
            Sfx.Go or Sfx.KnockOut => 0.6f,
            _ => 0.35f,
        };
        int n = (int)(duration * Rate);
        var buf = new float[n];
        float lp = 0, phase = 0, phase2 = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = Noise(ref rng);
            float v;
            switch (sfx)
            {
                case Sfx.Punch or Sfx.Kick or Sfx.Blunt:
                {
                    float f = MathX.Lerp(sfx == Sfx.Kick ? 140f : 180f, 55f, MathF.Min(1, t * 12));
                    phase += f / Rate;
                    lp += (noise - lp) * 0.25f;
                    v = MathF.Sin(phase * MathF.Tau) * Env(t, 0.002f, 0.06f) * 0.9f + lp * Env(t, 0.001f, 0.03f) * (sfx == Sfx.Blunt ? 1.2f : 0.7f);
                    break;
                }

                case Sfx.Heavy:
                {
                    float f = MathX.Lerp(120f, 38f, MathF.Min(1, t * 6));
                    phase += f / Rate;
                    lp += (noise - lp) * 0.15f;
                    v = MathF.Tanh(MathF.Sin(phase * MathF.Tau) * 2.5f) * Env(t, 0.003f, 0.14f) + lp * Env(t, 0.001f, 0.06f);
                    break;
                }

                case Sfx.Slash:
                {
                    float cutoff = MathX.Lerp(0.9f, 0.2f, t / duration);
                    lp += (noise - lp) * cutoff;
                    v = (noise - lp) * Env(t, 0.004f, 0.06f) * 0.9f;
                    phase += 2400f / Rate;
                    v += MathF.Sin(phase * MathF.Tau) * Env(t, 0.001f, 0.02f) * 0.3f;
                    break;
                }

                case Sfx.Whiff or Sfx.Wind:
                {
                    float sweep = MathX.Lerp(0.05f, 0.4f, MathF.Sin(MathF.PI * t / duration));
                    lp += (noise - lp) * sweep;
                    v = lp * MathF.Sin(MathF.PI * t / duration) * (sfx == Sfx.Wind ? 1.2f : 0.6f);
                    break;
                }

                case Sfx.Fire:
                    lp += (noise - lp) * 0.08f;
                    v = (lp * 2f + (rng.Chance(0.002f) ? 0.8f : 0f)) * Env(t, 0.05f, 0.3f);
                    break;
                case Sfx.Lightning or Sfx.Gadget:
                {
                    phase += (sfx == Sfx.Gadget ? 900f : 220f) * (1 + 0.5f * MathF.Sin(t * 90f)) / Rate;
                    float buzz = MathF.Sign(MathF.Sin(phase * MathF.Tau));
                    v = (buzz * 0.4f + noise * 0.6f) * Env(t, 0.001f, sfx == Sfx.Gadget ? 0.08f : 0.18f);
                    break;
                }

                case Sfx.Thunder:
                    lp += (noise - lp) * 0.03f;
                    v = lp * 4f * Env(t, 0.02f, 0.7f) * (1 + 0.5f * MathF.Sin(t * 13f));
                    break;
                case Sfx.Shockwave:
                {
                    float f = MathX.Lerp(90f, 30f, t / duration);
                    phase += f / Rate;
                    lp += (noise - lp) * 0.06f;
                    v = MathF.Sin(phase * MathF.Tau) * Env(t, 0.005f, 0.25f) + lp * 2f * Env(t, 0.002f, 0.2f);
                    break;
                }

                case Sfx.Jump:
                    phase += MathX.Lerp(300f, 600f, t / duration) / Rate;
                    v = MathF.Sin(phase * MathF.Tau) * Env(t, 0.005f, 0.06f) * 0.3f;
                    break;
                case Sfx.Land:
                    lp += (noise - lp) * 0.1f;
                    v = lp * 1.5f * Env(t, 0.002f, 0.05f);
                    break;
                case Sfx.Pickup or Sfx.Coin:
                {
                    float f = t < 0.08f ? 988f : 1319f;
                    phase += f / Rate;
                    v = (MathF.Sin(phase * MathF.Tau) + 0.3f * MathF.Sin(phase * 2 * MathF.Tau)) * Env(t, 0.002f, 0.12f) * 0.35f;
                    break;
                }

                case Sfx.RageOn or Sfx.Ultimate:
                {
                    float f = MathX.Lerp(60f, sfx == Sfx.Ultimate ? 240f : 160f, MathF.Min(1, t * 1.5f));
                    phase += f / Rate;
                    phase2 += f * 1.5f / Rate;
                    float saw = (phase % 1f) * 2f - 1f + (phase2 % 1f) - 0.5f;
                    lp += (noise - lp) * 0.1f;
                    v = MathF.Tanh(saw * 2f) * 0.5f * Env(t, 0.15f, 0.5f) + lp * Env(t, 0.05f, 0.4f);
                    break;
                }

                case Sfx.KnockOut:
                {
                    phase += 520f / Rate;
                    phase2 += 1310f / Rate;
                    v = (MathF.Sin(phase * MathF.Tau) + 0.5f * MathF.Sin(phase2 * MathF.Tau)) * Env(t, 0.002f, 0.25f) * 0.35f;
                    break;
                }

                case Sfx.Counter:
                    phase += 1760f / Rate;
                    v = MathF.Sin(phase * MathF.Tau) * Env(t, 0.001f, 0.1f) * 0.5f + noise * Env(t, 0.001f, 0.02f) * 0.5f;
                    break;
                case Sfx.Break:
                    lp += (noise - lp) * 0.35f;
                    v = lp * Env(t, 0.001f, 0.08f) * (rng.Chance(0.05f) ? 2f : 1f);
                    break;
                case Sfx.UiMove:
                    phase += 1200f / Rate;
                    v = MathF.Sin(phase * MathF.Tau) * Env(t, 0.001f, 0.02f) * 0.2f;
                    break;
                case Sfx.UiConfirm:
                    phase += (t < 0.05f ? 784f : 1175f) / Rate;
                    v = MathF.Sign(MathF.Sin(phase * MathF.Tau)) * Env(t, 0.001f, 0.08f) * 0.15f;
                    break;
                case Sfx.UiBack:
                    phase += 523f / Rate;
                    v = MathF.Sin(phase * MathF.Tau) * Env(t, 0.001f, 0.06f) * 0.25f;
                    break;
                case Sfx.BossRoar:
                {
                    float f = 70f + 25f * MathF.Sin(t * 30f) + 10f * MathF.Sin(t * 7f);
                    phase += f / Rate;
                    lp += (noise - lp) * 0.08f;
                    float saw = (phase % 1f) * 2f - 1f;
                    v = MathF.Tanh((saw + lp * 2f) * 2.5f) * Env(t, 0.2f, 0.7f) * 0.6f;
                    break;
                }

                case Sfx.Go:
                    phase += (t < 0.15f ? 659f : 880f) / Rate;
                    v = (MathF.Sign(MathF.Sin(phase * MathF.Tau)) * 0.5f + MathF.Sin(phase * MathF.Tau) * 0.5f) * Env(t, 0.002f, 0.2f) * 0.25f;
                    break;
                default:
                    v = noise * Env(t, 0.001f, 0.05f);
                    break;
            }

            buf[i] = Math.Clamp(v, -1f, 1f);
        }

        return buf;
    }

    // ───────────────────────────── music ─────────────────────────────

    private static readonly int[] s_pentatonicMinor = [0, 3, 5, 7, 10, 12, 15];
    private static readonly float[] s_slendro = [0f, 2.4f, 4.8f, 7.2f, 9.6f, 12f, 14.4f];

    private static float Midi(float note) => 440f * MathF.Pow(2f, (note - 69f) / 12f);

    public static (int Bpm, int Root, int[] Progression) StyleInfo(MusicStyle style) => style switch
    {
        MusicStyle.FunkRock => (112, 40, [0, 0, 5, 7]),
        MusicStyle.MetalBlues => (128, 40, [0, 3, 5, 0]),
        MusicStyle.SurfRock => (152, 40, [0, 5, 7, 5]),
        MusicStyle.TribalPercussion => (118, 38, [0, 0, 3, 0]),
        MusicStyle.GamelanMetal => (124, 38, [0, 2, 5, 3]),
        _ => (140, 38, [0, 8, 3, 10]),
    };

    /// <summary>
    /// Renders one 4-bar stereo loop of a stem. Layer 0: drums + bass (always on).
    /// Layer 1: rhythm (power chords, gamelan, strings). Layer 2: lead (fills in when the fight heats up).
    /// </summary>
    public static float[] MusicStem(MusicStyle style, int layer)
    {
        var (bpm, root, prog) = StyleInfo(style);
        float beat = 60f / bpm;
        int bars = 4;
        int n = (int)(bars * 4 * beat * Rate);
        var buf = new float[n * 2];
        var rng = new DeterministicRandom((ulong)style * 31 + (ulong)layer);

        for (int bar = 0; bar < bars; bar++)
        {
            int chord = prog[bar % prog.Length];
            for (int step = 0; step < 16; step++)
            {
                float start = (bar * 16 + step) * beat / 4f;
                bool down = step % 4 == 0;
                if (layer == 0)
                {
                    Drums(buf, start, step, style, ref rng);
                    if (style is not MusicStyle.TribalPercussion || step % 2 == 0)
                    {
                        int bassNote = root - 12 + chord + (step % 8 == 6 ? 7 : step % 8 == 7 ? 10 : 0);
                        if (step % 2 == 0 || style is MusicStyle.MetalBlues or MusicStyle.SymphonicMetal)
                        {
                            Tone(buf, start, beat / 4f * 0.95f, Midi(bassNote), Wave.Square, 0.16f, 0.004f, 0.18f, 0f, lowpass: 0.12f);
                        }
                    }
                }
                else if (layer == 1)
                {
                    switch (style)
                    {
                        case MusicStyle.GamelanMetal:
                            if (step % 2 == 0)
                            {
                                float deg = s_slendro[(step / 2 + bar * 3) % 5];
                                Bell(buf, start, Midi(root + 24 + chord + deg), 0.22f, step % 8 == 0 ? -0.5f : 0.5f);
                            }

                            if (down)
                            {
                                PowerChord(buf, start, beat * 0.9f, root + 12 + chord, 0.12f);
                            }

                            break;
                        case MusicStyle.SymphonicMetal:
                            if (step == 0)
                            {
                                Pad(buf, start, beat * 4f, root + 24 + chord, 0.09f);
                            }

                            if (step % 2 == 0)
                            {
                                PowerChord(buf, start, beat / 2f * 0.85f, root + 12 + chord, 0.1f);
                            }

                            break;
                        case MusicStyle.TribalPercussion:
                            if (step % 3 == 0)
                            {
                                Tone(buf, start, 0.12f, Midi(root + 24 + s_pentatonicMinor[(step + bar) % 5]), Wave.Triangle, 0.18f, 0.002f, 0.08f, step % 2 == 0 ? -0.4f : 0.4f);
                            }

                            break;
                        case MusicStyle.SurfRock:
                            if (step % 2 == 0)
                            {
                                // Tremolo-picked spring-reverb surf guitar.
                                Tone(buf, start, beat / 2f, Midi(root + 24 + chord + (step % 4 == 2 ? 7 : 0)), Wave.Saw, 0.1f, 0.002f, 0.12f, -0.3f, lowpass: 0.25f, tremolo: 18f);
                            }

                            break;
                        default:
                            if (step % 4 != 3)
                            {
                                PowerChord(buf, start, beat / 4f * (step % 4 == 0 ? 1.6f : 0.8f), root + 12 + chord, 0.13f);
                            }

                            break;
                    }
                }
                else if (step % 2 == 0 && (rng.Chance(0.7f) || down))
                {
                    // Lead: a pentatonic (or slendro) riff, wailing on the downbeats.
                    float deg = style == MusicStyle.GamelanMetal ? s_slendro[rng.Range(0, 7)] : s_pentatonicMinor[rng.Range(0, s_pentatonicMinor.Length)];
                    float len = down ? beat : beat / 2f;
                    Tone(buf, start, len * 0.95f, Midi(root + 36 + chord + deg), style == MusicStyle.SymphonicMetal ? Wave.Saw : Wave.Square, 0.08f, 0.01f, 0.25f, 0.35f, lowpass: 0.3f, vibrato: 5.5f, drive: 3f);
                }
            }
        }

        // Soft clip the mix.
        for (int i = 0; i < buf.Length; i++)
        {
            buf[i] = MathF.Tanh(buf[i] * 1.2f) * 0.9f;
        }

        return buf;
    }

    private enum Wave
    {
        Sine,
        Square,
        Saw,
        Triangle,
    }

    private static void Add(float[] buf, int frame, float sample, float pan)
    {
        if ((uint)frame >= buf.Length / 2)
        {
            return;
        }

        buf[frame * 2] += sample * (1f - MathF.Max(0, pan));
        buf[frame * 2 + 1] += sample * (1f + MathF.Min(0, pan));
    }

    private static void Tone(float[] buf, float start, float length, float freq, Wave wave, float gain, float attack, float decay, float pan,
        float lowpass = 1f, float vibrato = 0f, float tremolo = 0f, float drive = 0f)
    {
        int s0 = (int)(start * Rate);
        int count = (int)((length + decay * 2) * Rate);
        float phase = 0, lp = 0;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            float f = freq * (1f + (vibrato > 0 ? 0.008f * MathF.Sin(t * vibrato * MathF.Tau) : 0));
            phase += f / Rate;
            float p = phase % 1f;
            float v = wave switch
            {
                Wave.Square => p < 0.5f ? 1f : -1f,
                Wave.Saw => p * 2f - 1f,
                Wave.Triangle => 4f * MathF.Abs(p - 0.5f) - 1f,
                _ => MathF.Sin(phase * MathF.Tau),
            };
            if (drive > 0)
            {
                v = MathF.Tanh(v * drive);
            }

            lp += (v - lp) * lowpass;
            float env = t < attack ? t / attack : t < length ? 1f : MathF.Exp(-(t - length) / decay);
            if (tremolo > 0)
            {
                env *= 0.6f + 0.4f * MathF.Sin(t * tremolo * MathF.Tau);
            }

            Add(buf, s0 + i, lp * env * gain, pan);
        }
    }

    private static void PowerChord(float[] buf, float start, float length, float rootNote, float gain)
    {
        int s0 = (int)(start * Rate);
        int count = (int)((length + 0.05f) * Rate);
        float p1 = 0, p2 = 0, p3 = 0, lp = 0;
        float f1 = Midi(rootNote), f2 = Midi(rootNote + 7), f3 = Midi(rootNote + 12);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            p1 += f1 / Rate;
            p2 += f2 * 1.003f / Rate;
            p3 += f3 * 0.997f / Rate;
            float saw = (p1 % 1f) + (p2 % 1f) + (p3 % 1f) - 1.5f;
            float dist = MathF.Tanh(saw * 6f);
            lp += (dist - lp) * 0.22f;
            float env = t < length ? 1f - 0.3f * (t / length) : MathF.Exp(-(t - length) / 0.02f);
            Add(buf, s0 + i, lp * env * gain, -0.45f);
            Add(buf, s0 + i + 600, lp * env * gain * 0.8f, 0.45f);
        }
    }

    private static void Bell(float[] buf, float start, float freq, float gain, float pan)
    {
        int s0 = (int)(start * Rate);
        int count = (int)(1.4f * Rate);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            float v = MathF.Sin(t * freq * MathF.Tau) + 0.5f * MathF.Sin(t * freq * 2.76f * MathF.Tau) * MathF.Exp(-t * 6f) + 0.25f * MathF.Sin(t * freq * 5.4f * MathF.Tau) * MathF.Exp(-t * 12f);
            // Gamelan beating: a slightly detuned twin.
            v += 0.6f * MathF.Sin(t * (freq + 4f) * MathF.Tau);
            Add(buf, s0 + i, v * MathF.Exp(-t * 2.2f) * gain * 0.5f, pan);
        }
    }

    private static void Pad(float[] buf, float start, float length, float rootNote, float gain)
    {
        foreach (int interval in new[] { 0, 3, 7, 12 })
        {
            Tone(buf, start, length, Midi(rootNote + interval), Wave.Saw, gain * 0.5f, 0.4f, 0.6f, interval % 2 == 0 ? -0.6f : 0.6f, lowpass: 0.04f);
        }
    }

    private static void Drums(float[] buf, float start, int step, MusicStyle style, ref DeterministicRandom rng)
    {
        bool tribal = style == MusicStyle.TribalPercussion;
        bool kick = style switch
        {
            MusicStyle.MetalBlues or MusicStyle.SymphonicMetal => step % 2 == 0,
            MusicStyle.SurfRock => step % 4 == 0 || step == 10,
            MusicStyle.FunkRock => step is 0 or 6 or 8 or 11,
            MusicStyle.TribalPercussion => step is 0 or 3 or 6 or 10 or 12,
            _ => step % 4 == 0 || step == 7,
        };
        bool snare = !tribal && step % 8 == 4;
        bool hat = !tribal && (step % 2 == 0 || style == MusicStyle.FunkRock);
        int s0 = (int)(start * Rate);
        if (kick)
        {
            float phase = 0;
            for (int i = 0; i < Rate / 5; i++)
            {
                float t = i / (float)Rate;
                phase += MathX.Lerp(tribal ? 110f : 150f, tribal ? 70f : 45f, MathF.Min(1, t * 20)) / Rate;
                Add(buf, s0 + i, MathF.Sin(phase * MathF.Tau) * MathF.Exp(-t * (tribal ? 9f : 14f)) * 0.55f, 0);
            }
        }

        if (snare || (tribal && step % 4 == 2))
        {
            float lp = 0;
            for (int i = 0; i < Rate / 6; i++)
            {
                float t = i / (float)Rate;
                float noise = Noise(ref rng);
                lp += (noise - lp) * (tribal ? 0.1f : 0.5f);
                float tone = MathF.Sin(t * (tribal ? 260f : 185f) * MathF.Tau) * MathF.Exp(-t * 30f);
                Add(buf, s0 + i, (lp * 0.5f + tone * 0.4f) * MathF.Exp(-t * (tribal ? 25f : 18f)) * 0.45f, tribal ? 0.3f : 0.1f);
            }
        }

        if (hat)
        {
            float prev = 0;
            for (int i = 0; i < Rate / 30; i++)
            {
                float t = i / (float)Rate;
                float noise = Noise(ref rng);
                float hp = noise - prev;
                prev = noise;
                Add(buf, s0 + i, hp * MathF.Exp(-t * 90f) * 0.12f, -0.25f);
            }
        }
    }
}
