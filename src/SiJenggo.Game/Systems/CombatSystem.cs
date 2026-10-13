using System.Numerics;
using SiJenggo.Core;
using SiJenggo.Core.Ecs;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;

namespace SiJenggo.Game;

/// <summary>Everything that resolves a hit, in one place so moves, projectiles, props and hazards behave alike.</summary>
internal struct HitSpec
{
    public Entity Attacker;
    public float Damage;
    public Vector2 Knockback;
    /// <summary>+1/-1: direction the target is pushed along X.</summary>
    public float Direction;
    public int HitStun;
    public int HitStop;
    public MoveFlags Flags;
    public HitFx Fx;
    public float RageGain;
    public KoCause Cause;
    public MoveId Move;
    public Vector3 Point;

    public readonly bool Has(MoveFlags f) => (Flags & f) != 0;
}

/// <summary>Fighter state machine, movement physics, hit detection and damage.</summary>
public sealed partial class GameSession
{
    internal static void SetState(ref Fighter f, FighterState state)
    {
        f.State = state;
        f.StateTicks = 0;
    }

    private void StartMove(Entity e, ref Fighter f, MoveId id)
    {
        if (id.IsNone)
        {
            return;
        }

        var def = id.Def;
        SetState(ref f, FighterState.Attack);
        f.Move = id;
        f.MoveClock = 0;
        f.MoveTick = 0;
        f.AttackSerial++;
        f.HitsThisMove = 0;
        f.CounterArmed = def.Has(MoveFlags.Counter);
        ref var m = ref World.Get<Motion>(e);
        if (m.Grounded && !def.Has(MoveFlags.AirOnly))
        {
            m.Velocity.X = 0;
            m.Velocity.Z = 0;
        }

        if (def.Has(MoveFlags.Special))
        {
            Emit(GameEventKind.Special, e, Entity.Null, World.Get<Transform>(e).Position, fx: def.Fx, move: id);
        }
    }

    /// <summary>Interrupts whatever the fighter was doing (drops props, releases grabs).</summary>
    private void CancelActions(Entity e, ref Fighter f)
    {
        if (World.IsAlive(f.CarriedProp))
        {
            ref var prop = ref World.Get<Prop>(f.CarriedProp);
            prop.Carrier = Entity.Null;
            ref var pm = ref World.Get<Motion>(f.CarriedProp);
            pm.Grounded = false;
            pm.Velocity = new Vector3(0, 1, 0);
        }

        f.CarriedProp = Entity.Null;
        if (World.IsAlive(f.GrabTarget))
        {
            ref var tf = ref World.Get<Fighter>(f.GrabTarget);
            if (tf.State == FighterState.Grabbed && tf.GrabbedBy == e)
            {
                SetState(ref tf, FighterState.HitStun);
                tf.StunTicks = 8;
                tf.GrabbedBy = Entity.Null;
            }
        }

        f.GrabTarget = Entity.Null;
    }

    private float BossSpeed(Entity e)
    {
        if (!World.Has<Boss>(e))
        {
            return 1f;
        }

        ref var b = ref World.Get<Boss>(e);
        return Bosses.Get(b.Id).Phases[b.Phase].SpeedMultiplier;
    }

    private void UpdateFighters()
    {
        foreach (var e in _fighters)
        {
            ref var f = ref World.Get<Fighter>(e);
            if (f.HitStopTicks > 0)
            {
                f.HitStopTicks--;
                continue;
            }

            f.StateTicks++;
            if (f.InvulnTicks > 0)
            {
                f.InvulnTicks--;
            }

            ref var t = ref World.Get<Transform>(e);
            ref var m = ref World.Get<Motion>(e);

            // Armoured enemies slowly regain their poise.
            if (f.ArmorMax > 0 && f.Armor < f.ArmorMax && Tick % 120 == 0)
            {
                f.Armor = MathF.Min(f.ArmorMax, f.Armor + 1);
            }

            switch (f.State)
            {
                case FighterState.Attack:
                    AdvanceMove(e, ref f, ref t, ref m);
                    break;

                case FighterState.HitStun:
                    if (--f.StunTicks <= 0)
                    {
                        SetState(ref f, m.Grounded ? FighterState.Idle : FighterState.Juggle);
                    }

                    break;

                case FighterState.Knockdown:
                    if (!World.Get<Health>(e).Dead && f.StateTicks >= 42)
                    {
                        SetState(ref f, FighterState.GetUp);
                        f.InvulnTicks = 30;
                    }

                    break;

                case FighterState.GetUp:
                    if (f.StateTicks >= 22)
                    {
                        SetState(ref f, FighterState.Idle);
                        f.InvulnTicks = Math.Max(f.InvulnTicks, 12);
                    }

                    break;

                case FighterState.Dodge:
                    if (f.StateTicks >= 20)
                    {
                        SetState(ref f, FighterState.Idle);
                    }

                    break;

                case FighterState.Grabbing:
                    HoldGrab(e, ref f, ref t);
                    break;

                case FighterState.Grabbed:
                    if (!World.IsAlive(f.GrabbedBy) || World.Get<Fighter>(f.GrabbedBy).GrabTarget != e)
                    {
                        f.GrabbedBy = Entity.Null;
                        SetState(ref f, FighterState.Idle);
                    }

                    break;

                case FighterState.Carrying:
                    if (!World.IsAlive(f.CarriedProp))
                    {
                        f.CarriedProp = Entity.Null;
                        SetState(ref f, FighterState.Idle);
                    }

                    break;
            }
        }
    }

    private void HoldGrab(Entity e, ref Fighter f, ref Transform t)
    {
        if (!World.IsAlive(f.GrabTarget) || World.Get<Fighter>(f.GrabTarget).State != FighterState.Grabbed)
        {
            f.GrabTarget = Entity.Null;
            SetState(ref f, FighterState.Idle);
            return;
        }

        // The victim wriggles free after a while.
        if (f.StateTicks > 150)
        {
            CancelActions(e, ref f);
            SetState(ref f, FighterState.Idle);
            return;
        }

        PinGrabbed(e, f.GrabTarget, t);
    }

    private void PinGrabbed(Entity grabber, Entity target, in Transform t)
    {
        ref var tt = ref World.Get<Transform>(target);
        float reach = World.Get<Body>(grabber).Radius + World.Get<Body>(target).Radius + 0.05f;
        tt.Position = new Vector3(t.Position.X + t.Facing * reach, t.Position.Y, t.Position.Z);
        tt.Facing = -t.Facing;
        World.Get<Motion>(target).Velocity = Vector3.Zero;
    }

    private void AdvanceMove(Entity e, ref Fighter f, ref Transform t, ref Motion m)
    {
        var def = f.Move.Def;
        int previous = f.MoveTick;
        float speed = f.Stats.AttackSpeed * BossSpeed(e);
        if (World.Has<Rage>(e) && World.Get<Rage>(e).Active)
        {
            speed *= 1.1f;
        }

        f.MoveClock += speed;
        f.MoveTick = (int)f.MoveClock;

        bool activeEnd = f.MoveTick < def.Startup + def.Active;
        if (def.Lunge != 0 && activeEnd)
        {
            float l = f.MoveTick < def.Startup ? def.Lunge * 0.35f : def.Lunge;
            m.Velocity.X = t.Facing * l;
        }

        if (def.InvulnTo >= 0 && f.MoveTick >= def.InvulnFrom && f.MoveTick <= def.InvulnTo)
        {
            f.InvulnTicks = Math.Max(f.InvulnTicks, 2);
        }

        if (World.IsAlive(f.GrabTarget) && World.Get<Fighter>(f.GrabTarget).State == FighterState.Grabbed)
        {
            PinGrabbed(e, f.GrabTarget, t);
        }

        if (previous < def.Startup && f.MoveTick >= def.Startup)
        {
            OnMoveActive(e, ref f, ref t, ref m, def);
        }

        if (f.MoveTick >= def.TotalTicks)
        {
            f.Move = MoveId.None;
            if (World.IsAlive(f.GrabTarget) && World.Get<Fighter>(f.GrabTarget).State == FighterState.Grabbed)
            {
                SetState(ref f, FighterState.Grabbing);
                f.StateTicks = Math.Min(f.StateTicks, 100);
            }
            else
            {
                f.GrabTarget = Entity.Null;
                SetState(ref f, m.Grounded ? FighterState.Idle : FighterState.Airborne);
            }
        }
    }

    /// <summary>First active tick: projectiles, hazards, direct grab hits, self buffs.</summary>
    private void OnMoveActive(Entity e, ref Fighter f, ref Transform t, ref Motion m, MoveDef def)
    {
        var team = World.Get<Team>(e).Id;
        if (def.Lift != 0)
        {
            m.Velocity.Y = def.Lift;
            m.Grounded = false;
        }

        if (def.HitboxHalfSize != Vector3.Zero || def.Has(MoveFlags.Throw))
        {
            Emit(GameEventKind.Whiff, e, Entity.Null, t.Position + new Vector3(t.Facing * 0.8f, 1.2f, 0), fx: def.Fx, move: def.Id);
        }

        if (def.Has(MoveFlags.Projectile))
        {
            var vel = new Vector3(t.Facing * def.ProjectileSpeed, def.Shot switch { ProjectileKind.Bottle => 4f, ProjectileKind.Boulder => 5f, _ => 0 }, 0);
            SpawnProjectile(e, team, def.Shot, t.Position + new Vector3(t.Facing * 0.7f, 1.3f, 0), vel, def.Damage * f.Stats.Power, def.Has(MoveFlags.Knockdown), def.Shot == ProjectileKind.Peci ? 70 : 110);
        }

        if (def.Has(MoveFlags.SpawnsHazard))
        {
            SpawnMoveHazards(e, team, t, def, f.Stats.Power);
        }

        if (def.Id == Moves.JurusDebus)
        {
            f.Armor = 8;
            f.ArmorMax = Math.Max(f.ArmorMax, 0);
            World.Get<Health>(e).Current = MathF.Min(World.Get<Health>(e).Max, World.Get<Health>(e).Current + 10);
            Emit(GameEventKind.ArmorUp, e, Entity.Null, t.Position);
        }

        // Grab strikes and throws act directly on the held victim.
        if (World.IsAlive(f.GrabTarget) && (def.Has(MoveFlags.Throw) || def.Knockback == Vector2.Zero) && World.Get<Fighter>(f.GrabTarget).State == FighterState.Grabbed)
        {
            var target = f.GrabTarget;
            ref var tf = ref World.Get<Fighter>(target);
            var spec = SpecFromMove(e, def, World.Get<Transform>(target).Position + new Vector3(0, 1.1f, 0), t.Facing);
            if (def.Has(MoveFlags.Throw))
            {
                tf.GrabbedBy = Entity.Null;
                f.GrabTarget = Entity.Null;
                SetState(ref tf, FighterState.Idle);
                spec.Cause = KoCause.Throw;
                ApplyHit(target, ref spec);
                if (World.Get<Fighter>(target).State == FighterState.Juggle)
                {
                    SetState(ref World.Get<Fighter>(target), FighterState.Thrown);
                }

                Emit(GameEventKind.Throw, e, target, spec.Point);
            }
            else
            {
                ApplyHit(target, ref spec);
                if (!World.Get<Health>(target).Dead)
                {
                    SetState(ref tf, FighterState.Grabbed);
                    tf.GrabbedBy = e;
                }
            }

            f.HitsThisMove++;
        }
    }

    private void SpawnMoveHazards(Entity caster, TeamId team, in Transform t, MoveDef def, float power)
    {
        switch (def.Hazard)
        {
            case HazardKind.Lightning:
                foreach (var p in Players())
                {
                    var pp = World.Get<Transform>(p).Position;
                    SpawnHazard(HazardKind.Lightning, team, new Vector3(pp.X, 0, pp.Z), def.HazardRadius, 14, 50, 8);
                    SpawnHazard(HazardKind.Lightning, team, new Vector3(pp.X + _rng.Range(-3f, 3f), 0, Math.Clamp(pp.Z + _rng.Range(-1.5f, 1.5f), -Chapters.LaneHalfDepth, Chapters.LaneHalfDepth)), def.HazardRadius, 14, 62, 8);
                }

                break;
            case HazardKind.FallingTree:
                foreach (var p in Players())
                {
                    var pp = World.Get<Transform>(p).Position;
                    SpawnHazard(HazardKind.FallingTree, team, new Vector3(pp.X, 0, 0), def.HazardRadius, 20, 60, 10);
                }

                break;
            case HazardKind.LaserSweep:
            {
                var target = NearestPlayer(t.Position);
                var tp = World.IsAlive(target) ? World.Get<Transform>(target).Position : t.Position;
                for (int i = 0; i < 4; i++)
                {
                    SpawnHazard(HazardKind.LaserSweep, team, new Vector3(tp.X + (i - 1.5f) * 2.2f, 0, tp.Z), def.HazardRadius, 12, 40 + i * 10, 8);
                }

                break;
            }

            case HazardKind.LavaPool:
                SpawnHazard(HazardKind.LavaPool, team, t.Position + new Vector3(t.Facing * 1.6f, 0, 0), def.HazardRadius, 6, 40, 170, persistent: true);
                break;
            case HazardKind.Trap:
                SpawnHazard(HazardKind.Trap, team, t.Position + new Vector3(t.Facing * 1.1f, 0, 0), def.HazardRadius, 12 * power, 30, 1200, persistent: true);
                break;
            case HazardKind.Shockwave:
                SpawnHazard(HazardKind.Shockwave, team, new Vector3(t.Position.X, 0, t.Position.Z), def.HazardRadius, (def.Damage > 0 ? def.Damage : 14) * power, 6, 8);
                break;
            default:
                SpawnHazard(def.Hazard, team, t.Position + new Vector3(t.Facing * 2f, 0, 0), def.HazardRadius, 12, 45, 8);
                break;
        }
    }

    // ───────────────────────────── motion ─────────────────────────────

    private void UpdateMotion()
    {
        var motions = World.Pool<Motion>();
        var ids = motions.EntityIds;
        var fighters = World.Pool<Fighter>();
        var players = World.Pool<Player>();
        var props = World.Pool<Prop>();
        float length = Stage.Chapter.Length;
        float camMin = CameraX - ViewHalfWidth + 0.5f;
        float camMax = CameraX + ViewHalfWidth - 0.5f;

        for (int i = 0; i < ids.Length; i++)
        {
            int id = ids[i];
            ref var m = ref motions.Components[i];
            bool isFighter = fighters.Has(id);
            if (isFighter)
            {
                ref var f = ref fighters.Get(id);
                if (f.HitStopTicks > 0 || f.State == FighterState.Grabbed)
                {
                    continue;
                }
            }

            if (props.Has(id) && World.IsAlive(props.Get(id).Carrier))
            {
                continue;
            }

            ref var t = ref World.Pool<Transform>().Get(id);
            bool wasGrounded = m.Grounded;

            if (!m.Grounded || m.Velocity.Y > 0)
            {
                m.Velocity.Y += Gravity * m.GravityScale * Dt;
            }

            t.Position += m.Velocity * Dt;

            if (t.Position.Y <= 0 && m.GravityScale > 0)
            {
                float impact = m.Velocity.Y;
                t.Position.Y = 0;
                m.Velocity.Y = 0;
                m.Grounded = true;
                if (!wasGrounded)
                {
                    OnLanded(World.Handle(id), impact);
                }
            }

            if (m.Grounded && m.Friction > 0)
            {
                bool driven = isFighter && fighters.Get(id).State is FighterState.Walk or FighterState.Dodge or FighterState.Carrying
                    || (isFighter && fighters.Get(id).State == FighterState.Attack && fighters.Get(id).Move.Def.Lunge != 0 && fighters.Get(id).MoveTick < fighters.Get(id).Move.Def.Startup + fighters.Get(id).Move.Def.Active);
                if (!driven)
                {
                    m.Velocity.X = MathX.MoveTowards(m.Velocity.X, 0, m.Friction * Dt);
                    m.Velocity.Z = MathX.MoveTowards(m.Velocity.Z, 0, m.Friction * Dt);
                }
            }

            // Keep everything on the street.
            if (MathF.Abs(t.Position.Z) > Chapters.LaneHalfDepth)
            {
                t.Position.Z = Math.Clamp(t.Position.Z, -Chapters.LaneHalfDepth, Chapters.LaneHalfDepth);
                m.Velocity.Z = 0;
            }

            if (players.Has(id))
            {
                t.Position.X = Math.Clamp(t.Position.X, camMin, camMax);
            }
            else if (!World.Pool<Projectile>().Has(id) && !World.Pool<Hazard>().Has(id))
            {
                t.Position.X = Math.Clamp(t.Position.X, -2f, length + 2f);
            }
        }

        SeparateBodies();
    }

    /// <summary>Soft push-apart so fighters do not stack; heavier bodies move less.</summary>
    private void SeparateBodies()
    {
        var transforms = World.Pool<Transform>();
        var bodies = World.Pool<Body>();
        for (int i = 0; i < _fighters.Count; i++)
        {
            var a = _fighters[i];
            if (!World.IsAlive(a) || !Standing(a))
            {
                continue;
            }

            for (int j = i + 1; j < _fighters.Count; j++)
            {
                var b = _fighters[j];
                if (!World.IsAlive(b) || !Standing(b))
                {
                    continue;
                }

                ref var ta = ref transforms.Get(a.Id);
                ref var tb = ref transforms.Get(b.Id);
                var ba = bodies.Get(a.Id);
                var bb = bodies.Get(b.Id);
                float dx = tb.Position.X - ta.Position.X;
                float dz = tb.Position.Z - ta.Position.Z;
                float minD = (ba.Radius + bb.Radius) * 0.9f;
                float d2 = dx * dx + dz * dz;
                if (d2 >= minD * minD || d2 < 1e-8f)
                {
                    continue;
                }

                float d = MathF.Sqrt(d2);
                float push = (minD - d) * 0.5f;
                float wa = bb.Weight / (ba.Weight + bb.Weight);
                var dir = new Vector3(dx / d, 0, dz / d);
                ta.Position -= dir * push * wa * 2f;
                tb.Position += dir * push * (1 - wa) * 2f;
            }
        }
    }

    private bool Standing(Entity e)
    {
        var s = World.Get<Fighter>(e).State;
        return s is FighterState.Idle or FighterState.Walk or FighterState.Attack or FighterState.HitStun or FighterState.Carrying or FighterState.Grabbing
            && World.Get<Transform>(e).Position.Y < 0.3f;
    }

    private void OnLanded(Entity e, float impact)
    {
        var pos = World.Get<Transform>(e).Position;
        if (World.Has<Fighter>(e))
        {
            ref var f = ref World.Get<Fighter>(e);
            f.AirAttackUsed = false;
            switch (f.State)
            {
                case FighterState.Juggle or FighterState.Thrown:
                    f.JuggleCount = 0;
                    f.LaunchedBy = Entity.Null;
                    Emit(GameEventKind.Land, e, Entity.Null, pos, MathF.Abs(impact), param: 1);
                    if (World.Has<Player>(e) && World.Get<Health>(e).Current <= 0)
                    {
                        OnPlayerFell(e);
                    }
                    else if (World.Get<Health>(e).Dead)
                    {
                        SetState(ref f, FighterState.Dead);
                    }
                    else
                    {
                        SetState(ref f, FighterState.Knockdown);
                        f.InvulnTicks = 42;
                    }

                    break;
                case FighterState.Airborne:
                    SetState(ref f, FighterState.Idle);
                    Emit(GameEventKind.Land, e, Entity.Null, pos, MathF.Abs(impact));
                    break;
                case FighterState.Attack when f.Move.Def.Has(MoveFlags.AirOnly):
                    // Air moves end on landing; ground pounds keep their recovery.
                    if (f.Move.Def.Pose != MovePose.GroundPound)
                    {
                        f.Move = MoveId.None;
                        SetState(ref f, FighterState.Idle);
                    }

                    Emit(GameEventKind.Land, e, Entity.Null, pos, MathF.Abs(impact));
                    break;
            }

            return;
        }

        if (World.Has<Prop>(e))
        {
            ref var prop = ref World.Get<Prop>(e);
            if (prop.Thrown)
            {
                prop.Thrown = false;
                if (IsBreakable(prop.Kind))
                {
                    BreakProp(e);
                }
                else
                {
                    Emit(GameEventKind.Land, e, Entity.Null, pos, MathF.Abs(impact), param: 2);
                }
            }

            return;
        }

        if (World.Has<Projectile>(e))
        {
            Emit(GameEventKind.Hit, e, Entity.Null, pos, 0, fx: HitFx.Blunt);
            World.Destroy(e);
        }
    }

    // ───────────────────────────── hits ─────────────────────────────

    private HitSpec SpecFromMove(Entity attacker, MoveDef def, Vector3 point, float direction)
    {
        float power = World.Get<Fighter>(attacker).Stats.Power;
        if (World.Has<Rage>(attacker) && World.Get<Rage>(attacker).Active)
        {
            power *= 1.35f;
        }

        var weapon = World.Has<WeaponHolder>(attacker) ? World.Get<WeaponHolder>(attacker).Weapon : WeaponId.None;
        return new HitSpec
        {
            Attacker = attacker,
            Damage = def.Damage * power,
            Knockback = def.Knockback,
            Direction = direction,
            HitStun = def.HitStun,
            HitStop = def.HitStop,
            Flags = def.Flags,
            Fx = def.Fx,
            RageGain = def.RageGain,
            Cause = weapon != WeaponId.None && Array.IndexOf(Weapons.Get(weapon).Chain, def.Id) >= 0 || (weapon != WeaponId.None && Weapons.Get(weapon).Heavy == def.Id) ? KoCause.Weapon : KoCause.Strike,
            Move = def.Id,
            Point = point,
        };
    }

    private void UpdateHits()
    {
        var transforms = World.Pool<Transform>();
        foreach (var attacker in _fighters)
        {
            if (!World.IsAlive(attacker))
            {
                continue;
            }

            ref var f = ref World.Get<Fighter>(attacker);
            if (f.State != FighterState.Attack || f.HitStopTicks > 0)
            {
                continue;
            }

            var def = f.Move.Def;
            // Counters are a stance: they only strike through TriggerCounter.
            if (def.HitboxHalfSize == Vector3.Zero || def.Has(MoveFlags.Throw) || def.Has(MoveFlags.Counter) || f.MoveTick < def.Startup || f.MoveTick >= def.Startup + def.Active)
            {
                continue;
            }

            // Grab strikes are applied directly in OnMoveActive.
            if (World.IsAlive(f.GrabTarget) && def.Knockback == Vector2.Zero)
            {
                continue;
            }

            ref var t = ref transforms.Get(attacker.Id);
            var team = World.Get<Team>(attacker).Id;
            float scale = World.Has<Visual>(attacker) ? World.Get<Visual>(attacker).Scale : 1f;
            var off = def.HitboxOffset * scale;
            var centre = t.Position + new Vector3(off.X * t.Facing, off.Y, off.Z);
            var half = def.HitboxHalfSize * scale;
            long key = HitMemory.Key(attacker.Id, f.AttackSerial);

            foreach (var target in _fighters)
            {
                if (target == attacker || !World.IsAlive(target) || World.Get<Team>(target).Id == team)
                {
                    continue;
                }

                if (!CanBeHit(target, def.Has(MoveFlags.HitsDowned)))
                {
                    continue;
                }

                if (!Overlaps(centre, half, target))
                {
                    continue;
                }

                ref var memory = ref World.Get<HitMemory>(target);
                if (!memory.CanHit(key, Tick, def.RehitInterval))
                {
                    continue;
                }

                memory.Record(key, Tick);
                var tp = transforms.Get(target.Id).Position;
                float dir = def.Has(MoveFlags.Area) ? (tp.X >= t.Position.X ? 1 : -1) : t.Facing;

                ref var tf = ref World.Get<Fighter>(target);
                if (tf.State == FighterState.Attack && tf.CounterArmed && tf.MoveTick < tf.Move.Def.Startup + (int)(tf.Move.Def.Active * tf.Stats.CounterWindow) && !def.Has(MoveFlags.Ultimate))
                {
                    TriggerCounter(target, ref tf, attacker);
                    continue;
                }

                var spec = SpecFromMove(attacker, def, new Vector3(MathX.Lerp(centre.X, tp.X, 0.5f), MathF.Min(centre.Y, tp.Y + 1.4f), tp.Z), dir);
                ApplyHit(target, ref spec);
                f.HitsThisMove++;
            }

            // Props.
            var props = World.Pool<Prop>();
            var propIds = props.EntityIds;
            for (int i = propIds.Length - 1; i >= 0; i--)
            {
                var prop = World.Handle(propIds[i]);
                if (World.IsAlive(props.Get(prop.Id).Carrier) || !Overlaps(centre, half, prop))
                {
                    continue;
                }

                ref var memory = ref World.Get<HitMemory>(prop);
                if (!memory.CanHit(key, Tick, def.RehitInterval))
                {
                    continue;
                }

                memory.Record(key, Tick);
                HitProp(prop, attacker, def, t.Facing);
            }
        }
    }

    private bool CanBeHit(Entity target, bool hitsDowned)
    {
        ref var tf = ref World.Get<Fighter>(target);
        if (tf.InvulnTicks > 0 || World.Get<Health>(target).Dead && tf.State != FighterState.Juggle)
        {
            return false;
        }

        return tf.State switch
        {
            FighterState.Dead or FighterState.Downed or FighterState.GetUp or FighterState.Cinematic or FighterState.Dodge or FighterState.Victory => false,
            FighterState.Knockdown => hitsDowned,
            _ => true,
        };
    }

    private bool Overlaps(Vector3 centre, Vector3 half, Entity target)
    {
        var p = World.Get<Transform>(target).Position;
        var b = World.Get<Body>(target);
        float height = b.Height;
        if (World.Has<Fighter>(target) && World.Get<Fighter>(target).State is FighterState.Knockdown)
        {
            height = 0.5f;
        }

        return MathF.Abs(p.X - centre.X) <= half.X + b.Radius
            && MathF.Abs(p.Z - centre.Z) <= half.Z + b.Radius * 0.6f
            && centre.Y + half.Y >= p.Y && centre.Y - half.Y <= p.Y + height;
    }

    private void TriggerCounter(Entity defender, ref Fighter df, Entity attacker)
    {
        var def = df.Move.Def;
        df.CounterArmed = false;
        df.MoveClock = df.MoveTick = def.Startup + def.Active;
        df.InvulnTicks = 20;
        ref var dt = ref World.Get<Transform>(defender);
        var ap = World.Get<Transform>(attacker).Position;
        dt.Facing = ap.X >= dt.Position.X ? 1 : -1;
        var spec = SpecFromMove(defender, def, ap + new Vector3(0, 1.1f, 0), dt.Facing);
        spec.Damage *= 1.25f;
        ApplyHit(attacker, ref spec);
        Stats.CountersLanded++;
        Emit(GameEventKind.Counter, defender, attacker, spec.Point);
    }

    /// <summary>The one place damage happens. Everything (moves, projectiles, props, hazards) ends up here.</summary>
    internal void ApplyHit(Entity target, ref HitSpec hit)
    {
        ref var tf = ref World.Get<Fighter>(target);
        ref var health = ref World.Get<Health>(target);
        ref var m = ref World.Get<Motion>(target);
        var body = World.Get<Body>(target);
        var tp = World.Get<Transform>(target).Position;
        bool attackerIsPlayer = World.IsAlive(hit.Attacker) && World.Has<Player>(hit.Attacker);

        if (World.Has<Boss>(target) && World.Get<Boss>(target).ShieldTicks > 0)
        {
            Emit(GameEventKind.Blocked, hit.Attacker, target, hit.Point, fx: HitFx.Shockwave);
            return;
        }

        // Co-op: hitting an enemy your partner launched.
        float coopBonus = 1f;
        if (attackerIsPlayer && tf.State == FighterState.Juggle && World.IsAlive(tf.LaunchedBy) && tf.LaunchedBy != hit.Attacker && World.Has<Player>(tf.LaunchedBy))
        {
            coopBonus = 1.3f;
            Stats.CoopMoves++;
            Emit(GameEventKind.CoopMove, hit.Attacker, target, hit.Point, param: (int)CoopMove.AirLaunchCombo);
        }

        float damage = hit.Damage * coopBonus / Math.Max(0.2f, tf.Stats.Defense);
        health.Current -= damage;
        bool killed = health.Current <= 0;

        if (World.Has<Player>(target))
        {
            ref var victim = ref World.Get<Player>(target);
            victim.DamageTaken += (int)MathF.Ceiling(damage);
            Stats.DamageTaken += (int)MathF.Ceiling(damage);
            if (World.Has<Rage>(target))
            {
                ref var vr = ref World.Get<Rage>(target);
                if (!vr.Active)
                {
                    vr.Meter = MathF.Min(100, vr.Meter + damage * 0.6f * tf.Stats.RageGain);
                }
            }

            if (killed)
            {
                health.Current = 0;
            }
        }
        else if (killed)
        {
            health.Current = 0;
            health.Dead = true;
        }

        if (attackerIsPlayer)
        {
            ref var ap = ref World.Get<Player>(hit.Attacker);
            ap.Combo++;
            ap.ComboTimer = 90;
            ap.Score += (int)(damage * 10);
            ap.BestCombo = Math.Max(ap.BestCombo, ap.Combo);
            Stats.MaxCombo = Math.Max(Stats.MaxCombo, ap.Combo);
            Stats.Hits++;
            if (ap.Combo is 10 or 20 or 30 or 50 or 75 or 100)
            {
                Emit(GameEventKind.ComboMilestone, hit.Attacker, target, hit.Point, ap.Combo, ap.Slot);
            }

            ref var ar = ref World.Get<Rage>(hit.Attacker);
            if (!ar.Active)
            {
                ar.Meter = MathF.Min(100, ar.Meter + hit.RageGain * World.Get<Fighter>(hit.Attacker).Stats.RageGain);
            }

            if (World.Has<Enemy>(target))
            {
                ref var en = ref World.Get<Enemy>(target);
                en.LastHitterSlot = ap.Slot;
                en.LastCause = hit.Cause;
            }

            // Weapons wear out.
            if (hit.Cause == KoCause.Weapon)
            {
                ref var w = ref World.Get<WeaponHolder>(hit.Attacker);
                if (w.Weapon != WeaponId.None && --w.Durability <= 0)
                {
                    Emit(GameEventKind.WeaponBroken, hit.Attacker, Entity.Null, hit.Point, param: (int)w.Weapon);
                    w.Weapon = WeaponId.None;
                }
            }
        }
        else if (World.Has<Enemy>(target) && hit.Cause == KoCause.Hazard)
        {
            World.Get<Enemy>(target).LastCause = KoCause.Hazard;
        }

        if (World.IsAlive(hit.Attacker) && World.Has<Fighter>(hit.Attacker))
        {
            World.Get<Fighter>(hit.Attacker).HitStopTicks = Math.Max(World.Get<Fighter>(hit.Attacker).HitStopTicks, hit.HitStop);
        }

        tf.HitStopTicks = hit.HitStop;
        Emit(GameEventKind.Hit, hit.Attacker, target, hit.Point, damage, killed ? 1 : 0, hit.Fx, hit.Move);

        if (World.Has<AiBrain>(target))
        {
            World.Get<AiBrain>(target).HasToken = false;
        }

        // Reaction.
        bool armored = !killed && (tf.State == FighterState.Attack && tf.Move.Def.Has(MoveFlags.SuperArmor) || tf.State == FighterState.Cinematic);
        if (!armored && !killed && tf.Armor >= 1 && !hit.Has(MoveFlags.Ultimate))
        {
            tf.Armor -= hit.Has(MoveFlags.Knockdown) || hit.Has(MoveFlags.Launch) ? 1.5f : 1f;
            armored = tf.Armor >= 0;
            if (tf.Armor < 0)
            {
                tf.Armor = 0;
            }
        }

        if (armored)
        {
            return;
        }

        bool grabbedStrike = tf.State == FighterState.Grabbed && hit.Knockback == Vector2.Zero;
        if (grabbedStrike)
        {
            return;
        }

        CancelActions(target, ref tf);
        float resist = 1f - Math.Clamp(tf.Stats.KnockbackResist, 0, 0.8f);
        float weight = MathF.Sqrt(Math.Max(0.3f, body.Weight));
        float kx = hit.Knockback.X * hit.Direction * resist / weight;
        bool airborne = tp.Y > 0.05f || tf.State is FighterState.Juggle or FighterState.Thrown;

        if (killed || hit.Has(MoveFlags.Launch) || hit.Has(MoveFlags.Knockdown) || airborne)
        {
            float decay = MathF.Max(0.35f, 1f - tf.JuggleCount * 0.15f);
            float ky = hit.Knockback.Y > 0 ? hit.Knockback.Y : (airborne ? 4f : 2.5f);
            if (killed)
            {
                ky = MathF.Max(ky, 5.5f);
                kx = MathF.Sign(hit.Direction) * MathF.Max(MathF.Abs(kx), 5f);
            }

            m.Velocity = new Vector3(kx, ky * decay * resist, 0);
            m.Grounded = false;
            tf.JuggleCount++;
            if (attackerIsPlayer && (hit.Has(MoveFlags.Launch) || !World.IsAlive(tf.LaunchedBy)))
            {
                tf.LaunchedBy = hit.Attacker;
            }

            SetState(ref tf, FighterState.Juggle);
            tf.Move = MoveId.None;
        }
        else
        {
            m.Velocity = new Vector3(kx, 0, 0);
            SetState(ref tf, FighterState.HitStun);
            tf.StunTicks = hit.HitStun;
            tf.Move = MoveId.None;
        }
    }

    private void HitProp(Entity prop, Entity attacker, MoveDef def, float facing)
    {
        ref var pr = ref World.Get<Prop>(prop);
        ref var health = ref World.Get<Health>(prop);
        ref var m = ref World.Get<Motion>(prop);
        float damage = def.Damage * World.Get<Fighter>(attacker).Stats.Power;
        health.Current -= damage;
        var pos = World.Get<Transform>(prop).Position;
        Emit(GameEventKind.Hit, attacker, prop, pos + new Vector3(0, 0.6f, 0), damage, 0, def.Fx == HitFx.Slash ? HitFx.Slash : HitFx.Blunt, def.Id);
        if (health.Current <= 0 && IsBreakable(pr.Kind))
        {
            BreakProp(prop);
            return;
        }

        // Kicked props slide into enemies.
        float weight = World.Get<Body>(prop).Weight;
        float speed = (def.Knockback.X + 4f) * 1.6f / weight;
        if (speed > 2f)
        {
            m.Velocity = new Vector3(facing * speed, MathF.Min(3f, def.Knockback.Y * 0.4f), 0);
            m.Grounded = m.Velocity.Y <= 0;
            pr.Rolling = true;
            pr.Thrower = attacker;
            World.Get<HitMemory>(prop) = default;
        }
    }

    internal void BreakProp(Entity prop)
    {
        var pos = World.Get<Transform>(prop).Position;
        var kind = World.Get<Prop>(prop).Kind;
        Emit(GameEventKind.PropBroken, Entity.Null, prop, pos, param: (int)kind);
        if (kind is PropKind.PetiKayu or PropKind.TongSampah && _rng.Chance(0.6f))
        {
            DropRandomPickup(pos, generous: true);
        }

        World.Destroy(prop);
    }

    private void DropRandomPickup(Vector3 pos, bool generous)
    {
        float r = _rng.NextFloat();
        PickupKind kind = r switch
        {
            < 0.25f => PickupKind.Sate,
            < 0.45f => PickupKind.EsTeh,
            < 0.6f => PickupKind.KopiTubruk,
            < 0.72f when generous => PickupKind.NasiPadang,
            < 0.85f => PickupKind.Weapon,
            _ => PickupKind.Uang,
        };
        var weapon = kind == PickupKind.Weapon ? (WeaponId)_rng.Range(1, (int)WeaponId.PipaBesi + 1) : WeaponId.None;
        SpawnPickup(kind, pos + new Vector3(0, 0.5f, 0), weapon).Also(this, dropped: true);
    }
}
