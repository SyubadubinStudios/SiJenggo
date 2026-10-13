using System.Numerics;
using SiJenggo.Core;
using SiJenggo.Core.Ecs;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;

namespace SiJenggo.Game;

/// <summary>Projectiles, telegraphed hazards, props (carried, thrown, sliding) and pickups.</summary>
public sealed partial class GameSession
{
    private void UpdateProjectiles()
    {
        var pool = World.Pool<Projectile>();
        var ids = pool.EntityIds;
        for (int i = ids.Length - 1; i >= 0; i--)
        {
            var e = World.Handle(ids[i]);
            ref var p = ref pool.Get(e.Id);
            ref var t = ref World.Get<Transform>(e);
            ref var m = ref World.Get<Motion>(e);
            p.Ticks++;

            // The peci comes back like a boomerang.
            if (p.Kind == ProjectileKind.Peci && p.Ticks == 26)
            {
                m.Velocity.X = -m.Velocity.X;
                World.Get<HitMemory>(e) = default;
            }

            if (p.Kind == ProjectileKind.Peci && p.Ticks > 30 && World.IsAlive(p.Owner) && MathX.GroundDistance(World.Get<Transform>(p.Owner).Position, t.Position) < 0.8f)
            {
                World.Destroy(e);
                continue;
            }

            if (p.Ticks >= p.MaxTicks || MathF.Abs(t.Position.X - CameraX) > ViewHalfWidth + 6)
            {
                World.Destroy(e);
                continue;
            }

            long key = HitMemory.Key(e.Id, 0);
            foreach (var target in _fighters)
            {
                if (!World.IsAlive(target))
                {
                    continue;
                }

                var team = World.Get<Team>(target).Id;
                if (team == p.Team || !CanBeHit(target, false))
                {
                    continue;
                }

                if (!Overlaps(t.Position, new Vector3(0.35f, 0.35f, 0.35f), target))
                {
                    continue;
                }

                ref var memory = ref World.Get<HitMemory>(target);
                if (!memory.CanHit(key, Tick, 0))
                {
                    continue;
                }

                memory.Record(key, Tick);
                var spec = new HitSpec
                {
                    Attacker = World.IsAlive(p.Owner) ? p.Owner : Entity.Null,
                    Damage = p.Damage,
                    Knockback = p.Knockdown ? new Vector2(6f, 3f) : new Vector2(2f, 0),
                    Direction = MathF.Sign(m.Velocity.X) == 0 ? 1 : MathF.Sign(m.Velocity.X),
                    HitStun = p.Kind == ProjectileKind.Gadget ? 60 : 18,
                    HitStop = 5,
                    Flags = p.Knockdown ? MoveFlags.Knockdown : MoveFlags.None,
                    Fx = p.Kind switch
                    {
                        ProjectileKind.Laser or ProjectileKind.Gadget => HitFx.Lightning,
                        ProjectileKind.WindBlade => HitFx.Wind,
                        ProjectileKind.Harpoon or ProjectileKind.Shuriken or ProjectileKind.Weapon => HitFx.Slash,
                        _ => HitFx.Blunt,
                    },
                    RageGain = 3,
                    Cause = p.Kind == ProjectileKind.Weapon ? KoCause.Weapon : KoCause.Strike,
                    Point = t.Position,
                };
                ApplyHit(target, ref spec);

                // Most projectiles stop at the first body; lasers and the peci pierce.
                if (p.Kind is not (ProjectileKind.Laser or ProjectileKind.Peci))
                {
                    World.Destroy(e);
                    break;
                }
            }
        }
    }

    private static (float Damage, Vector2 Knockback, MoveFlags Flags, HitFx Fx, int Stun) HazardHit(HazardKind kind) => kind switch
    {
        HazardKind.Lightning => (14, new(4, 6), MoveFlags.Knockdown, HitFx.Lightning, 20),
        HazardKind.FallingRock => (16, new(3, 4), MoveFlags.Knockdown, HitFx.Heavy, 20),
        HazardKind.LavaPool => (6, new(2, 5), MoveFlags.Knockdown, HitFx.Fire, 20),
        HazardKind.FallingTree => (20, new(2, 3), MoveFlags.Knockdown, HitFx.Heavy, 20),
        HazardKind.SpearTrap => (12, new(1, 9), MoveFlags.Launch, HitFx.Slash, 20),
        HazardKind.CrateDrop => (16, new(3, 3), MoveFlags.Knockdown, HitFx.Heavy, 20),
        HazardKind.Trap => (12, Vector2.Zero, MoveFlags.None, HitFx.Lightning, 150),
        HazardKind.WildBoar => (12, new(8, 5), MoveFlags.Knockdown, HitFx.Heavy, 20),
        HazardKind.LaserSweep => (12, new(4, 5), MoveFlags.Knockdown, HitFx.Fire, 20),
        HazardKind.Shockwave => (14, new(7, 5), MoveFlags.Knockdown, HitFx.Shockwave, 20),
        _ => (10, new(3, 3), MoveFlags.Knockdown, HitFx.Heavy, 20),
    };

    private void UpdateHazards()
    {
        var pool = World.Pool<Hazard>();
        var ids = pool.EntityIds;
        for (int i = ids.Length - 1; i >= 0; i--)
        {
            var e = World.Handle(ids[i]);
            ref var h = ref pool.Get(e.Id);
            ref var t = ref World.Get<Transform>(e);
            h.Ticks++;

            if (h.Ticks == h.WarnTicks)
            {
                Emit(GameEventKind.HazardStrike, Entity.Null, e, t.Position, h.Radius, (int)h.Kind);
            }

            if (h.Ticks >= h.WarnTicks + h.ActiveTicks)
            {
                World.Destroy(e);
                continue;
            }

            if (h.Ticks < h.WarnTicks)
            {
                continue;
            }

            var (_, kb, flags, fx, stun) = HazardHit(h.Kind);
            long key = HitMemory.Key(e.Id, 0);
            bool consumed = false;
            foreach (var target in _fighters)
            {
                if (!World.IsAlive(target))
                {
                    continue;
                }

                var team = World.Get<Team>(target).Id;
                if (team == h.Team || (h.Team == TeamId.Neutral && team == TeamId.Neutral) || !CanBeHit(target, true))
                {
                    continue;
                }

                var tp = World.Get<Transform>(target).Position;
                if (MathX.GroundDistance(tp, t.Position) > h.Radius + World.Get<Body>(target).Radius || tp.Y > 2.2f)
                {
                    continue;
                }

                ref var memory = ref World.Get<HitMemory>(target);
                if (!memory.CanHit(key, Tick, h.Persistent ? h.RehitTicks : 0))
                {
                    continue;
                }

                memory.Record(key, Tick);
                var spec = new HitSpec
                {
                    Attacker = Entity.Null,
                    Damage = h.Damage,
                    Knockback = kb,
                    Direction = tp.X >= t.Position.X ? 1 : -1,
                    HitStun = stun,
                    HitStop = 4,
                    Flags = flags,
                    Fx = fx,
                    Cause = KoCause.Hazard,
                    Point = tp + new Vector3(0, 1f, 0),
                };

                // Player-made hazards (Rani's mines, Ucup's drum) credit the players.
                if (h.Team == TeamId.Players && World.Has<Enemy>(target))
                {
                    var owner = NearestPlayer(t.Position);
                    if (World.IsAlive(owner))
                    {
                        World.Get<Enemy>(target).LastHitterSlot = World.Get<Player>(owner).Slot;
                    }
                }

                ApplyHit(target, ref spec);
                if (h.Kind == HazardKind.Trap)
                {
                    consumed = true;
                    break;
                }
            }

            if (consumed)
            {
                World.Destroy(e);
            }
        }
    }

    private void UpdateProps()
    {
        var pool = World.Pool<Prop>();
        var ids = pool.EntityIds;
        for (int i = ids.Length - 1; i >= 0; i--)
        {
            var e = World.Handle(ids[i]);
            ref var pr = ref pool.Get(e.Id);
            ref var t = ref World.Get<Transform>(e);
            ref var m = ref World.Get<Motion>(e);

            if (World.IsAlive(pr.Carrier))
            {
                var ct = World.Get<Transform>(pr.Carrier);
                t.Position = ct.Position + new Vector3(0, 2.05f, 0);
                t.Facing = ct.Facing;
                m.Velocity = Vector3.Zero;
                m.Grounded = false;
                continue;
            }

            pr.Carrier = Entity.Null;
            float speed = MathF.Sqrt(m.Velocity.X * m.Velocity.X + m.Velocity.Z * m.Velocity.Z);
            bool dangerous = pr.Thrown || (pr.Rolling && speed > 2.5f);
            if (!dangerous)
            {
                pr.Rolling = false;
                continue;
            }

            var throwerTeam = World.IsAlive(pr.Thrower) ? World.Get<Team>(pr.Thrower).Id : TeamId.Neutral;
            var body = World.Get<Body>(e);
            long key = HitMemory.Key(e.Id, 1);
            foreach (var target in _fighters)
            {
                if (!World.IsAlive(target) || target == pr.Thrower || World.Get<Team>(target).Id == throwerTeam || !CanBeHit(target, false))
                {
                    continue;
                }

                if (!Overlaps(t.Position + new Vector3(0, body.Height * 0.5f, 0), new Vector3(body.Radius, body.Height * 0.5f, body.Radius), target))
                {
                    continue;
                }

                ref var memory = ref World.Get<HitMemory>(target);
                if (!memory.CanHit(key, Tick, 0))
                {
                    continue;
                }

                memory.Record(key, Tick);
                var spec = new HitSpec
                {
                    Attacker = World.IsAlive(pr.Thrower) ? pr.Thrower : Entity.Null,
                    Damage = (pr.Thrown ? 16f : 9f) * MathF.Sqrt(body.Weight) * (World.IsAlive(pr.Thrower) ? World.Get<Fighter>(pr.Thrower).Stats.Power : 1f),
                    Knockback = new Vector2(7f, 4f),
                    Direction = MathF.Sign(m.Velocity.X) == 0 ? 1 : MathF.Sign(m.Velocity.X),
                    HitStun = 20,
                    HitStop = 7,
                    Flags = MoveFlags.Knockdown,
                    Fx = HitFx.Heavy,
                    RageGain = 5,
                    Cause = KoCause.Prop,
                    Point = World.Get<Transform>(target).Position + new Vector3(0, 1f, 0),
                };
                ApplyHit(target, ref spec);

                if (pr.Thrown)
                {
                    // A thrown prop smashes on the first body; heavy ones bounce off.
                    if (IsBreakable(pr.Kind))
                    {
                        BreakProp(e);
                        break;
                    }

                    m.Velocity.X *= -0.25f;
                }
                else
                {
                    m.Velocity *= 0.7f;
                }
            }
        }
    }

    private void UpdatePickups()
    {
        var pool = World.Pool<Pickup>();
        var ids = pool.EntityIds;
        for (int i = ids.Length - 1; i >= 0; i--)
        {
            var e = World.Handle(ids[i]);
            ref var pk = ref pool.Get(e.Id);
            pk.Ticks++;
            if (pk.Dropped && pk.Ticks > 1200)
            {
                World.Destroy(e);
                continue;
            }

            if (pk.Kind == PickupKind.Weapon)
            {
                continue;
            }

            var pos = World.Get<Transform>(e).Position;
            for (int s = 0; s < PlayerCount; s++)
            {
                var pe = _players[s];
                if (!World.IsAlive(pe) || World.Get<Fighter>(pe).State is FighterState.Dead or FighterState.Downed)
                {
                    continue;
                }

                if (MathX.GroundDistance(World.Get<Transform>(pe).Position, pos) > 0.9f)
                {
                    continue;
                }

                ref var health = ref World.Get<Health>(pe);
                ref var rage = ref World.Get<Rage>(pe);
                ref var player = ref World.Get<Player>(pe);
                bool hurt = health.Current < health.Max - 0.5f;
                bool taken = true;
                switch (pk.Kind)
                {
                    case PickupKind.NasiPadang when hurt: health.Current = health.Max; break;
                    case PickupKind.Sate when hurt: health.Current = MathF.Min(health.Max, health.Current + health.Max * 0.5f); break;
                    case PickupKind.EsTeh when hurt || rage.Semangat < 100: health.Current = MathF.Min(health.Max, health.Current + health.Max * 0.15f); rage.Semangat = 100; break;
                    case PickupKind.KopiTubruk: rage.Meter = MathF.Min(100, rage.Meter + 40); break;
                    case PickupKind.Uang: player.Score += 1000; break;
                    default: taken = false; break;
                }

                if (taken)
                {
                    Emit(GameEventKind.PickupTaken, pe, e, pos, param: (int)pk.Kind);
                    World.Destroy(e);
                    break;
                }
            }
        }
    }

    private void UpdateRage()
    {
        for (int s = 0; s < PlayerCount; s++)
        {
            var e = _players[s];
            if (!World.IsAlive(e))
            {
                continue;
            }

            ref var rage = ref World.Get<Rage>(e);
            float regen = 8f * Config.Players[s].SemangatRegen;
            rage.Semangat = MathF.Min(100, rage.Semangat + regen * Dt);
            if (rage.Active)
            {
                float duration = 600 * World.Get<Fighter>(e).Stats.RageDuration;
                rage.ActiveTicks--;
                rage.Meter = 100f * rage.ActiveTicks / duration;
                if (rage.ActiveTicks <= 0)
                {
                    rage.Active = false;
                    rage.Meter = 0;
                    Emit(GameEventKind.RageOff, e, Entity.Null, World.Get<Transform>(e).Position, param: s);
                }
            }
        }
    }
}
