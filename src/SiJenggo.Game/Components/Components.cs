using System.Numerics;
using SiJenggo.Core.Ecs;
using SiJenggo.Game.Data;

namespace SiJenggo.Game.Components;

public enum TeamId : byte
{
    Players,
    Enemies,
    Neutral,
}

/// <summary>Position on the stage. X runs along the street, Z is the depth lane, Y is height.</summary>
public struct Transform
{
    public Vector3 Position;
    /// <summary>Last tick's position, for render interpolation.</summary>
    public Vector3 PreviousPosition;
    /// <summary>+1 facing right, -1 facing left.</summary>
    public float Facing;
}

public struct Motion
{
    public Vector3 Velocity;
    public bool Grounded;
    public float GravityScale;
    /// <summary>Ground friction while not driven by input (units/s²).</summary>
    public float Friction;
}

/// <summary>Upright cylinder used for pushing, hurtboxes and pickups.</summary>
public struct Body
{
    public float Radius;
    public float Height;
    /// <summary>Heavier bodies are pushed less and knocked back less.</summary>
    public float Weight;
    public bool Solid;
}

public struct Health
{
    public float Current;
    public float Max;
    public bool Dead;
    public readonly float Fraction => Max <= 0 ? 0 : Current / Max;
}

public struct Team
{
    public TeamId Id;
}

public enum FighterState : byte
{
    Idle,
    Walk,
    Airborne,
    Attack,
    HitStun,
    /// <summary>Launched into the air; can be juggled until it lands.</summary>
    Juggle,
    Knockdown,
    GetUp,
    Grabbing,
    Grabbed,
    Thrown,
    Dodge,
    Carrying,
    /// <summary>Player at zero health waiting for a partner to revive them.</summary>
    Downed,
    Dead,
    /// <summary>Frozen for a cinematic (boss intro, team finisher).</summary>
    Cinematic,
    Victory,
}

public struct Fighter
{
    public FighterState State;
    /// <summary>Ticks spent in <see cref="State"/>.</summary>
    public int StateTicks;
    public MoveId Move;
    public int MoveTick;
    public int ComboStep;
    /// <summary>Unique id of the current move instance, so one swing hits each target once.</summary>
    public int AttackSerial;
    public int HitsThisMove;
    public int StunTicks;
    public int InvulnTicks;
    public int HitStopTicks;
    public int JuggleCount;
    public Entity GrabTarget;
    public Entity GrabbedBy;
    public int GrabStrikes;
    public Entity CarriedProp;
    /// <summary>Remaining hits the fighter shrugs off without flinching (bosses, Debus).</summary>
    public float Armor;
    public FighterStats Stats;
    /// <summary>Who last launched this fighter; used for the co-op Air Launch Combo.</summary>
    public Entity LaunchedBy;
    public bool CounterArmed;
    /// <summary>Fractional move time; <see cref="MoveTick"/> is its integer part (attack speed scales it).</summary>
    public float MoveClock;
    public float ArmorMax;
    public bool AirAttackUsed;
}

/// <summary>Final, modifier-applied stats of a fighter (base data + level + skills + equipment).</summary>
public struct FighterStats
{
    public float WalkSpeed;
    public float DepthSpeed;
    public float JumpVelocity;
    public float Power;
    public float Defense;
    public float AttackSpeed;
    public float RageGain;
    public float RageDuration;
    public float ComboWindow;
    public float CounterWindow;
    public float KnockbackResist;

    public static FighterStats Default => new()
    {
        WalkSpeed = 4.2f,
        DepthSpeed = 2.8f,
        JumpVelocity = 8.5f,
        Power = 1f,
        Defense = 1f,
        AttackSpeed = 1f,
        RageGain = 1f,
        RageDuration = 1f,
        ComboWindow = 1f,
        CounterWindow = 1f,
        KnockbackResist = 0f,
    };
}

public struct Player
{
    public int Slot;
    public CharacterId Character;
    public InputState Input;
    public int Lives;
    public int Score;
    public int Combo;
    public int ComboTimer;
    public int BestCombo;
    public int KnockOuts;
    public int DownedTicks;
    public int ReviveProgress;
    public int RespawnTicks;
    public bool IsAi;
    public int Xp;
    public int DamageTaken;
    /// <summary>Attack buttons pressed while busy, replayed when the fighter can act (input buffer).</summary>
    public InputButtons Buffered;
    public int BufferTicks;
    public int AiTimer;
    public InputButtons AiButtons;
}

public struct Rage
{
    /// <summary>0..100.</summary>
    public float Meter;
    public bool Active;
    public int ActiveTicks;
    /// <summary>Semangat: regenerating resource spent on specials.</summary>
    public float Semangat;
}

public struct WeaponHolder
{
    public WeaponId Weapon;
    public int Durability;
}

public struct Enemy
{
    public EnemyKind Kind;
    public int ScoreValue;
    public int XpValue;
    public int DeathTicks;
    /// <summary>Player slot that landed the last hit (score and XP credit), -1 when none.</summary>
    public int LastHitterSlot;
    /// <summary>How the last hit was delivered, for weapon/prop/throw KO stats.</summary>
    public KoCause LastCause;
}

public enum KoCause : byte
{
    Strike,
    Weapon,
    Prop,
    Throw,
    Hazard,
}

/// <summary>
/// Remembers who hit this entity recently so one swing (or one rolling cart) hits once,
/// and multi-hit moves respect their rehit interval. Fixed size, no allocations.
/// </summary>
public struct HitMemory
{
    private long _k0, _k1, _k2, _k3, _k4, _k5;
    private int _t0, _t1, _t2, _t3, _t4, _t5;
    private int _next;

    public static long Key(int attackerId, int serial) => ((long)attackerId << 32) | (uint)serial;

    /// <summary>True if this key may hit now (never hit, or hit at least <paramref name="interval"/> ticks ago when interval &gt; 0).</summary>
    public readonly bool CanHit(long key, int tick, int interval)
    {
        int last = LastTick(key);
        return last < 0 || (interval > 0 && tick - last >= interval);
    }

    private readonly int LastTick(long key)
    {
        if (_k0 == key && _t0 != 0) return _t0;
        if (_k1 == key && _t1 != 0) return _t1;
        if (_k2 == key && _t2 != 0) return _t2;
        if (_k3 == key && _t3 != 0) return _t3;
        if (_k4 == key && _t4 != 0) return _t4;
        if (_k5 == key && _t5 != 0) return _t5;
        return -1;
    }

    public void Record(long key, int tick)
    {
        tick = Math.Max(1, tick);
        if (_k0 == key) { _t0 = tick; return; }
        if (_k1 == key) { _t1 = tick; return; }
        if (_k2 == key) { _t2 = tick; return; }
        if (_k3 == key) { _t3 = tick; return; }
        if (_k4 == key) { _t4 = tick; return; }
        if (_k5 == key) { _t5 = tick; return; }
        switch (_next)
        {
            case 0: _k0 = key; _t0 = tick; break;
            case 1: _k1 = key; _t1 = tick; break;
            case 2: _k2 = key; _t2 = tick; break;
            case 3: _k3 = key; _t3 = tick; break;
            case 4: _k4 = key; _t4 = tick; break;
            default: _k5 = key; _t5 = tick; break;
        }

        _next = (_next + 1) % 6;
    }
}

public enum AiState : byte
{
    Idle,
    Approach,
    Wait,
    Attack,
    Retreat,
    FetchWeapon,
    Ranged,
}

public struct AiBrain
{
    public AiState State;
    public int Timer;
    public Entity Target;
    public int AttackCooldown;
    public float Aggression;
    /// <summary>Preferred offset from the target while waiting for an attack token.</summary>
    public Vector2 SlotOffset;
    public bool HasToken;
    public int ThinkTicks;
    public MoveId PlannedMove;
    public Vector2 MoveDir;
}

public struct Boss
{
    public BossId Id;
    public int Phase;
    public int PhaseTicks;
    public int MechanicTimer;
    public int PatternIndex;
    public bool IntroDone;
    public int ShieldTicks;
}

public struct Prop
{
    public PropKind Kind;
    public float Durability;
    public bool Thrown;
    public Entity Thrower;
    public bool Rolling;
    public Entity Carrier;
}

public struct Pickup
{
    public PickupKind Kind;
    public WeaponId Weapon;
    public int Ticks;
    /// <summary>Dropped by an enemy or prop; expires after a while (placed pickups never do).</summary>
    public bool Dropped;
}

public enum ProjectileKind : byte
{
    Bottle,
    Harpoon,
    WindBlade,
    Shuriken,
    Gadget,
    Laser,
    Boulder,
    Peci,
    Weapon,
}

public struct Projectile
{
    public ProjectileKind Kind;
    public Entity Owner;
    public TeamId Team;
    public float Damage;
    public int Ticks;
    public int MaxTicks;
    public bool Knockdown;
}

public enum HazardKind : byte
{
    Lightning,
    FallingRock,
    LavaPool,
    FallingTree,
    SpearTrap,
    CrateDrop,
    Trap,
    WildBoar,
    LaserSweep,
    Shockwave,
}

/// <summary>A telegraphed area attack: warns for <see cref="WarnTicks"/>, then strikes.</summary>
public struct Hazard
{
    public HazardKind Kind;
    public TeamId Team;
    public float Radius;
    public float Damage;
    public int WarnTicks;
    public int ActiveTicks;
    public int Ticks;
    public bool Persistent;
    public int RehitTicks;
}

/// <summary>What the renderer should draw for this entity.</summary>
public struct Visual
{
    public VisualKind Kind;
    /// <summary>Character / enemy / boss / prop / pickup id cast to int.</summary>
    public int Variant;
    public float Scale;
}

public enum VisualKind : byte
{
    Character,
    Enemy,
    Boss,
    Prop,
    Pickup,
    Projectile,
    Hazard,
}
