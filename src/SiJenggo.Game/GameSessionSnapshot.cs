using SiJenggo.Core;
using SiJenggo.Core.Ecs;
using SiJenggo.Game.Data;

namespace SiJenggo.Game;

/// <summary>
/// A complete copy of a <see cref="GameSession"/>'s simulation state, for rollback netcode (and potentially
/// instant replays). Create one per ring slot and reuse it: after the first <see cref="GameSession.SaveState"/>
/// (warm-up) saving and loading copy into the existing buffers and do not allocate.
/// <para>Not part of the state: <see cref="GameSession.Events"/> (presentation only), and the immutable setup
/// (<see cref="GameSession.Config"/>, <see cref="GameSession.Stage"/>, player count, difficulty scale).
/// A snapshot may only be loaded into the session it was saved from (or one built from the same config).</para>
/// </summary>
public sealed class SessionSnapshot
{
    internal World? World;
    internal readonly Entity[] Players = new Entity[GameSession.MaxPlayers];
    internal readonly List<Entity> Fighters = new(64);
    internal readonly List<EnemyKind> SpawnQueue = new(32);

    internal DeterministicRandom Rng;
    internal SessionState State;
    internal SessionStats Stats;
    internal float CameraX;
    internal bool CameraLocked;
    internal Entity BossEntity;
    internal MusicState Music;
    internal float MusicIntensity;
    internal bool GoArrow;
    internal int CinematicTicks;
    internal bool? ChallengeResult;

    internal int WaveIndex;
    internal bool WaveActive;
    internal int WaveMaxAlive;
    internal int SpawnTimer;
    internal float LockCenter;
    internal bool BossTriggered;
    internal bool BossDefeated;
    internal int AreaIndex;
    internal int AmbientTimer;
    internal int EndTicks;
    internal int SurvivalWave;
    internal int SurvivalRest;

    /// <summary><see cref="GameSession.Tick"/> at the time of the save; -1 while empty.</summary>
    public int Tick { get; internal set; } = -1;

    public bool HasData => Tick >= 0;
}

/// <summary>
/// Snapshot / restore of the whole simulation. Every mutable field of every <c>GameSession</c> partial must be
/// copied here; <c>RollbackTests.Every_session_field_is_covered_by_the_snapshot</c> fails when a new field is
/// added without being listed, so keep both in sync.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>
    /// While true, <see cref="Emit"/> drops presentation events. Rollback sets it while re-simulating frames
    /// that were already presented, so sparks, sounds and toasts are not played twice.
    /// </summary>
    internal bool SuppressEvents { get; set; }

    /// <summary>Copies the complete simulation state into <paramref name="snapshot"/> (reusing its buffers).</summary>
    public void SaveState(SessionSnapshot snapshot)
    {
        (snapshot.World ??= new World(World.IdCapacity)).CopyFrom(World);
        Array.Copy(_players, snapshot.Players, MaxPlayers);
        CopyList(_fighters, snapshot.Fighters);
        CopyList(_spawnQueue, snapshot.SpawnQueue);

        snapshot.Rng = _rng;
        snapshot.State = State;
        snapshot.Tick = Tick;
        snapshot.Stats = Stats;
        snapshot.CameraX = CameraX;
        snapshot.CameraLocked = CameraLocked;
        snapshot.BossEntity = BossEntity;
        snapshot.Music = Music;
        snapshot.MusicIntensity = MusicIntensity;
        snapshot.GoArrow = GoArrow;
        snapshot.CinematicTicks = CinematicTicks;
        snapshot.ChallengeResult = ChallengeResult;

        snapshot.WaveIndex = _waveIndex;
        snapshot.WaveActive = _waveActive;
        snapshot.WaveMaxAlive = _waveMaxAlive;
        snapshot.SpawnTimer = _spawnTimer;
        snapshot.LockCenter = _lockCenter;
        snapshot.BossTriggered = _bossTriggered;
        snapshot.BossDefeated = _bossDefeated;
        snapshot.AreaIndex = _areaIndex;
        snapshot.AmbientTimer = _ambientTimer;
        snapshot.EndTicks = _endTicks;
        snapshot.SurvivalWave = _survivalWave;
        snapshot.SurvivalRest = _survivalRest;
    }

    /// <summary>Restores a state saved by <see cref="SaveState"/>. Pending presentation events are left alone.</summary>
    public void LoadState(SessionSnapshot snapshot)
    {
        if (snapshot.World is null || !snapshot.HasData)
        {
            throw new InvalidOperationException("The snapshot is empty.");
        }

        World.CopyFrom(snapshot.World);
        Array.Copy(snapshot.Players, _players, MaxPlayers);
        CopyList(snapshot.Fighters, _fighters);
        CopyList(snapshot.SpawnQueue, _spawnQueue);

        _rng = snapshot.Rng;
        State = snapshot.State;
        Tick = snapshot.Tick;
        Stats = snapshot.Stats;
        CameraX = snapshot.CameraX;
        CameraLocked = snapshot.CameraLocked;
        BossEntity = snapshot.BossEntity;
        Music = snapshot.Music;
        MusicIntensity = snapshot.MusicIntensity;
        GoArrow = snapshot.GoArrow;
        CinematicTicks = snapshot.CinematicTicks;
        ChallengeResult = snapshot.ChallengeResult;

        _waveIndex = snapshot.WaveIndex;
        _waveActive = snapshot.WaveActive;
        _waveMaxAlive = snapshot.WaveMaxAlive;
        _spawnTimer = snapshot.SpawnTimer;
        _lockCenter = snapshot.LockCenter;
        _bossTriggered = snapshot.BossTriggered;
        _bossDefeated = snapshot.BossDefeated;
        _areaIndex = snapshot.AreaIndex;
        _ambientTimer = snapshot.AmbientTimer;
        _endTicks = snapshot.EndTicks;
        _survivalWave = snapshot.SurvivalWave;
        _survivalRest = snapshot.SurvivalRest;
    }

    private static void CopyList<T>(List<T> from, List<T> to)
    {
        to.Clear();
        for (int i = 0; i < from.Count; i++)
        {
            to.Add(from[i]);
        }
    }

    /// <summary>
    /// 64-bit hash over the complete simulation state (every pool's raw bytes plus every session field).
    /// Far stricter than <see cref="Checksum"/>; it walks all memory, so use it in tests and desync
    /// diagnostics rather than every tick.
    /// </summary>
    public ulong FullStateHash()
    {
        ulong h = World.ComputeStateHash();
        for (int i = 0; i < MaxPlayers; i++)
        {
            h = HashEntity(h, _players[i]);
        }

        h = Mix(h, (ulong)_fighters.Count);
        foreach (var e in _fighters)
        {
            h = HashEntity(h, e);
        }

        h = Mix(h, (ulong)_spawnQueue.Count);
        foreach (var k in _spawnQueue)
        {
            h = Mix(h, (ulong)k);
        }

        var rng = _rng;
        h = Mix(h, rng.NextUInt());
        h = Mix(h, rng.NextUInt());
        h = Mix(h, rng.NextUInt());
        h = Mix(h, rng.NextUInt());
        h = Mix(h, (ulong)State);
        h = Mix(h, (ulong)Tick);
        var st = Stats;
        h = Mix(h, (ulong)st.Ticks);
        h = Mix(h, (ulong)st.KnockOuts);
        h = Mix(h, (ulong)st.WeaponKnockOuts);
        h = Mix(h, (ulong)st.PropKnockOuts);
        h = Mix(h, (ulong)st.ThrowKnockOuts);
        h = Mix(h, (ulong)st.MaxCombo);
        h = Mix(h, (ulong)st.DamageTaken);
        h = Mix(h, (ulong)st.UltimatesUsed);
        h = Mix(h, (ulong)st.CountersLanded);
        h = Mix(h, (ulong)st.CoopMoves);
        h = Mix(h, (ulong)st.JurusUsed);
        h = Mix(h, (ulong)st.BossesDefeated);
        h = Mix(h, (ulong)st.WavesSurvived);
        h = Mix(h, (ulong)st.Hits);
        h = Mix(h, BitConverter.SingleToUInt32Bits(CameraX));
        h = Mix(h, CameraLocked ? 1UL : 0UL);
        h = HashEntity(h, BossEntity);
        h = Mix(h, (ulong)Music);
        h = Mix(h, BitConverter.SingleToUInt32Bits(MusicIntensity));
        h = Mix(h, GoArrow ? 1UL : 0UL);
        h = Mix(h, (ulong)CinematicTicks);
        h = Mix(h, ChallengeResult is null ? 2UL : ChallengeResult.Value ? 1UL : 0UL);

        h = Mix(h, (ulong)_waveIndex);
        h = Mix(h, _waveActive ? 1UL : 0UL);
        h = Mix(h, (ulong)_waveMaxAlive);
        h = Mix(h, (ulong)_spawnTimer);
        h = Mix(h, BitConverter.SingleToUInt32Bits(_lockCenter));
        h = Mix(h, _bossTriggered ? 1UL : 0UL);
        h = Mix(h, _bossDefeated ? 1UL : 0UL);
        h = Mix(h, (ulong)_areaIndex);
        h = Mix(h, (ulong)_ambientTimer);
        h = Mix(h, (ulong)_endTicks);
        h = Mix(h, (ulong)_survivalWave);
        h = Mix(h, (ulong)_survivalRest);
        return h;

        static ulong HashEntity(ulong h, Entity e) => Mix(Mix(h, (ulong)e.Id), (ulong)e.Version);
        static ulong Mix(ulong h, ulong v) => (h ^ v) * 1099511628211UL;
    }
}
