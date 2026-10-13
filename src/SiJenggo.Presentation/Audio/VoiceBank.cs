using System.Buffers.Binary;
using SiJenggo.Game.Data;

namespace SiJenggo.Presentation.Audio;

/// <summary>What a voice line is for. Files are named <c>boss_{BossId}_{cue}.wav</c> / <c>player_{CharacterId}_{cue}.wav</c>.</summary>
public enum VoiceCue : byte
{
    Intro,
    Taunt1,
    Taunt2,
    Defeat,
    Ultimate,
    Special,
    Revive,
    Down,
    Victory,
}

/// <summary>
/// Recorded voice lines (generated with ElevenLabs through the Rodin MCP, converted by
/// <c>tools/voice/convert.ps1</c> to 16-bit mono WAV in <c>assets/voice</c>). Files are decoded on first
/// use and cached as float samples. A missing file is simply silent: the subtitles still show.
/// </summary>
public sealed class VoiceBank
{
    private readonly string _folder;
    private readonly Dictionary<string, (float[] Samples, int Rate)?> _cache = [];

    public VoiceBank(string? folder = null)
    {
        _folder = folder ?? Path.Combine(AssetPaths.Root, "voice");
    }

    public static string BossKey(BossId boss, VoiceCue cue) => $"boss_{boss}_{CueName(cue)}";

    public static string PlayerKey(CharacterId character, VoiceCue cue) => $"player_{character}_{CueName(cue)}";

    private static string CueName(VoiceCue cue) => cue.ToString().ToLowerInvariant();

    public bool TryGet(string key, out float[] samples, out int rate)
    {
        if (!_cache.TryGetValue(key, out var entry))
        {
            entry = Load(Path.Combine(_folder, key + ".wav"));
            _cache[key] = entry;
        }

        samples = entry?.Samples ?? [];
        rate = entry?.Rate ?? 0;
        return entry is not null;
    }

    private static (float[] Samples, int Rate)? Load(string path)
    {
        try
        {
            return File.Exists(path) ? DecodeWav(File.ReadAllBytes(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Decodes a PCM 16-bit WAV, mixing any extra channels down to mono.</summary>
    public static (float[] Samples, int Rate) DecodeWav(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12 || !data[..4].SequenceEqual("RIFF"u8) || !data[8..12].SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("Not a RIFF/WAVE file.");
        }

        int channels = 0, rate = 0, bits = 0;
        int pos = 12;
        while (pos + 8 <= data.Length)
        {
            var id = data.Slice(pos, 4);
            int size = BinaryPrimitives.ReadInt32LittleEndian(data[(pos + 4)..]);
            var body = data.Slice(pos + 8, Math.Min(size, data.Length - pos - 8));
            if (id.SequenceEqual("fmt "u8))
            {
                if (BinaryPrimitives.ReadUInt16LittleEndian(body) != 1)
                {
                    throw new InvalidDataException("Only PCM WAV is supported.");
                }

                channels = BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
                rate = BinaryPrimitives.ReadInt32LittleEndian(body[4..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(body[14..]);
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (bits != 16 || channels < 1)
                {
                    throw new InvalidDataException("Only 16-bit PCM WAV is supported.");
                }

                int frames = body.Length / (2 * channels);
                var samples = new float[frames];
                for (int i = 0; i < frames; i++)
                {
                    float sum = 0;
                    for (int c = 0; c < channels; c++)
                    {
                        sum += BinaryPrimitives.ReadInt16LittleEndian(body[((i * channels + c) * 2)..]) / 32768f;
                    }

                    samples[i] = sum / channels;
                }

                return (samples, rate);
            }

            pos += 8 + size + (size & 1);
        }

        throw new InvalidDataException("WAV has no data chunk.");
    }
}
