using System.Numerics;
using SiJenggo.Core;
using SiJenggo.Core.Ecs;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;

namespace SiJenggo.Game;

public enum SessionState : byte
{
    Playing,
    /// <summary>Boss intro, team finisher: players are frozen while the camera does its thing.</summary>
    Cinematic,
    StageClear,
    GameOver,
}

public enum MusicState : byte
{
    Explore,
    Combat,
    Boss,
    Rage,
    Victory,
    Defeat,
}

/// <summary>
/// One running stage of the game: the ECS world plus every gameplay system, ticked at a fixed 60 Hz.
/// It is fully deterministic for a given <see cref="SessionConfig.Seed"/> and input stream, which is
/// what online co-op (lockstep) and replays rely on. Systems live in partial files under Systems/.
/// </summary>
public sealed partial class GameSession
{
    public const float Gravity = -26f;
    public const float Dt = FixedStepClock.Step;
    /// <summary>Half the visible stage width at the gameplay camera distance.</summary>
    public const float ViewHalfWidth = 7.8f;
    public const int MaxPlayers = 4;

    private DeterministicRandom _rng;
    private readonly Entity[] _players = new Entity[MaxPlayers];
    private readonly List<Entity> _fighters = new(64);
    private readonly List<Entity> _scratch = new(64);

    public GameSession(SessionConfig config, StagePlan stage)
    {
        Config = config;
        Stage = stage;
        _rng = new DeterministicRandom(config.Seed);
        World = new World(512);
        Array.Fill(_players, Entity.Null);
        PlayerCount = config.Players.Length;
        DifficultyScale = config.Difficulty switch
        {
            Difficulty.Santai => 0.75f,
            Difficulty.Jagoan => 1.3f,
            _ => 1f,
        } * stage.Scaling;

        for (int i = 0; i < PlayerCount; i++)
        {
            SpawnPlayer(i, config.Players[i], 3f + i * 1.2f, i % 2 == 0 ? -0.8f : 0.8f);
        }

        InitStage();
    }

    public SessionConfig Config { get; }
    public StagePlan Stage { get; }
    public World World { get; }
    public EventQueue<GameEvent> Events { get; } = new(256);
    public SessionState State { get; private set; } = SessionState.Playing;
    public int Tick { get; private set; }
    public int PlayerCount { get; }
    public float DifficultyScale { get; }
    public SessionStats Stats;

    /// <summary>Centre of the gameplay camera on the X axis.</summary>
    public float CameraX { get; private set; }
    public bool CameraLocked { get; private set; }
    public Entity BossEntity { get; private set; } = Entity.Null;
    public MusicState Music { get; private set; }
    /// <summary>0..1, drives dynamic music layering.</summary>
    public float MusicIntensity { get; private set; }
    public bool GoArrow { get; private set; }
    public int CinematicTicks { get; private set; }
    public bool? ChallengeResult { get; private set; }

    public Entity PlayerEntity(int slot) => (uint)slot < MaxPlayers ? _players[slot] : Entity.Null;

    /// <summary>Advances the simulation one tick. <paramref name="inputs"/> holds one frame per player slot.</summary>
    public void Step(ReadOnlySpan<InputFrame> inputs)
    {
        if (State is SessionState.StageClear or SessionState.GameOver)
        {
            AdvanceEndTimers();
            Tick++;
            return;
        }

        Tick++;
        Stats.Ticks++;

        // Snapshot positions for render interpolation.
        var transforms = World.Pool<Transform>();
        foreach (ref var t in transforms.Components)
        {
            t.PreviousPosition = t.Position;
        }

        CollectFighters();
        ReadInputs(inputs);
        UpdateCompanions();

        if (CinematicTicks > 0)
        {
            CinematicTicks--;
            if (CinematicTicks == 0)
            {
                EndCinematic();
            }
        }

        UpdatePlayers();
        UpdateEnemyAi();
        UpdateBoss();
        UpdateFighters();
        UpdateMotion();
        UpdateHits();
        UpdateProjectiles();
        UpdateHazards();
        UpdateProps();
        UpdatePickups();
        UpdateRage();
        UpdateCoop();
        UpdateDeaths();
        UpdateDirector();
        UpdateEndOfStage();
        UpdateCamera();
        UpdateMusic();
        UpdateChallenge();

        World.Flush();
    }

    /// <summary>Order-dependent hash of the gameplay state, compared between peers to detect desyncs.</summary>
    public uint Checksum()
    {
        uint h = 2166136261u;
        h = MathX.Mix(h, Tick);
        h = MathX.Mix(h, (int)_rng.StateHash);
        var healths = World.Pool<Health>();
        var ids = healths.EntityIds;
        var comps = healths.Components;
        for (int i = 0; i < ids.Length; i++)
        {
            h = MathX.Mix(h, ids[i]);
            h = MathX.Mix(h, comps[i].Current);
            ref var t = ref World.Pool<Transform>().Get(ids[i]);
            h = MathX.Mix(h, t.Position.X);
            h = MathX.Mix(h, t.Position.Y);
            h = MathX.Mix(h, t.Position.Z);
        }

        return h;
    }

    // ───────────────────────────── spawning ─────────────────────────────

    private Entity SpawnFighterBase(Vector3 position, TeamId team, float maxHealth, float radius, float height, float weight)
    {
        var e = World.Create();
        World.Add(e, new Transform { Position = position, PreviousPosition = position, Facing = team == TeamId.Players ? 1 : -1 });
        World.Add(e, new Motion { GravityScale = 1, Friction = 30, Grounded = true });
        World.Add(e, new Body { Radius = radius, Height = height, Weight = weight, Solid = true });
        World.Add(e, new Health { Current = maxHealth, Max = maxHealth });
        World.Add(e, new Team { Id = team });
        World.Add(e, new HitMemory());
        return e;
    }

    private void SpawnPlayer(int slot, PlayerLoadout loadout, float x, float z)
    {
        var def = Characters.Get(loadout.Character);
        var e = SpawnFighterBase(new Vector3(x, 0, z), TeamId.Players, loadout.MaxHealth, 0.38f, 1.8f, def.Weight);
        World.Add(e, new Fighter { Stats = loadout.Stats, State = FighterState.Idle, GrabTarget = Entity.Null, GrabbedBy = Entity.Null, CarriedProp = Entity.Null, LaunchedBy = Entity.Null });
        World.Add(e, new Player { Slot = slot, Character = loadout.Character, Lives = Config.Lives, IsAi = loadout.IsAi });
        World.Add(e, new Rage { Semangat = 60 });
        World.Add(e, new WeaponHolder());
        World.Add(e, new Visual { Kind = VisualKind.Character, Variant = (int)loadout.Character, Scale = 1 });
        _players[slot] = e;
    }

    public Entity SpawnEnemy(EnemyKind kind, Vector3 position)
    {
        var def = Enemies.Get(kind);
        float hp = def.Health * DifficultyScale;
        var e = SpawnFighterBase(position, TeamId.Enemies, hp, 0.38f * def.Scale, 1.8f * def.Scale, def.Weight);
        var stats = FighterStats.Default;
        stats.WalkSpeed = def.WalkSpeed;
        stats.DepthSpeed = def.WalkSpeed * 0.7f;
        stats.Power = def.Power * (0.85f + 0.15f * DifficultyScale);
        World.Add(e, new Fighter { Stats = stats, Armor = def.Armor, ArmorMax = def.Armor, State = FighterState.Idle, GrabTarget = Entity.Null, GrabbedBy = Entity.Null, CarriedProp = Entity.Null, LaunchedBy = Entity.Null });
        World.Add(e, new Enemy { Kind = kind, ScoreValue = def.Score, XpValue = def.Xp, LastHitterSlot = -1 });
        World.Add(e, new AiBrain { State = AiState.Approach, Target = Entity.Null, Aggression = def.Aggression * (0.8f + 0.2f * DifficultyScale), AttackCooldown = _rng.Range(30, 90), ThinkTicks = _rng.Range(0, 6) });
        World.Add(e, new WeaponHolder { Weapon = def.StartingWeapon, Durability = def.StartingWeapon == WeaponId.None ? 0 : Weapons.Get(def.StartingWeapon).Durability });
        World.Add(e, new Visual { Kind = VisualKind.Enemy, Variant = (int)kind, Scale = def.Scale });
        return e;
    }

    public Entity SpawnBoss(BossId id, Vector3 position)
    {
        var def = Bosses.Get(id);
        float hp = def.Health * DifficultyScale * (PlayerCount > 1 ? 1f + 0.35f * (PlayerCount - 1) : 1f);
        var e = SpawnFighterBase(position, TeamId.Enemies, hp, 0.5f * def.Scale, 1.8f * def.Scale, def.Weight);
        var stats = FighterStats.Default;
        stats.WalkSpeed = def.WalkSpeed;
        stats.DepthSpeed = def.WalkSpeed * 0.75f;
        stats.Power = def.Power * (0.85f + 0.15f * DifficultyScale);
        World.Add(e, new Fighter { Stats = stats, Armor = def.Armor, ArmorMax = def.Armor, State = FighterState.Cinematic, GrabTarget = Entity.Null, GrabbedBy = Entity.Null, CarriedProp = Entity.Null, LaunchedBy = Entity.Null });
        World.Add(e, new Enemy { ScoreValue = def.Score, XpValue = def.Xp, LastHitterSlot = -1 });
        World.Add(e, new AiBrain { State = AiState.Approach, Target = Entity.Null, Aggression = 1, AttackCooldown = 60 });
        World.Add(e, new Boss { Id = id, MechanicTimer = def.Phases[0].MechanicInterval });
        World.Add(e, new WeaponHolder());
        World.Add(e, new Visual { Kind = VisualKind.Boss, Variant = (int)id, Scale = def.Scale });
        BossEntity = e;
        return e;
    }

    public Entity SpawnProp(PropKind kind, Vector3 position)
    {
        var e = World.Create();
        var (radius, height, weight, durability) = PropStats(kind);
        World.Add(e, new Transform { Position = position, PreviousPosition = position, Facing = 1 });
        World.Add(e, new Motion { GravityScale = 1, Friction = 18, Grounded = true });
        World.Add(e, new Body { Radius = radius, Height = height, Weight = weight, Solid = true });
        World.Add(e, new Health { Current = durability, Max = durability });
        World.Add(e, new Team { Id = TeamId.Neutral });
        World.Add(e, new Prop { Kind = kind, Durability = durability, Thrower = Entity.Null });
        World.Add(e, new HitMemory());
        World.Add(e, new Visual { Kind = VisualKind.Prop, Variant = (int)kind, Scale = 1 });
        return e;
    }

    internal static (float Radius, float Height, float Weight, float Durability) PropStats(PropKind kind) => kind switch
    {
        PropKind.Kursi => (0.3f, 0.8f, 0.6f, 12),
        PropKind.Motor => (0.9f, 1.1f, 3.5f, 60),
        PropKind.Gerobak => (0.9f, 1.4f, 2.5f, 45),
        PropKind.TongSampah => (0.35f, 0.9f, 0.8f, 18),
        PropKind.Bambu => (0.3f, 0.5f, 0.7f, 20),
        PropKind.Pagar => (0.7f, 1.1f, 1.2f, 25),
        PropKind.PetiKayu => (0.45f, 0.8f, 1.2f, 16),
        PropKind.BatuBesar => (0.7f, 1f, 5f, 80),
        _ => (0.4f, 0.8f, 1f, 20),
    };

    /// <summary>Props light enough for anyone to lift; the rest need a heavyweight (Jenggo).</summary>
    internal static bool IsLiftable(PropKind kind, float liftStrength) => kind switch
    {
        PropKind.Motor or PropKind.BatuBesar or PropKind.Gerobak => liftStrength >= 1.35f,
        _ => true,
    };

    internal static bool IsBreakable(PropKind kind) => kind is PropKind.Kursi or PropKind.PetiKayu or PropKind.TongSampah or PropKind.Bambu or PropKind.Pagar;

    public Entity SpawnPickup(PickupKind kind, Vector3 position, WeaponId weapon = WeaponId.None)
    {
        var e = World.Create();
        World.Add(e, new Transform { Position = position, PreviousPosition = position, Facing = 1 });
        World.Add(e, new Motion { GravityScale = 1, Friction = 20, Grounded = position.Y <= 0 });
        World.Add(e, new Pickup { Kind = kind, Weapon = weapon });
        World.Add(e, new Visual { Kind = VisualKind.Pickup, Variant = kind == PickupKind.Weapon ? 100 + (int)weapon : (int)kind, Scale = 1 });
        return e;
    }

    private Entity SpawnProjectile(Entity owner, TeamId team, ProjectileKind kind, Vector3 position, Vector3 velocity, float damage, bool knockdown, int maxTicks = 120)
    {
        var e = World.Create();
        World.Add(e, new Transform { Position = position, PreviousPosition = position, Facing = MathF.Sign(velocity.X) == 0 ? 1 : MathF.Sign(velocity.X) });
        World.Add(e, new Motion { Velocity = velocity, GravityScale = kind is ProjectileKind.Bottle or ProjectileKind.Boulder ? 0.35f : 0, Friction = 0 });
        World.Add(e, new Projectile { Kind = kind, Owner = owner, Team = team, Damage = damage, MaxTicks = maxTicks, Knockdown = knockdown });
        World.Add(e, new HitMemory());
        World.Add(e, new Visual { Kind = VisualKind.Projectile, Variant = (int)kind, Scale = 1 });
        Emit(GameEventKind.Projectile, owner, e, position, param: (int)kind);
        return e;
    }

    public Entity SpawnHazard(HazardKind kind, TeamId team, Vector3 position, float radius, float damage, int warnTicks, int activeTicks, bool persistent = false, Vector3 velocity = default)
    {
        var e = World.Create();
        World.Add(e, new Transform { Position = position, PreviousPosition = position, Facing = velocity.X < 0 ? -1 : 1 });
        World.Add(e, new Motion { Velocity = velocity, GravityScale = 0, Friction = 0 });
        World.Add(e, new Hazard { Kind = kind, Team = team, Radius = radius, Damage = damage * (team == TeamId.Players ? 1 : DifficultyScale), WarnTicks = warnTicks, ActiveTicks = activeTicks, Persistent = persistent, RehitTicks = 30 });
        World.Add(e, new HitMemory());
        World.Add(e, new Visual { Kind = VisualKind.Hazard, Variant = (int)kind, Scale = radius });
        Emit(GameEventKind.HazardWarn, Entity.Null, e, position, radius, (int)kind);
        return e;
    }

    // ───────────────────────────── helpers ─────────────────────────────

    internal void Emit(GameEventKind kind, Entity source, Entity target, Vector3 position, float value = 0, int param = 0, HitFx fx = HitFx.Punch, MoveId move = default)
    {
        if (SuppressEvents)
        {
            return; // rollback re-simulation of frames the player has already seen (GameSessionSnapshot.cs)
        }

        Events.Push(new GameEvent { Kind = kind, Source = source, Target = target, Position = position, Value = value, Param = param, Fx = fx, Move = move });
    }

    private void CollectFighters()
    {
        _fighters.Clear();
        foreach (int id in World.Pool<Fighter>().EntityIds)
        {
            _fighters.Add(World.Handle(id));
        }

        // Stable order regardless of pool swaps, so lockstep peers resolve hits identically.
        _fighters.Sort(static (a, b) => a.Id.CompareTo(b.Id));
    }

    private void ReadInputs(ReadOnlySpan<InputFrame> inputs)
    {
        for (int slot = 0; slot < PlayerCount; slot++)
        {
            var e = _players[slot];
            if (!World.IsAlive(e))
            {
                continue;
            }

            ref var p = ref World.Get<Player>(e);
            if (!p.IsAi)
            {
                p.Input.Push(slot < inputs.Length ? inputs[slot].Buttons : InputButtons.None);
            }
        }
    }

    public IEnumerable<Entity> Players()
    {
        for (int i = 0; i < PlayerCount; i++)
        {
            if (World.IsAlive(_players[i]))
            {
                yield return _players[i];
            }
        }
    }

    /// <summary>Living, standing player nearest to <paramref name="from"/> (for AI targeting).</summary>
    private Entity NearestPlayer(Vector3 from)
    {
        Entity best = Entity.Null;
        float bestD = float.MaxValue;
        for (int i = 0; i < PlayerCount; i++)
        {
            var e = _players[i];
            if (!World.IsAlive(e))
            {
                continue;
            }

            ref var f = ref World.Get<Fighter>(e);
            if (f.State is FighterState.Downed or FighterState.Dead || World.Get<Health>(e).Dead)
            {
                continue;
            }

            float d = MathX.GroundDistance(World.Get<Transform>(e).Position, from);
            if (d < bestD)
            {
                bestD = d;
                best = e;
            }
        }

        return best;
    }

    private int AliveEnemyCount()
    {
        int n = 0;
        var enemies = World.Pool<Enemy>();
        foreach (int id in enemies.EntityIds)
        {
            if (!World.Pool<Health>().Get(id).Dead)
            {
                n++;
            }
        }

        return n;
    }

    private bool AnyPlayerActive()
    {
        for (int i = 0; i < PlayerCount; i++)
        {
            var e = _players[i];
            if (World.IsAlive(e) && World.Get<Fighter>(e).State != FighterState.Dead)
            {
                return true;
            }
        }

        return false;
    }

    private void StartCinematic(int ticks)
    {
        CinematicTicks = Math.Max(CinematicTicks, ticks);
        State = SessionState.Cinematic;
        foreach (var e in _fighters)
        {
            ref var f = ref World.Get<Fighter>(e);
            if (World.Has<Player>(e) && f.State is FighterState.Idle or FighterState.Walk)
            {
                f.State = FighterState.Cinematic;
                f.StateTicks = 0;
                World.Get<Motion>(e).Velocity = Vector3.Zero;
            }
        }
    }

    private void EndCinematic()
    {
        if (State == SessionState.Cinematic)
        {
            State = SessionState.Playing;
        }

        foreach (var e in _fighters)
        {
            ref var f = ref World.Get<Fighter>(e);
            if (f.State == FighterState.Cinematic)
            {
                SetState(ref f, FighterState.Idle);
            }
        }
    }
}
