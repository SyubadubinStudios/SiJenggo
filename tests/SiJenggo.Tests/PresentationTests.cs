using System.Text.Json;
using SiJenggo.Game.Data;
using SiJenggo.Presentation;
using SiJenggo.Presentation.Audio;

namespace SiJenggo.Tests;

/// <summary>GPU-free presentation checks: the recorded voice lines and the cutscene camera scripts.</summary>
public class PresentationTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SiJenggo.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir.FullName;
    }

    private static IEnumerable<string> ExpectedVoiceKeys()
    {
        foreach (var boss in Enum.GetValues<BossId>())
        {
            foreach (var cue in new[] { VoiceCue.Intro, VoiceCue.Taunt1, VoiceCue.Taunt2, VoiceCue.Defeat })
            {
                yield return VoiceBank.BossKey(boss, cue);
            }
        }

        foreach (var c in Enum.GetValues<CharacterId>())
        {
            foreach (var cue in new[] { VoiceCue.Ultimate, VoiceCue.Special, VoiceCue.Revive, VoiceCue.Down, VoiceCue.Victory })
            {
                yield return VoiceBank.PlayerKey(c, cue);
            }
        }
    }

    [Fact]
    public void Every_voice_cue_has_a_decodable_recording()
    {
        var bank = new VoiceBank(Path.Combine(RepoRoot(), "assets", "voice"));
        foreach (string key in ExpectedVoiceKeys())
        {
            Assert.True(bank.TryGet(key, out var samples, out int rate), $"missing or unreadable voice line {key}.wav");
            Assert.Equal(22050, rate);
            Assert.InRange(samples.Length / (float)rate, 0.3f, 12f);
            Assert.Contains(samples, s => MathF.Abs(s) > 0.5f);
        }
    }

    [Fact]
    public void Voice_manifest_matches_the_cues_the_game_plays()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "tools", "voice", "lines.json")));
        var keys = doc.RootElement.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("key").GetString()).ToHashSet();
        Assert.True(keys.SetEquals(ExpectedVoiceKeys()));
    }

    [Fact]
    public void Wav_decoder_mixes_stereo_to_mono()
    {
        // 2 frames of 16-bit stereo at 8 kHz: (1000, 3000) and (-2000, 0).
        var data = new List<byte>();
        void Add(string s) => data.AddRange(System.Text.Encoding.ASCII.GetBytes(s));
        void I32(int v) => data.AddRange(BitConverter.GetBytes(v));
        void I16(short v) => data.AddRange(BitConverter.GetBytes(v));
        Add("RIFF");
        I32(36 + 8);
        Add("WAVE");
        Add("fmt ");
        I32(16);
        I16(1);
        I16(2);
        I32(8000);
        I32(8000 * 4);
        I16(4);
        I16(16);
        Add("data");
        I32(8);
        I16(1000);
        I16(3000);
        I16(-2000);
        I16(0);
        var (samples, rate) = VoiceBank.DecodeWav(data.ToArray());
        Assert.Equal(8000, rate);
        Assert.Equal(2, samples.Length);
        Assert.Equal(2000 / 32768f, samples[0], 5);
        Assert.Equal(-1000 / 32768f, samples[1], 5);
    }

    [Fact]
    public void Chapter_intros_have_one_shot_per_story_line_plus_the_heroes()
    {
        foreach (var chapter in Chapters.All)
        {
            var lines = chapter.Story.Select(l => l.Id).ToArray();
            var shots = Cutscenes.ChapterIntro(chapter, lines);
            Assert.Equal(lines.Length + 1, shots.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                Assert.Equal(i, shots[i].Line);
                Assert.True(shots[i].Duration >= 2.6f);
                // Every shot stays inside the stage.
                Assert.InRange(shots[i].FromLook.X, -1f, chapter.Length + 1f);
                Assert.InRange(shots[i].ToLook.X, -1f, chapter.Length + 1f);
            }

            Assert.Equal(-1, shots[^1].Line);
        }

        Assert.Equal(Chapters.Epilogue.Length, Cutscenes.Epilogue(160, Chapters.Epilogue.Select(l => l.En).ToArray()).Length);
    }
}
