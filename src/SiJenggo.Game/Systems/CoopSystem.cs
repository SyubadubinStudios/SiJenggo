using System.Numerics;
using SiJenggo.Core;
using SiJenggo.Core.Ecs;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;

namespace SiJenggo.Game;

/// <summary>Co-op moves (double grab, team finisher, revive), player lives, KOs, and the AI partner.</summary>
public sealed partial class GameSession
{
    private const int DownedDuration = 720;
    private const int ReviveTicks = 75;

    private bool HasStandingPartner(Entity self)
    {
        for (int s = 0; s < PlayerCount; s++)
        {
            var o = _players[s];
            if (o != self && World.IsAlive(o) && World.Get<Fighter>(o).State is not (FighterState.Downed or FighterState.Dead))
            {
                return true;
            }
        }

        return false;
    }

    private void DoubleGrab(Entity e, ref Fighter f, Entity partner, Entity target)
    {
        ref var pf = ref World.Get<Fighter>(partner);
        ref var tf = ref World.Get<Fighter>(target);
        pf.GrabTarget = Entity.Null;
        tf.GrabbedBy = Entity.Null;
        SetState(ref tf, FighterState.Idle);
        StartMove(e, ref f, Moves.DoubleGrabSlam);
        StartMove(partner, ref pf, Moves.DoubleGrabSlam);
        var tp = World.Get<Transform>(target).Position;
        var spec = SpecFromMove(e, Moves.DoubleGrabSlam.Def, tp + new Vector3(0, 1f, 0), World.Get<Transform>(e).Facing);
        spec.Damage += SpecFromMove(partner, Moves.DoubleGrabSlam.Def, tp, 1).Damage * 0.5f;
        spec.Cause = KoCause.Throw;
        ApplyHit(target, ref spec);
        // Both players already dealt the damage; stop their hitboxes from hitting again.
        f.HitsThisMove = pf.HitsThisMove = 1;
        World.Get<HitMemory>(target).Record(HitMemory.Key(e.Id, f.AttackSerial), Tick);
        World.Get<HitMemory>(target).Record(HitMemory.Key(partner.Id, pf.AttackSerial), Tick);
        Stats.CoopMoves++;
        Emit(GameEventKind.CoopMove, e, target, tp, param: (int)CoopMove.DoubleGrab);
    }

    private bool TryTeamFinisher(Entity e, ref Fighter f, ref Rage rage, Vector3 pos)
    {
        if (rage.Meter < 50)
        {
            return false;
        }

        for (int s = 0; s < PlayerCount; s++)
        {
            var o = _players[s];
            if (o == e || !World.IsAlive(o))
            {
                continue;
            }

            ref var of = ref World.Get<Fighter>(o);
            ref var orage = ref World.Get<Rage>(o);
            if (orage.Meter < 50 || orage.Active || of.State is not (FighterState.Idle or FighterState.Walk) || MathX.GroundDistance(World.Get<Transform>(o).Position, pos) > 4.5f)
            {
                continue;
            }

            rage.Meter -= 50;
            orage.Meter -= 50;
            StartMove(e, ref f, Moves.TeamFinisher);
            StartMove(o, ref of, Moves.TeamFinisher);
            Stats.CoopMoves++;
            Emit(GameEventKind.CoopMove, e, o, pos, param: (int)CoopMove.TeamFinisher);
            return true;
        }

        return false;
    }

    private void UpdateCoop()
    {
        for (int s = 0; s < PlayerCount; s++)
        {
            var e = _players[s];
            if (!World.IsAlive(e))
            {
                continue;
            }

            ref var f = ref World.Get<Fighter>(e);
            ref var p = ref World.Get<Player>(e);
            if (f.State != FighterState.Downed)
            {
                continue;
            }

            var pos = World.Get<Transform>(e).Position;
            bool helped = false;
            for (int o = 0; o < PlayerCount; o++)
            {
                var helper = _players[o];
                if (helper == e || !World.IsAlive(helper))
                {
                    continue;
                }

                ref var hf = ref World.Get<Fighter>(helper);
                ref var hp = ref World.Get<Player>(helper);
                if (hf.State is FighterState.Idle or FighterState.Walk && hp.Input.Held(InputButtons.Grab) && MathX.GroundDistance(World.Get<Transform>(helper).Position, pos) < 1.5f)
                {
                    helped = true;
                    World.Get<Motion>(helper).Velocity = Vector3.Zero;
                }
            }

            p.ReviveProgress = helped ? p.ReviveProgress + 1 : Math.Max(0, p.ReviveProgress - 2);
            if (p.ReviveProgress >= ReviveTicks)
            {
                ref var health = ref World.Get<Health>(e);
                health.Current = health.Max * 0.4f;
                p.ReviveProgress = 0;
                SetState(ref f, FighterState.GetUp);
                f.InvulnTicks = 120;
                Stats.CoopMoves++;
                Emit(GameEventKind.PlayerRevived, e, Entity.Null, pos, param: s);
                Emit(GameEventKind.CoopMove, e, Entity.Null, pos, param: (int)CoopMove.Revive);
                continue;
            }

            if (--p.DownedTicks <= 0 || !HasStandingPartner(e))
            {
                LoseLife(e, ref p, ref f);
            }
        }
    }

    /// <summary>Called when a player at zero health lands from the final hit.</summary>
    private void OnPlayerFell(Entity e)
    {
        ref var f = ref World.Get<Fighter>(e);
        ref var p = ref World.Get<Player>(e);
        CancelActions(e, ref f);
        if (HasStandingPartner(e))
        {
            SetState(ref f, FighterState.Downed);
            p.DownedTicks = DownedDuration;
            p.ReviveProgress = 0;
            Emit(GameEventKind.PlayerDown, e, Entity.Null, World.Get<Transform>(e).Position, param: p.Slot);
        }
        else
        {
            LoseLife(e, ref p, ref f);
        }
    }

    private void LoseLife(Entity e, ref Player p, ref Fighter f)
    {
        SetState(ref f, FighterState.Dead);
        var pos = World.Get<Transform>(e).Position;
        if (p.Lives > 0)
        {
            p.Lives--;
            p.RespawnTicks = 100;
            Emit(GameEventKind.PlayerDown, e, Entity.Null, pos, param: p.Slot);
        }
        else
        {
            World.Get<Health>(e).Dead = true;
            Emit(GameEventKind.PlayerOut, e, Entity.Null, pos, param: p.Slot);
        }
    }

    private void UpdateDeaths()
    {
        // Players: respawn falls in from the top of the screen, invulnerable for a moment.
        for (int s = 0; s < PlayerCount; s++)
        {
            var e = _players[s];
            if (!World.IsAlive(e))
            {
                continue;
            }

            ref var p = ref World.Get<Player>(e);
            if (p.RespawnTicks > 0 && --p.RespawnTicks == 0)
            {
                ref var f = ref World.Get<Fighter>(e);
                ref var health = ref World.Get<Health>(e);
                ref var t = ref World.Get<Transform>(e);
                ref var m = ref World.Get<Motion>(e);
                health.Current = health.Max;
                health.Dead = false;
                t.Position = new Vector3(CameraX - ViewHalfWidth * 0.5f, 5f, 0);
                t.PreviousPosition = t.Position;
                m.Velocity = Vector3.Zero;
                m.Grounded = false;
                SetState(ref f, FighterState.Airborne);
                f.InvulnTicks = 180;
                World.Get<Rage>(e).Semangat = 100;
                World.Get<WeaponHolder>(e) = default;
                Emit(GameEventKind.PlayerRespawn, e, Entity.Null, t.Position, param: s);
            }
        }

        if (!AnyPlayerAliveOrRespawning() && State != SessionState.GameOver)
        {
            State = SessionState.GameOver;
            Emit(GameEventKind.GameOver, Entity.Null, Entity.Null, new Vector3(CameraX, 0, 0));
        }

        // Enemies: KO credit, drops, then fade out.
        var enemies = World.Pool<Enemy>();
        var ids = enemies.EntityIds;
        for (int i = ids.Length - 1; i >= 0; i--)
        {
            var e = World.Handle(ids[i]);
            ref var en = ref enemies.Get(e.Id);
            ref var health = ref World.Get<Health>(e);
            if (!health.Dead)
            {
                continue;
            }

            if (en.DeathTicks++ == 0)
            {
                OnEnemyKnockedOut(e, ref en);
            }

            if (en.DeathTicks > 110 && !World.Has<Boss>(e))
            {
                World.Destroy(e);
            }
        }
    }

    private bool AnyPlayerAliveOrRespawning()
    {
        for (int s = 0; s < PlayerCount; s++)
        {
            var e = _players[s];
            if (World.IsAlive(e) && !World.Get<Health>(e).Dead)
            {
                return true;
            }
        }

        return false;
    }

    private void OnEnemyKnockedOut(Entity e, ref Enemy en)
    {
        var pos = World.Get<Transform>(e).Position;
        Stats.KnockOuts++;
        switch (en.LastCause)
        {
            case KoCause.Weapon: Stats.WeaponKnockOuts++; break;
            case KoCause.Prop: Stats.PropKnockOuts++; break;
            case KoCause.Throw: Stats.ThrowKnockOuts++; break;
        }

        if (en.LastHitterSlot >= 0 && World.IsAlive(_players[en.LastHitterSlot]))
        {
            ref var killer = ref World.Get<Player>(_players[en.LastHitterSlot]);
            killer.Score += en.ScoreValue;
            killer.KnockOuts++;
        }

        for (int s = 0; s < PlayerCount; s++)
        {
            if (World.IsAlive(_players[s]))
            {
                World.Get<Player>(_players[s]).Xp += en.XpValue;
            }
        }

        Emit(GameEventKind.KnockOut, Entity.Null, e, pos, en.ScoreValue, en.LastHitterSlot);

        // Release any grab and drop the weapon.
        ref var f = ref World.Get<Fighter>(e);
        CancelActions(e, ref f);
        ref var weapon = ref World.Get<WeaponHolder>(e);
        if (weapon.Weapon != WeaponId.None)
        {
            SpawnPickup(PickupKind.Weapon, pos + new Vector3(0, 0.8f, 0), weapon.Weapon).Also(this, dropped: true);
            weapon.Weapon = WeaponId.None;
        }

        if (!World.Has<Boss>(e) && _rng.Chance(Enemies.Get(en.Kind).DropChance))
        {
            DropRandomPickup(pos, generous: false);
        }
    }

    // ───────────────────────────── AI partner ─────────────────────────────

    /// <summary>
    /// The CPU partner (Player + AI mode, and the attract-mode autopilot) writes button presses
    /// into its own input state, then goes through exactly the same player code as a human.
    /// </summary>
    private void UpdateCompanions()
    {
        for (int s = 0; s < PlayerCount; s++)
        {
            var e = _players[s];
            if (!World.IsAlive(e))
            {
                continue;
            }

            ref var p = ref World.Get<Player>(e);
            if (!p.IsAi)
            {
                continue;
            }

            p.Input.Push(CompanionButtons(e, ref p));
        }
    }

    private InputButtons CompanionButtons(Entity e, ref Player p)
    {
        ref var f = ref World.Get<Fighter>(e);
        var pos = World.Get<Transform>(e).Position;
        float facing = World.Get<Transform>(e).Facing;
        p.AiTimer++;
        var buttons = InputButtons.None;

        // 1. Revive a downed partner.
        for (int s = 0; s < PlayerCount; s++)
        {
            var o = _players[s];
            if (o != e && World.IsAlive(o) && World.Get<Fighter>(o).State == FighterState.Downed)
            {
                var op = World.Get<Transform>(o).Position;
                return MathX.GroundDistance(op, pos) < 1.2f ? InputButtons.Grab : SteerTowards(pos, op, 0.4f);
            }
        }

        // 2. Fight the nearest enemy.
        Entity target = Entity.Null;
        float best = float.MaxValue;
        int nearby = 0;
        foreach (var o in _fighters)
        {
            if (!World.Has<Enemy>(o) || World.Get<Health>(o).Dead)
            {
                continue;
            }

            var op = World.Get<Transform>(o).Position;
            if (MathF.Abs(op.X - CameraX) > ViewHalfWidth + 0.5f)
            {
                continue;
            }

            float d = MathX.GroundDistance(op, pos);
            if (d < 2.6f)
            {
                nearby++;
            }

            if (d < best)
            {
                best = d;
                target = o;
            }
        }

        ref var rage = ref World.Get<Rage>(e);
        bool tick = (p.AiTimer & 7) == 0;

        if (target.IsNull)
        {
            // Low on health: go for food.
            ref var health = ref World.Get<Health>(e);
            if (health.Fraction < 0.6f && TryFindFood(pos, out var food))
            {
                return SteerTowards(pos, food, 0.3f);
            }

            // Follow the first human player, or push on to the right.
            var leader = Entity.Null;
            for (int s = 0; s < PlayerCount; s++)
            {
                if (_players[s] != e && World.IsAlive(_players[s]) && !World.Get<Player>(_players[s]).IsAi)
                {
                    leader = _players[s];
                    break;
                }
            }

            var goal = World.IsAlive(leader)
                ? World.Get<Transform>(leader).Position + new Vector3(-2.2f, 0, 1.2f)
                : new Vector3(CameraLocked ? pos.X : pos.X + 3f, 0, 0);
            return MathX.GroundDistance(goal, pos) > 0.8f ? SteerTowards(pos, goal, 0.5f) : InputButtons.None;
        }

        var tp = World.Get<Transform>(target).Position;
        float dx = tp.X - pos.X;
        float dz = tp.Z - pos.Z;
        float side = dx >= 0 ? 1 : -1;

        if (rage.Active && nearby >= 2 && tick)
        {
            return InputButtons.Rage;
        }

        if (!rage.Active && rage.Meter >= 100 && tick)
        {
            return InputButtons.Rage;
        }

        if (MathF.Abs(dx) > 1.25f || MathF.Abs(dz) > 0.35f)
        {
            var goal = new Vector3(tp.X - side * 1.05f, 0, tp.Z);
            buttons = SteerTowards(pos, goal, 0.15f);
        }
        else
        {
            if (side != facing)
            {
                buttons |= side > 0 ? InputButtons.Right : InputButtons.Left;
            }

            int phase = p.AiTimer % 48;
            if (rage.Semangat >= 60 && nearby >= 2 && phase == 0)
            {
                buttons |= InputButtons.Special;
            }
            else if (phase is 40)
            {
                buttons |= InputButtons.Heavy;
            }
            else if ((p.AiTimer & 3) == 0)
            {
                buttons |= InputButtons.Light;
            }
        }

        return buttons;
    }

    private static InputButtons SteerTowards(Vector3 from, Vector3 to, float deadZone)
    {
        var b = InputButtons.None;
        if (to.X - from.X > deadZone) b |= InputButtons.Right;
        else if (from.X - to.X > deadZone) b |= InputButtons.Left;
        if (to.Z - from.Z > deadZone) b |= InputButtons.Up;
        else if (from.Z - to.Z > deadZone) b |= InputButtons.Down;
        return b;
    }

    private bool TryFindFood(Vector3 from, out Vector3 position)
    {
        position = default;
        float best = 12f;
        bool found = false;
        var pool = World.Pool<Pickup>();
        foreach (int id in pool.EntityIds)
        {
            var kind = pool.Get(id).Kind;
            if (kind is not (PickupKind.NasiPadang or PickupKind.Sate or PickupKind.EsTeh))
            {
                continue;
            }

            var pp = World.Pool<Transform>().Get(id).Position;
            float d = MathX.GroundDistance(pp, from);
            if (d < best)
            {
                best = d;
                position = pp;
                found = true;
            }
        }

        return found;
    }
}
