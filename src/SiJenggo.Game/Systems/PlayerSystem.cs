using System.Numerics;
using SiJenggo.Core;
using SiJenggo.Core.Ecs;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;

namespace SiJenggo.Game;

/// <summary>Turns each player's input into fighter actions: movement, combos, specials, rage, grabs, props.</summary>
public sealed partial class GameSession
{
    private const int BufferWindow = 8;
    private const InputButtons ActionButtons = InputButtons.Light | InputButtons.Heavy | InputButtons.Special | InputButtons.Jump | InputButtons.Grab | InputButtons.Rage | InputButtons.Dodge;

    private void UpdatePlayers()
    {
        for (int slot = 0; slot < PlayerCount; slot++)
        {
            var e = _players[slot];
            if (!World.IsAlive(e))
            {
                continue;
            }

            ref var p = ref World.Get<Player>(e);
            ref var f = ref World.Get<Fighter>(e);

            if (p.ComboTimer > 0 && --p.ComboTimer == 0)
            {
                p.Combo = 0;
            }

            // Input buffer: remember attack presses for a few ticks so mashing during recovery still chains.
            var pressed = p.Input.Current & ~p.Input.Previous & ActionButtons;
            if (pressed != 0)
            {
                p.Buffered |= pressed;
                p.BufferTicks = BufferWindow;
            }
            else if (p.BufferTicks > 0 && --p.BufferTicks == 0)
            {
                p.Buffered = InputButtons.None;
            }

            if (f.HitStopTicks > 0 || State == SessionState.StageClear)
            {
                continue;
            }

            ControlPlayer(e, ref p, ref f);
        }
    }

    private bool Wants(ref Player p, InputButtons button) => (p.Buffered & button) != 0;

    private static void Consume(ref Player p, InputButtons button) => p.Buffered &= ~button;

    private void ControlPlayer(Entity e, ref Player p, ref Fighter f)
    {
        ref var t = ref World.Get<Transform>(e);
        ref var m = ref World.Get<Motion>(e);
        var def = Characters.Get(p.Character);
        var axis = p.Input.Axis;

        switch (f.State)
        {
            case FighterState.Idle or FighterState.Walk:
                if (TryGroundAction(e, ref p, ref f, def, axis))
                {
                    return;
                }

                Walk(ref f, ref t, ref m, axis, 1f);
                return;

            case FighterState.Carrying:
                if (Wants(ref p, InputButtons.Light | InputButtons.Heavy | InputButtons.Grab))
                {
                    Consume(ref p, InputButtons.Light | InputButtons.Heavy | InputButtons.Grab);
                    ThrowCarriedProp(e, ref f, ref t);
                    return;
                }

                Walk(ref f, ref t, ref m, axis, 0.7f);
                f.State = FighterState.Carrying;
                return;

            case FighterState.Grabbing:
                GrabControl(e, ref p, ref f, ref t, def);
                return;

            case FighterState.Airborne:
                if (!f.AirAttackUsed && Wants(ref p, InputButtons.Light | InputButtons.Heavy))
                {
                    bool heavy = Wants(ref p, InputButtons.Heavy);
                    Consume(ref p, InputButtons.Light | InputButtons.Heavy);
                    f.AirAttackUsed = true;
                    StartMove(e, ref f, heavy ? def.AirHeavy : def.AirLight);
                    return;
                }

                // A little air control.
                m.Velocity.X = MathX.MoveTowards(m.Velocity.X, axis.X * f.Stats.WalkSpeed, 12f * Dt);
                if (axis.X != 0)
                {
                    t.Facing = MathF.Sign(axis.X);
                }

                return;

            case FighterState.Attack:
                TryCancel(e, ref p, ref f, def);
                return;
        }
    }

    private void Walk(ref Fighter f, ref Transform t, ref Motion m, Vector2 axis, float speedScale)
    {
        if (!m.Grounded)
        {
            return;
        }

        m.Velocity.X = axis.X * f.Stats.WalkSpeed * speedScale;
        m.Velocity.Z = axis.Y * f.Stats.DepthSpeed * speedScale;
        if (axis.X != 0)
        {
            t.Facing = MathF.Sign(axis.X);
        }

        var next = axis == Vector2.Zero ? FighterState.Idle : FighterState.Walk;
        if (f.State != next)
        {
            SetState(ref f, next);
        }
    }

    private bool TryGroundAction(Entity e, ref Player p, ref Fighter f, CharacterDef def, Vector2 axis)
    {
        ref var t = ref World.Get<Transform>(e);
        ref var m = ref World.Get<Motion>(e);
        ref var weapon = ref World.Get<WeaponHolder>(e);

        if (Wants(ref p, InputButtons.Rage))
        {
            Consume(ref p, InputButtons.Rage);
            if (TryRage(e, ref p, ref f))
            {
                return true;
            }
        }

        if (Wants(ref p, InputButtons.Dodge))
        {
            bool counter = Wants(ref p, InputButtons.Light) || p.Input.Held(InputButtons.Light);
            Consume(ref p, InputButtons.Dodge | InputButtons.Light);
            if (counter)
            {
                StartMove(e, ref f, def.Counter);
            }
            else
            {
                SetState(ref f, FighterState.Dodge);
                f.InvulnTicks = 14;
                var dir = axis == Vector2.Zero ? new Vector2(-t.Facing, 0) : Vector2.Normalize(axis);
                m.Velocity = new Vector3(dir.X * 9f, 0, dir.Y * 7f);
            }

            return true;
        }

        if (Wants(ref p, InputButtons.Special))
        {
            Consume(ref p, InputButtons.Special);
            return TrySpecial(e, ref p, ref f, def, axis, t.Facing);
        }

        if (Wants(ref p, InputButtons.Grab))
        {
            Consume(ref p, InputButtons.Grab);
            if (TryGrabInteract(e, ref p, ref f, ref t, ref weapon))
            {
                return true;
            }
        }

        if (Wants(ref p, InputButtons.Jump) && m.Grounded)
        {
            Consume(ref p, InputButtons.Jump);
            m.Velocity.Y = f.Stats.JumpVelocity;
            m.Velocity.X = axis.X * f.Stats.WalkSpeed;
            m.Velocity.Z = axis.Y * f.Stats.DepthSpeed * 0.6f;
            m.Grounded = false;
            f.AirAttackUsed = false;
            SetState(ref f, FighterState.Airborne);
            Emit(GameEventKind.Jump, e, Entity.Null, t.Position);
            return true;
        }

        if (Wants(ref p, InputButtons.Heavy))
        {
            Consume(ref p, InputButtons.Heavy);
            bool dashing = f.State == FighterState.Walk && axis.X != 0 && MathF.Sign(axis.X) == t.Facing && f.StateTicks > 8;
            if (weapon.Weapon != WeaponId.None)
            {
                StartMove(e, ref f, Weapons.Get(weapon.Weapon).Heavy);
            }
            else
            {
                StartMove(e, ref f, dashing ? def.Dash : def.Heavy);
            }

            return true;
        }

        if (Wants(ref p, InputButtons.Light))
        {
            Consume(ref p, InputButtons.Light);
            f.ComboStep = 0;
            StartMove(e, ref f, ChainMove(def, weapon.Weapon, 0));
            return true;
        }

        return false;
    }

    private static MoveId[] Chain(CharacterDef def, WeaponId weapon) => weapon == WeaponId.None ? def.LightChain : Weapons.Get(weapon).Chain;

    private static MoveId ChainMove(CharacterDef def, WeaponId weapon, int step) => Chain(def, weapon)[step];

    /// <summary>Combo cancels: light continues the chain, heavy finishes it, special cancels a landed hit.</summary>
    private void TryCancel(Entity e, ref Player p, ref Fighter f, CharacterDef def)
    {
        var move = f.Move.Def;
        int cancelAt = (int)(move.EffectiveCancelFrom / Math.Max(0.5f, f.Stats.ComboWindow));
        if (f.MoveTick < cancelAt || move.Has(MoveFlags.Ultimate) || move.Has(MoveFlags.Throw))
        {
            return;
        }

        var weaponId = World.Get<WeaponHolder>(e).Weapon;
        var chain = Chain(def, weaponId);
        bool inChain = Array.IndexOf(chain, f.Move) >= 0;
        bool rage = World.Get<Rage>(e).Active;

        if (inChain && Wants(ref p, InputButtons.Light))
        {
            Consume(ref p, InputButtons.Light);
            int next = f.ComboStep + 1;
            if (next >= chain.Length)
            {
                // Rage Nusantara opens an endless chain: loop back to the second strike.
                if (!rage)
                {
                    return;
                }

                next = Math.Min(1, chain.Length - 1);
            }

            StartMove(e, ref f, chain[next]);
            f.ComboStep = next;
            return;
        }

        if (inChain && f.HitsThisMove > 0 && Wants(ref p, InputButtons.Heavy))
        {
            Consume(ref p, InputButtons.Heavy);
            StartMove(e, ref f, weaponId == WeaponId.None ? def.Heavy : Weapons.Get(weaponId).Heavy);
            return;
        }

        if ((f.HitsThisMove > 0 || rage) && Wants(ref p, InputButtons.Special) && !move.Has(MoveFlags.Special))
        {
            Consume(ref p, InputButtons.Special);
            TrySpecial(e, ref p, ref f, def, p.Input.Axis, World.Get<Transform>(e).Facing);
        }
    }

    private bool TrySpecial(Entity e, ref Player p, ref Fighter f, CharacterDef def, Vector2 axis, float facing)
    {
        ref var rage = ref World.Get<Rage>(e);
        var loadout = Config.Players[p.Slot];
        MoveId id;
        if (axis.Y < -0.5f && !loadout.Jurus.IsNone)
        {
            id = loadout.Jurus;
            Stats.JurusUsed++;
        }
        else if (axis.Y > 0.5f)
        {
            id = def.Specials[2];
        }
        else if (axis.X != 0)
        {
            id = def.Specials[1];
            World.Get<Transform>(e).Facing = MathF.Sign(axis.X);
        }
        else
        {
            id = def.Specials[0];
        }

        float cost = rage.Active ? 0 : id.Def.SemangatCost * loadout.SpecialCostMultiplier;
        if (rage.Semangat < cost)
        {
            Emit(GameEventKind.NotEnoughSpirit, e, Entity.Null, World.Get<Transform>(e).Position);
            return false;
        }

        rage.Semangat -= cost;
        StartMove(e, ref f, id);
        return true;
    }

    private bool TryRage(Entity e, ref Player p, ref Fighter f)
    {
        ref var rage = ref World.Get<Rage>(e);
        var pos = World.Get<Transform>(e).Position;
        if (rage.Active)
        {
            rage.Active = false;
            rage.Meter = 0;
            rage.ActiveTicks = 0;
            StartMove(e, ref f, Characters.Get(p.Character).Ultimate);
            Stats.UltimatesUsed++;
            Emit(GameEventKind.Ultimate, e, Entity.Null, pos, param: (int)p.Character, move: f.Move);
            return true;
        }

        if (rage.Meter >= 100)
        {
            rage.Active = true;
            rage.ActiveTicks = (int)(600 * f.Stats.RageDuration);
            Emit(GameEventKind.RageOn, e, Entity.Null, pos, param: p.Slot);
            return true;
        }

        return TryTeamFinisher(e, ref f, ref rage, pos);
    }

    /// <summary>Grab: weapon/food pickup, prop lift, enemy grab (or double grab with a partner). Returns true if something happened.</summary>
    private bool TryGrabInteract(Entity e, ref Player p, ref Fighter f, ref Transform t, ref WeaponHolder weapon)
    {
        // 1. Pickups on the floor.
        var pickups = World.Pool<Pickup>();
        var pickupIds = pickups.EntityIds;
        for (int i = pickupIds.Length - 1; i >= 0; i--)
        {
            int id = pickupIds[i];
            ref var pk = ref pickups.Get(id);
            var pp = World.Pool<Transform>().Get(id).Position;
            if (pk.Kind == PickupKind.Weapon && MathX.GroundDistance(pp, t.Position) < 1.0f && pp.Y < 0.5f)
            {
                if (weapon.Weapon != WeaponId.None)
                {
                    SpawnPickup(PickupKind.Weapon, t.Position + new Vector3(0, 0.6f, 0), weapon.Weapon).Also(this, dropped: true);
                }

                weapon.Weapon = pk.Weapon;
                weapon.Durability = Weapons.Get(pk.Weapon).Durability;
                Emit(GameEventKind.PickupTaken, e, Entity.Null, pp, param: 100 + (int)pk.Weapon);
                World.Destroy(World.Handle(id));
                return true;
            }
        }

        // 2. A grabbable enemy in front.
        var target = FindGrabTarget(e, t);
        if (!target.IsNull)
        {
            ref var tf = ref World.Get<Fighter>(target);
            if (tf.State == FighterState.Grabbed && World.Has<Player>(tf.GrabbedBy) && tf.GrabbedBy != e)
            {
                DoubleGrab(e, ref f, tf.GrabbedBy, target);
                return true;
            }

            BeginGrab(e, ref f, ref t, target);
            return true;
        }

        // 3. A prop to lift.
        float liftStrength = Characters.Get(p.Character).Weight;
        var props = World.Pool<Prop>();
        foreach (int id in props.EntityIds)
        {
            ref var prop = ref props.Get(id);
            var pp = World.Pool<Transform>().Get(id).Position;
            if (!World.IsAlive(prop.Carrier) && !prop.Thrown && pp.Y < 0.2f && MathX.GroundDistance(pp, t.Position) < World.Pool<Body>().Get(id).Radius + 0.8f && IsLiftable(prop.Kind, liftStrength))
            {
                prop.Carrier = e;
                prop.Thrower = e;
                f.CarriedProp = World.Handle(id);
                SetState(ref f, FighterState.Carrying);
                World.Get<Motion>(e).Velocity = Vector3.Zero;
                Emit(GameEventKind.Grab, e, f.CarriedProp, pp);
                return true;
            }
        }

        // 4. Nothing around: throw the held weapon.
        if (weapon.Weapon != WeaponId.None)
        {
            var w = Weapons.Get(weapon.Weapon);
            var proj = SpawnProjectile(e, TeamId.Players, ProjectileKind.Weapon, t.Position + new Vector3(t.Facing * 0.6f, 1.2f, 0), new Vector3(t.Facing * 16f, 0, 0), w.ThrowDamage * f.Stats.Power, true, 60);
            World.Get<Visual>(proj).Variant = 100 + (int)weapon.Weapon;
            weapon.Weapon = WeaponId.None;
            weapon.Durability = 0;
            return true;
        }

        return false;
    }

    private Entity FindGrabTarget(Entity grabber, in Transform t)
    {
        var team = World.Get<Team>(grabber).Id;
        Entity best = Entity.Null;
        float bestD = 1.15f;
        foreach (var o in _fighters)
        {
            if (o == grabber || World.Get<Team>(o).Id == team || World.Has<Boss>(o))
            {
                continue;
            }

            ref var of = ref World.Get<Fighter>(o);
            if (of.State is not (FighterState.Idle or FighterState.Walk or FighterState.HitStun or FighterState.Grabbed) || World.Get<Health>(o).Dead)
            {
                continue;
            }

            var op = World.Get<Transform>(o).Position;
            float dx = (op.X - t.Position.X) * t.Facing;
            if (dx < -0.2f || MathF.Abs(op.Z - t.Position.Z) > 0.5f || op.Y > 0.3f)
            {
                continue;
            }

            float d = MathX.GroundDistance(op, t.Position);
            if (d < bestD)
            {
                bestD = d;
                best = o;
            }
        }

        return best;
    }

    private void BeginGrab(Entity e, ref Fighter f, ref Transform t, Entity target)
    {
        ref var tf = ref World.Get<Fighter>(target);
        SetState(ref f, FighterState.Grabbing);
        f.GrabTarget = target;
        f.GrabStrikes = 0;
        World.Get<Motion>(e).Velocity = Vector3.Zero;
        CancelActions(target, ref tf);
        SetState(ref tf, FighterState.Grabbed);
        tf.GrabbedBy = e;
        World.Get<Transform>(target).Facing = -t.Facing;
        if (World.Has<AiBrain>(target))
        {
            World.Get<AiBrain>(target).HasToken = false;
        }

        Emit(GameEventKind.Grab, e, target, World.Get<Transform>(target).Position);
    }

    private void GrabControl(Entity e, ref Player p, ref Fighter f, ref Transform t, CharacterDef def)
    {
        if (Wants(ref p, InputButtons.Heavy) || (Wants(ref p, InputButtons.Light) && f.GrabStrikes >= 3))
        {
            Consume(ref p, InputButtons.Heavy | InputButtons.Light);
            // Holding back turns it into a back throw.
            if (p.Input.Axis.X != 0 && MathF.Sign(p.Input.Axis.X) != t.Facing)
            {
                t.Facing = -t.Facing;
            }

            StartMove(e, ref f, def.Throw);
            return;
        }

        if (Wants(ref p, InputButtons.Light))
        {
            Consume(ref p, InputButtons.Light);
            f.GrabStrikes++;
            StartMove(e, ref f, def.GrabStrike);
        }
    }

    private void ThrowCarriedProp(Entity e, ref Fighter f, ref Transform t)
    {
        var prop = f.CarriedProp;
        f.CarriedProp = Entity.Null;
        SetState(ref f, FighterState.Idle);
        if (!World.IsAlive(prop))
        {
            return;
        }

        ref var pr = ref World.Get<Prop>(prop);
        pr.Carrier = Entity.Null;
        pr.Thrown = true;
        pr.Thrower = e;
        ref var pm = ref World.Get<Motion>(prop);
        pm.Velocity = new Vector3(t.Facing * 13f, 3.5f, 0);
        pm.Grounded = false;
        World.Get<HitMemory>(prop) = default;
        Emit(GameEventKind.Throw, e, prop, World.Get<Transform>(prop).Position);
    }
}

internal static class EntityExtensions
{
    /// <summary>Marks a freshly spawned pickup as dropped (expires) — tiny helper to keep spawn sites terse.</summary>
    public static Entity Also(this Entity e, GameSession session, bool dropped)
    {
        if (dropped && session.World.Has<Pickup>(e))
        {
            session.World.Get<Pickup>(e).Dropped = true;
        }

        return e;
    }
}
