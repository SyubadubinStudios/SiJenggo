using System.Diagnostics;
using System.Reflection;
using SiJenggo.Core;
using SiJenggo.Game;
using SiJenggo.Game.Data;
using SiJenggo.Game.Net;
using Xunit.Abstractions;

namespace SiJenggo.Tests;

public class RollbackTests(ITestOutputHelper output)
{
    /// <summary>Inputs that are held for a while (like a real player) so prediction is sometimes right.</summary>
    private static InputButtons NextButtons(ref DeterministicRandom rng, InputButtons current)
    {
        if (!rng.Chance(0.25f))
        {
            return current;
        }

        var b = InputButtons.None;
        if (rng.Chance(0.6f)) b |= InputButtons.Right;
        if (rng.Chance(0.1f)) b |= InputButtons.Left;
        if (rng.Chance(0.15f)) b |= InputButtons.Up;
        if (rng.Chance(0.15f)) b |= InputButtons.Down;
        if (rng.Chance(0.35f)) b |= InputButtons.Light;
        if (rng.Chance(0.08f)) b |= InputButtons.Heavy;
        if (rng.Chance(0.04f)) b |= InputButtons.Jump;
        if (rng.Chance(0.03f)) b |= InputButtons.Special;
        if (rng.Chance(0.03f)) b |= InputButtons.Grab;
        if (rng.Chance(0.02f)) b |= InputButtons.Dodge;
        return b;
    }

    private static InputFrame[] Script(int ticks, int players, ulong seed)
    {
        var rng = new DeterministicRandom(seed);
        var frames = new InputFrame[ticks * players];
        var held = new InputButtons[players];
        for (int t = 0; t < ticks; t++)
        {
            for (int p = 0; p < players; p++)
            {
                held[p] = NextButtons(ref rng, held[p]);
                frames[t * players + p] = new InputFrame(held[p]);
            }
        }

        return frames;
    }

    private static void Play(GameSession s, InputFrame[] script, int from, int count)
    {
        int n = s.PlayerCount;
        for (int t = from; t < from + count; t++)
        {
            s.Step(script.AsSpan(t * n, n));
        }
    }

    [Fact]
    public void Save_run_load_rerun_reproduces_the_exact_state()
    {
        const int n = 1500, m = 400;
        var script = Script(n + m, 2, 21);
        var s = TestHelpers.NewSession(players: 2, seed: 77);
        Play(s, script, 0, n);
        Assert.True(s.World.AliveCount > 2, "the save point should have more than just the players alive");

        var snapshot = new SessionSnapshot();
        s.SaveState(snapshot);
        ulong atSave = s.FullStateHash();
        uint atSaveChecksum = s.Checksum();

        Play(s, script, n, m);
        uint firstChecksum = s.Checksum();
        ulong firstHash = s.FullStateHash();
        Assert.NotEqual(atSave, firstHash);

        s.LoadState(snapshot);
        Assert.Equal(n, s.Tick);
        Assert.Equal(atSave, s.FullStateHash());
        Assert.Equal(atSaveChecksum, s.Checksum());

        Play(s, script, n, m);
        Assert.Equal(firstChecksum, s.Checksum());
        Assert.Equal(firstHash, s.FullStateHash());

        // And identical to a session that never rewound.
        var reference = TestHelpers.NewSession(players: 2, seed: 77);
        Play(reference, script, 0, n + m);
        Assert.Equal(reference.FullStateHash(), s.FullStateHash());
        Assert.Equal(reference.Stats.KnockOuts, s.Stats.KnockOuts);
    }

    [Fact]
    public void A_restored_session_can_take_a_different_future()
    {
        const int n = 900, m = 600;
        var scriptA = Script(n + m, 2, 5);
        var scriptB = Script(n + m, 2, 6);
        Array.Copy(scriptA, scriptB, n * 2); // same past, different future

        var s = TestHelpers.NewSession(players: 2, seed: 3);
        Play(s, scriptA, 0, n);
        var snapshot = new SessionSnapshot();
        s.SaveState(snapshot);

        Play(s, scriptA, n, m);
        ulong futureA = s.FullStateHash();

        s.LoadState(snapshot);
        Play(s, scriptB, n, m);
        ulong futureB = s.FullStateHash();
        Assert.NotEqual(futureA, futureB);

        var referenceB = TestHelpers.NewSession(players: 2, seed: 3);
        Play(referenceB, scriptB, 0, n + m);
        Assert.Equal(referenceB.FullStateHash(), futureB);
        Assert.Equal(referenceB.Checksum(), s.Checksum());

        // Back to the first future once more, from the same snapshot.
        s.LoadState(snapshot);
        Play(s, scriptA, n, m);
        Assert.Equal(futureA, s.FullStateHash());
    }

    [Fact]
    public void Boss_arena_state_survives_a_round_trip()
    {
        var config = new SessionConfig { Players = [PlayerLoadout.Base(CharacterId.Jenggo), PlayerLoadout.Base(CharacterId.Rani)], Seed = 9 };
        var plan = new StagePlan { Kind = StageKind.BossArena, Chapter = Chapters.Get(2) };
        var script = Script(1200, 2, 8);
        var s = new GameSession(config, plan);
        var reference = new GameSession(config, plan);
        var snapshot = new SessionSnapshot();
        for (int t = 0; t < 1200; t += 100)
        {
            s.SaveState(snapshot);
            Play(s, script, t, 100);
            s.LoadState(snapshot);
            Play(s, script, t, 100);
            Play(reference, script, t, 100);
            Assert.Equal(reference.FullStateHash(), s.FullStateHash());
        }
    }

    /// <summary>
    /// Guards against a new GameSession field that SaveState/LoadState forgot. If this fails after adding a
    /// field: copy it in GameSessionSnapshot.cs (SaveState, LoadState, FullStateHash) and list it here.
    /// </summary>
    [Fact]
    public void Every_session_field_is_covered_by_the_snapshot()
    {
        string[] snapshotted =
        [
            "_rng", "_players", "_fighters", "_spawnQueue", "State", "Tick", "Stats", "CameraX", "CameraLocked",
            "BossEntity", "Music", "MusicIntensity", "GoArrow", "CinematicTicks", "ChallengeResult",
            "_waveIndex", "_waveActive", "_waveMaxAlive", "_spawnTimer", "_lockCenter", "_bossTriggered",
            "_bossDefeated", "_areaIndex", "_ambientTimer", "_endTicks", "_survivalWave", "_survivalRest",
            "World", // copied pool by pool
        ];
        string[] immutableOrTransient =
        [
            "Config", "Stage", "PlayerCount", "DifficultyScale", // fixed at construction
            "Events", "SuppressEvents", // presentation only
            "_scratch", // per-call temporary
        ];

        var fields = typeof(GameSession).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var missing = new List<string>();
        foreach (var f in fields)
        {
            string name = f.Name.StartsWith('<') ? f.Name[1..f.Name.IndexOf('>')] : f.Name;
            if (!snapshotted.Contains(name) && !immutableOrTransient.Contains(name))
            {
                missing.Add(name);
            }
        }

        Assert.True(missing.Count == 0, "GameSession fields not handled by SaveState/LoadState: " + string.Join(", ", missing));
    }

    [Fact]
    public void Snapshots_do_not_allocate_after_warm_up_and_are_cheap()
    {
        var script = Script(1500, 2, 1);
        var s = TestHelpers.NewSession(players: 2, seed: 4);
        Play(s, script, 0, 1500);
        var snapshots = new SessionSnapshot[10];
        for (int i = 0; i < snapshots.Length; i++)
        {
            snapshots[i] = new SessionSnapshot();
            s.SaveState(snapshots[i]);
        }

        s.LoadState(snapshots[0]);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            s.SaveState(snapshots[i % snapshots.Length]);
            s.LoadState(snapshots[(i + 3) % snapshots.Length]);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        const int rounds = 2000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < rounds; i++)
        {
            s.SaveState(snapshots[i % snapshots.Length]);
        }

        sw.Stop();
        double saveUs = sw.Elapsed.TotalMilliseconds * 1000 / rounds;
        sw.Restart();
        for (int i = 0; i < rounds; i++)
        {
            s.LoadState(snapshots[i % snapshots.Length]);
        }

        sw.Stop();
        double loadUs = sw.Elapsed.TotalMilliseconds * 1000 / rounds;

        var step = Stopwatch.StartNew();
        Play(s, script, 0, 300);
        double stepUs = step.Elapsed.TotalMilliseconds * 1000 / 300;
        output.WriteLine($"entities alive {s.World.AliveCount}: SaveState {saveUs:F2} us, LoadState {loadUs:F2} us, Step {stepUs:F2} us");
        Assert.Equal(0, allocated);
    }

    /// <summary>Fixed-size packet queue so the test itself does not allocate per packet.</summary>
    private sealed class BufferPipe
    {
        private readonly byte[][] _buffers = Enumerable.Range(0, 64).Select(_ => new byte[512]).ToArray();
        private readonly int[] _lengths = new int[64];
        private int _count;

        public void Send(ReadOnlySpan<byte> data, bool reliable)
        {
            data.CopyTo(_buffers[_count]);
            _lengths[_count++] = data.Length;
        }

        public void Deliver(RollbackSession to)
        {
            for (int i = 0; i < _count; i++)
            {
                to.Receive(_buffers[i].AsSpan(0, _lengths[i]));
            }

            _count = 0;
        }
    }

    [Fact]
    public void Rollback_ticks_do_not_allocate_after_warm_up()
    {
        var toA = new BufferPipe();
        var toB = new BufferPipe();
        var sessionA = TestHelpers.NewSession(players: 2, seed: 8);
        var sessionB = TestHelpers.NewSession(players: 2, seed: 8);
        var peerA = new RollbackSession(sessionA, 0, 2, toB.Send, inputDelay: 0);
        var peerB = new RollbackSession(sessionB, 1, 2, toA.Send, inputDelay: 0);
        var rng = new DeterministicRandom(2);
        InputButtons a = default, b = default;
        long before = 0;
        for (int t = 0; t < 1200; t++)
        {
            if (t == 900)
            {
                before = GC.GetAllocatedBytesForCurrentThread();
            }

            a = NextButtons(ref rng, a);
            b = NextButtons(ref rng, b);
            peerA.AdvanceFrame(a);
            peerB.AdvanceFrame(b);
            toA.Deliver(peerA);
            toB.Deliver(peerB);
            sessionA.Events.Clear();
            sessionB.Events.Clear();
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(peerA.Rollbacks > 50, $"rollbacks {peerA.Rollbacks}");
        Assert.True(allocated == 0, $"{allocated} bytes allocated in 300 rollback ticks");
    }

    [Fact]
    public void Match_start_carries_the_net_mode()
    {
        var start = new MatchStart(1UL, GameModeKind.Arcade, 2, Difficulty.Normal, [CharacterId.Jenggo, CharacterId.Bima], NetMode.Rollback);
        Assert.Equal(NetMode.Rollback, MatchStart.Decode(start.Encode())!.NetMode);

        // A start without the trailing byte (older build) means lockstep.
        var legacy = start.Encode()[..^1];
        Assert.Equal(NetMode.Lockstep, MatchStart.Decode(legacy)!.NetMode);
    }

    /// <summary>Unreliable in-memory link with latency, jitter (so reordering) and loss; reliable packets only get latency.</summary>
    private sealed class LossyPipe(ulong seed, float loss, int minLatency, int maxLatency)
    {
        private readonly List<(int At, byte[] Data)> _inFlight = [];
        private DeterministicRandom _rng = new(seed);
        public int Now;
        public bool Perfect;

        public void Send(ReadOnlySpan<byte> data, bool reliable)
        {
            if (!Perfect && !reliable && _rng.Chance(loss))
            {
                return;
            }

            int latency = Perfect ? 0 : _rng.Range(minLatency, maxLatency + 1);
            _inFlight.Add((Now + latency, data.ToArray()));
        }

        public void Deliver(RollbackSession to)
        {
            for (int i = 0; i < _inFlight.Count;)
            {
                if (_inFlight[i].At <= Now)
                {
                    to.Receive(_inFlight[i].Data);
                    _inFlight.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        }
    }

    [Theory]
    [InlineData(0.2f, 2, 6, 1)]
    [InlineData(0.35f, 4, 9, 0)]
    public void Rollback_peers_converge_on_a_laggy_lossy_link(float loss, int minLatency, int maxLatency, int inputDelay)
    {
        const ulong seed = 31;
        var sessionA = TestHelpers.NewSession(players: 2, seed: seed);
        var sessionB = TestHelpers.NewSession(players: 2, seed: seed);
        var reference = TestHelpers.NewSession(players: 2, seed: seed);
        var toA = new LossyPipe(1, loss, minLatency, maxLatency);
        var toB = new LossyPipe(2, loss, minLatency, maxLatency);
        var peerA = new RollbackSession(sessionA, 0, 2, toB.Send, maxRollback: 8, inputDelay: inputDelay);
        var peerB = new RollbackSession(sessionB, 1, 2, toA.Send, maxRollback: 8, inputDelay: inputDelay);
        var rng = new DeterministicRandom(17);
        InputButtons heldA = default, heldB = default;
        var refInputs = new InputFrame[2];
        int eventsA = 0, eventsRef = 0;

        void Tick(InputButtons a, InputButtons b)
        {
            toA.Now++;
            toB.Now++;
            toA.Deliver(peerA);
            toB.Deliver(peerB);
            peerA.AdvanceFrame(a);
            peerB.AdvanceFrame(b);
            eventsA += CountEvents(sessionA);
            sessionA.Events.Clear();
            sessionB.Events.Clear();

            // The reference session plays the authoritative (confirmed) input stream, never predicting.
            while (reference.Tick <= Math.Min(peerA.ConfirmedFrame, peerB.ConfirmedFrame))
            {
                int f = reference.Tick;
                Assert.True(peerA.TryGetConfirmedInput(0, f, out var r0));
                Assert.True(peerA.TryGetConfirmedInput(1, f, out var r1));
                Assert.True(peerB.TryGetConfirmedInput(0, f, out var b0) && b0 == r0);
                Assert.True(peerB.TryGetConfirmedInput(1, f, out var b1) && b1 == r1);
                refInputs[0] = new InputFrame(r0);
                refInputs[1] = new InputFrame(r1);
                reference.Step(refInputs);
                eventsRef += CountEvents(reference);
                reference.Events.Clear();
            }
        }

        for (int t = 0; t < 2400; t++)
        {
            heldA = NextButtons(ref rng, heldA);
            heldB = NextButtons(ref rng, heldB);
            Tick(heldA, heldB);
        }

        // Settle: a perfect link and idle hands until both peers have confirmed everything they simulated.
        toA.Perfect = toB.Perfect = true;
        for (int t = 0; t < 60; t++)
        {
            Tick(InputButtons.None, InputButtons.None);
        }

        // Bring the peer that is behind level with the other one.
        for (int guard = 0; guard < 64 && peerA.Frame != peerB.Frame; guard++)
        {
            var behind = peerA.Frame < peerB.Frame ? peerA : peerB;
            behind.AdvanceFrame(InputButtons.None);
            toA.Deliver(peerA);
            toB.Deliver(peerB);
        }

        output.WriteLine($"A: frame {peerA.Frame}, rollbacks {peerA.Rollbacks}, resimulated {peerA.RolledBackFrames}, deepest {peerA.DeepestRollback}, stalls {peerA.StallTicks}, sync waits {peerA.SyncWaitTicks}, events {eventsA}");
        output.WriteLine($"events: peer A {eventsA}, reference {eventsRef}");
        output.WriteLine($"B: frame {peerB.Frame}, rollbacks {peerB.Rollbacks}, resimulated {peerB.RolledBackFrames}, deepest {peerB.DeepestRollback}, stalls {peerB.StallTicks}, sync waits {peerB.SyncWaitTicks}");

        Assert.Equal(peerA.Frame, peerB.Frame);
        Assert.False(peerA.HasPendingRollback);
        Assert.False(peerB.HasPendingRollback);
        Assert.True(peerA.ConfirmedFrame >= peerA.Frame - 1 && peerB.ConfirmedFrame >= peerB.Frame - 1);
        Assert.True(peerA.Frame > 2000, $"peers should keep running despite the bad link (frame {peerA.Frame})");
        Assert.True(peerA.Rollbacks > 50 && peerB.Rollbacks > 50, "the test link should force plenty of rollbacks");
        Assert.True(peerA.DeepestRollback <= peerA.MaxRollback);
        Assert.False(peerA.Desynced);
        Assert.False(peerB.Desynced);

        // Catch the reference up to the peers and compare everything.
        while (reference.Tick < peerA.Frame)
        {
            int f = reference.Tick;
            Assert.True(peerA.TryGetConfirmedInput(0, f, out var r0) & peerA.TryGetConfirmedInput(1, f, out var r1));
            refInputs[0] = new InputFrame(r0);
            refInputs[1] = new InputFrame(r1);
            reference.Step(refInputs);
        }

        Assert.Equal(reference.Checksum(), sessionA.Checksum());
        Assert.Equal(reference.Checksum(), sessionB.Checksum());
        Assert.Equal(reference.FullStateHash(), sessionA.FullStateHash());
        Assert.Equal(reference.FullStateHash(), sessionB.FullStateHash());
        Assert.True(reference.Stats.KnockOuts > 0);
        // Re-simulated frames are silent: without suppression the peer would emit roughly
        // (frames + resimulated frames) / frames times as many events as the authoritative timeline.
        Assert.True(peerA.RolledBackFrames > peerA.Frame / 2, "the link should re-simulate a large share of frames");
        // (Corrected outcomes lose their effects, so the peer may show somewhat fewer, never noticeably more.)
        Assert.InRange(eventsA, 1, eventsRef * 1.2);
    }

    private static int CountEvents(GameSession s)
    {
        int n = 0;
        foreach (var ev in s.Events.Items)
        {
            if (ev.Kind != GameEventKind.Footstep)
            {
                n++;
            }
        }

        return n;
    }
}
