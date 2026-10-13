using SiJenggo.Core;
using SiJenggo.Game;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;
using SiJenggo.Game.Net;
using SiJenggo.Game.Persistence;

namespace SiJenggo.Tests;

public class SessionTests
{
    private static InputButtons RandomButtons(ref DeterministicRandom rng)
    {
        var b = InputButtons.None;
        if (rng.Chance(0.6f)) b |= InputButtons.Right;
        if (rng.Chance(0.15f)) b |= InputButtons.Up;
        if (rng.Chance(0.15f)) b |= InputButtons.Down;
        if (rng.Chance(0.3f)) b |= InputButtons.Light;
        if (rng.Chance(0.05f)) b |= InputButtons.Heavy;
        if (rng.Chance(0.02f)) b |= InputButtons.Jump;
        if (rng.Chance(0.02f)) b |= InputButtons.Special;
        if (rng.Chance(0.02f)) b |= InputButtons.Grab;
        return b;
    }

    [Fact]
    public void Same_seed_and_inputs_give_identical_simulations()
    {
        var a = TestHelpers.NewSession(players: 2, seed: 99);
        var b = TestHelpers.NewSession(players: 2, seed: 99);
        var rng = new DeterministicRandom(7);
        var inputs = new InputFrame[2];
        for (int i = 0; i < 3000; i++)
        {
            inputs[0] = new InputFrame(RandomButtons(ref rng));
            inputs[1] = new InputFrame(RandomButtons(ref rng));
            a.Step(inputs);
            b.Step(inputs);
            if (i % 250 == 0)
            {
                Assert.Equal(a.Checksum(), b.Checksum());
            }
        }

        Assert.Equal(a.Checksum(), b.Checksum());
        Assert.True(a.Stats.KnockOuts > 0, "random play should knock someone out in 50 seconds");
    }

    [Fact]
    public void Lockstep_peers_stay_in_sync_over_a_lossy_link()
    {
        var sessionA = TestHelpers.NewSession(players: 2, seed: 5);
        var sessionB = TestHelpers.NewSession(players: 2, seed: 5);
        var toA = new Queue<byte[]>();
        var toB = new Queue<byte[]>();
        var loss = new DeterministicRandom(3);
        LockstepSession? peerA = null, peerB = null;
        peerA = new LockstepSession(0, 2, (data, reliable) => { if (reliable || !loss.Chance(0.3f)) toB.Enqueue(data.ToArray()); });
        peerB = new LockstepSession(1, 2, (data, reliable) => { if (reliable || !loss.Chance(0.3f)) toA.Enqueue(data.ToArray()); });
        var rng = new DeterministicRandom(11);

        for (int tick = 0; tick < 2000; tick++)
        {
            peerA.SubmitLocal(RandomButtons(ref rng));
            peerB.SubmitLocal(RandomButtons(ref rng));
            while (toA.Count > 0) peerA.Receive(toA.Dequeue());
            while (toB.Count > 0) peerB.Receive(toB.Dequeue());
            while (peerA.TryNextFrame(out var inputs))
            {
                sessionA.Step(inputs);
                peerA.AfterFrame(peerA.Frame, sessionA.Checksum());
            }

            while (peerB.TryNextFrame(out var inputs))
            {
                sessionB.Step(inputs);
                peerB.AfterFrame(peerB.Frame, sessionB.Checksum());
            }
        }

        while (toA.Count > 0) peerA.Receive(toA.Dequeue());
        while (toB.Count > 0) peerB.Receive(toB.Dequeue());
        Assert.True(sessionA.Tick > 1500);
        int common = Math.Min(sessionA.Tick, sessionB.Tick);
        Assert.False(peerA.Desynced);
        Assert.False(peerB.Desynced);
        if (sessionA.Tick == sessionB.Tick)
        {
            Assert.Equal(sessionA.Checksum(), sessionB.Checksum());
        }

        Assert.True(common > 0);
    }

    [Fact]
    public void Match_start_round_trips()
    {
        var start = new MatchStart(123456789UL, GameModeKind.Arcade, 3, Difficulty.Jagoan, [CharacterId.Jenggo, CharacterId.Rani]);
        var decoded = MatchStart.Decode(start.Encode());
        Assert.NotNull(decoded);
        Assert.Equal(start.Seed, decoded.Seed);
        Assert.Equal(start.Mode, decoded.Mode);
        Assert.Equal(start.Characters, decoded.Characters);
    }

    [Fact]
    public void Walking_into_a_wave_locks_the_camera_and_spawns_enemies()
    {
        var s = TestHelpers.NewSession();
        s.Run(240, InputButtons.Right);
        Assert.True(s.CameraLocked);
        s.Run(200);
        Assert.True(s.World.Pool<Enemy>().Count > 0);
    }

    [Fact]
    public void Two_ai_fighters_can_clear_the_first_chapter()
    {
        var s = TestHelpers.NewSession(CharacterId.Jenggo, players: 2, ai: true, seed: 2026, chapter: 0);
        RunToEnd(s);
        Assert.True(s.State == SessionState.StageClear, $"state {s.State} after {s.Tick} ticks, waves {s.WaveNumber}, boss {(s.World.IsAlive(s.BossEntity) ? s.H(s.BossEntity).Current : -1)}");
    }

    /// <summary>Every chapter is playable end to end: waves stream, the camera unlocks, the boss arrives and takes damage.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Ai_fighters_reach_and_hurt_every_boss(int chapter)
    {
        var s = TestHelpers.NewSession(CharacterId.Jenggo, players: 2, ai: true, seed: 2026, chapter: chapter);
        RunToEnd(s);
        Assert.True(s.Stats.KnockOuts > 10, $"KOs {s.Stats.KnockOuts}, waves {s.WaveNumber}, state {s.State}, tick {s.Tick}");
        Assert.True(s.World.IsAlive(s.BossEntity), $"boss never spawned (waves {s.WaveNumber}, state {s.State})");
        Assert.True(s.H(s.BossEntity).Current < s.H(s.BossEntity).Max);
    }

    private static void RunToEnd(GameSession s)
    {
        int limit = 60 * 60 * 15;
        while (s.State is not (SessionState.StageClear or SessionState.GameOver) && s.Tick < limit)
        {
            s.Run(60);
        }
    }

    [Fact]
    public void Survival_keeps_sending_waves()
    {
        var s = TestHelpers.NewSession(players: 2, ai: true, kind: StageKind.SurvivalArena);
        s.Run(60 * 120);
        Assert.True(s.Stats.WavesSurvived >= 2, $"waves {s.Stats.WavesSurvived}");
    }

    [Fact]
    public void Boss_arena_spawns_the_boss_with_an_intro()
    {
        var config = new SessionConfig { Players = [PlayerLoadout.Base(CharacterId.Jenggo)] };
        var s = new GameSession(config, new StagePlan { Kind = StageKind.BossArena, Chapter = Chapters.Get(1), Boss = BossId.KaptenBadai });
        Assert.True(s.World.IsAlive(s.BossEntity));
        Assert.Equal(SessionState.Cinematic, s.State);
        Assert.Contains(s.Events.Items.ToArray(), e => e.Kind == GameEventKind.BossIntro);
        s.Run(200);
        Assert.Equal(SessionState.Playing, s.State);
    }

    [Fact]
    public void Profile_save_and_load_round_trip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sijenggo-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var save = new SaveService(dir);
            var profile = new Profile { StoryChapterUnlocked = 3 };
            profile.For(CharacterId.Bima).Xp = 1234;
            profile.For(CharacterId.Bima).Skills.Add("power.1");
            profile.LearnedJurus.Add(Moves.JurusBetawi);
            save.Save(profile);
            save.Save(profile);

            var loaded = save.Load();
            Assert.Equal(3, loaded.StoryChapterUnlocked);
            Assert.Equal(1234, loaded.For(CharacterId.Bima).Xp);
            Assert.Contains("power.1", loaded.For(CharacterId.Bima).Skills);
            Assert.Contains(Moves.JurusBetawi, loaded.LearnedJurus);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Skill_tree_requires_points_and_previous_tier()
    {
        var progress = new CharacterProgress();
        var tier2 = SkillTree.Find("power.2")!;
        var tier1 = SkillTree.Find("power.1")!;
        Assert.False(progress.CanUnlock(tier1));

        progress.Xp = Leveling.XpForLevel(4);
        Assert.False(progress.CanUnlock(tier2));
        Assert.True(progress.Unlock(tier1));
        Assert.True(progress.Unlock(tier2));

        var stats = StatsBuilder.Build(CharacterId.Jenggo, progress);
        Assert.True(stats.Stats.Power > Characters.Jenggo.Power);
    }

    [Fact]
    public void Story_clear_unlocks_the_next_chapter_jurus_and_gear()
    {
        var profile = new Profile();
        var campaign = new Campaign(GameModeKind.Story, profile, [new PlayerSlot(CharacterId.Jenggo, true), new PlayerSlot(CharacterId.Bima, true)], Difficulty.Normal, 0, seed: 77);
        var session = campaign.CreateSession();
        while (session.State is not (SessionState.StageClear or SessionState.GameOver) && session.Tick < 60 * 60 * 15)
        {
            session.Run(60);
        }

        Assert.Equal(SessionState.StageClear, session.State);
        var result = campaign.Complete(session);
        Assert.True(result.Cleared);
        Assert.Equal(1, profile.StoryChapterUnlocked);
        Assert.Contains(Moves.JurusBetawi, profile.LearnedJurus);
        Assert.Contains(EquipmentId.JaketKulitKodir, profile.Inventory);
        Assert.Contains("turun-gunung", profile.Achievements);
    }

    [Fact]
    public void Challenge_goals_resolve()
    {
        var keroyokan = Challenges.Find("ch.keroyokan")!;
        var stats = new SessionStats { KnockOuts = 25, Ticks = 100 };
        Assert.True(keroyokan.Evaluate(stats, false));
        stats = new SessionStats { KnockOuts = 3, Ticks = keroyokan.TimeLimit };
        Assert.False(keroyokan.Evaluate(stats, false));

        var clean = Challenges.Find("ch.kodir-bersih")!;
        Assert.Null(clean.Evaluate(new SessionStats(), false));
        Assert.False(clean.Evaluate(new SessionStats { DamageTaken = 1 }, false));
        Assert.True(clean.Evaluate(new SessionStats(), true));
    }
}
