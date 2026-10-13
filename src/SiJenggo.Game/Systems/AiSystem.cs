using System.Numerics;
using SiJenggo.Core;
using SiJenggo.Core.Ecs;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;

namespace SiJenggo.Game;

/// <summary>
/// Enemy brains. Classic beat 'em up crowd logic: only a few enemies hold an "attack token" per player
/// at once; the rest circle at a distance, so fights stay readable and fair.
/// </summary>
public sealed partial class GameSession
{
    private int AttackTokensPerPlayer => Config.Difficulty switch
    {
        Difficulty.Santai => 1,
        Difficulty.Jagoan => 3,
        _ => 2,
    };

    private void UpdateEnemyAi()
    {
        if (State == SessionState.Cinematic)
        {
            return;
        }

        var brains = World.Pool<AiBrain>();
        var ids = brains.EntityIds;
        for (int i = 0; i < ids.Length; i++)
        {
            var e = World.Handle(ids[i]);
            if (World.Has<Boss>(e))
            {
                continue;
            }

            ref var brain = ref brains.Get(e.Id);
            ref var f = ref World.Get<Fighter>(e);
            if (brain.AttackCooldown > 0)
            {
                brain.AttackCooldown--;
            }

            if (f.HitStopTicks > 0 || f.State is not (FighterState.Idle or FighterState.Walk))
            {
                continue;
            }

            ref var t = ref World.Get<Transform>(e);
            ref var m = ref World.Get<Motion>(e);

            if (--brain.ThinkTicks <= 0)
            {
                brain.ThinkTicks = 6;
                Think(e, ref brain, ref f, ref t);
            }

            if (brain.State == AiState.Attack && !brain.PlannedMove.IsNone && InAttackRange(e, ref brain, t))
            {
                StartMove(e, ref f, brain.PlannedMove);
                brain.PlannedMove = MoveId.None;
                brain.HasToken = false;
                brain.State = AiState.Retreat;
                brain.Timer = 24;
                brain.AttackCooldown = (int)(_rng.Range(45, 110) / Math.Max(0.3f, brain.Aggression));
                continue;
            }

            if (brain.State == AiState.Ranged && !brain.PlannedMove.IsNone && brain.AttackCooldown <= 0)
            {
                var target = brain.Target;
                if (World.IsAlive(target))
                {
                    var tp = World.Get<Transform>(target).Position;
                    if (MathF.Abs(tp.Z - t.Position.Z) < 0.5f)
                    {
                        t.Facing = tp.X >= t.Position.X ? 1 : -1;
                        StartMove(e, ref f, brain.PlannedMove);
                        brain.PlannedMove = MoveId.None;
                        brain.AttackCooldown = (int)(_rng.Range(90, 160) / Math.Max(0.3f, brain.Aggression));
                        continue;
                    }
                }
            }

            // Apply the steering chosen by Think().
            float speed = f.Stats.WalkSpeed;
            m.Velocity.X = brain.MoveDir.X * speed;
            m.Velocity.Z = brain.MoveDir.Y * f.Stats.DepthSpeed;
            var next = brain.MoveDir == Vector2.Zero ? FighterState.Idle : FighterState.Walk;
            if (f.State != next)
            {
                SetState(ref f, next);
            }

            if (World.IsAlive(brain.Target) && brain.State != AiState.FetchWeapon)
            {
                float tx = World.Get<Transform>(brain.Target).Position.X;
                if (MathF.Abs(tx - t.Position.X) > 0.15f)
                {
                    t.Facing = tx >= t.Position.X ? 1 : -1;
                }
            }
        }
    }

    private bool InAttackRange(Entity e, ref AiBrain brain, in Transform t)
    {
        if (!World.IsAlive(brain.Target))
        {
            return false;
        }

        var def = brain.PlannedMove.Def;
        var tp = World.Get<Transform>(brain.Target).Position;
        float scale = World.Get<Visual>(e).Scale;
        float reach = (def.HitboxOffset.X + def.HitboxHalfSize.X * 0.7f) * scale + def.Lunge * def.Active / 60f * 0.8f + World.Get<Body>(brain.Target).Radius;
        float dx = MathF.Abs(tp.X - t.Position.X);
        return dx <= reach && dx > 0.25f && MathF.Abs(tp.Z - t.Position.Z) < 0.45f;
    }

    private void Think(Entity e, ref AiBrain brain, ref Fighter f, ref Transform t)
    {
        var target = NearestPlayer(t.Position);
        brain.Target = target;
        if (target.IsNull)
        {
            brain.MoveDir = Vector2.Zero;
            brain.State = AiState.Idle;
            return;
        }

        var en = World.Get<Enemy>(e);
        var def = Enemies.Get(en.Kind);
        var tp = World.Get<Transform>(target).Position;
        float dx = tp.X - t.Position.X;
        float dz = tp.Z - t.Position.Z;
        ref var weapon = ref World.Get<WeaponHolder>(e);

        // Enemies off screen walk in first.
        if (MathF.Abs(t.Position.X - CameraX) > ViewHalfWidth - 0.6f)
        {
            brain.MoveDir = new Vector2(MathF.Sign(CameraX - t.Position.X), 0);
            brain.State = AiState.Approach;
            return;
        }

        if (brain.State == AiState.Retreat && --brain.Timer > 0)
        {
            brain.MoveDir = new Vector2(-MathF.Sign(dx) * 0.7f, 0);
            return;
        }

        // Grab a weapon lying nearby.
        if (weapon.Weapon == WeaponId.None && def.Archetype != AiArchetype.Ranged && (brain.State == AiState.FetchWeapon || _rng.Chance(0.08f)) && TryFindWeapon(t.Position, out var wpos, out var wid))
        {
            brain.State = AiState.FetchWeapon;
            if (MathX.GroundDistance(wpos, t.Position) < 0.7f)
            {
                World.Destroy(wid);
                var picked = World.Get<Pickup>(wid).Weapon;
                weapon.Weapon = picked;
                weapon.Durability = Weapons.Get(picked).Durability;
                Emit(GameEventKind.PickupTaken, e, Entity.Null, wpos, param: 100 + (int)picked);
                brain.State = AiState.Approach;
            }
            else
            {
                brain.MoveDir = Steer(t.Position, wpos);
            }

            return;
        }

        if (def.Archetype == AiArchetype.Ranged)
        {
            ThinkRanged(ref brain, def, t, dx, dz);
            return;
        }

        // Attack tokens.
        if (!brain.HasToken && brain.AttackCooldown <= 0 && TokensOn(target) < AttackTokensPerPlayer && _rng.Chance(0.35f + 0.5f * brain.Aggression))
        {
            brain.HasToken = true;
            brain.PlannedMove = PickEnemyMove(def, weapon.Weapon);
        }

        if (brain.HasToken)
        {
            brain.State = AiState.Attack;
            if (brain.PlannedMove.IsNone)
            {
                brain.PlannedMove = PickEnemyMove(def, weapon.Weapon);
            }

            var mdef = brain.PlannedMove.Def;
            float ideal = MathF.Max(0.6f, (mdef.HitboxOffset.X + mdef.HitboxHalfSize.X * 0.3f) * World.Get<Visual>(e).Scale);
            var goal = new Vector3(tp.X - MathF.Sign(dx == 0 ? 1 : dx) * ideal, 0, tp.Z);
            brain.MoveDir = Steer(t.Position, goal);
            return;
        }

        // Circle at a distance on our own side of the player, drifting through slots.
        brain.State = AiState.Wait;
        if (--brain.Timer <= 0)
        {
            brain.Timer = _rng.Range(8, 20);
            brain.SlotOffset = new Vector2(_rng.Range(2.6f, 4.4f), _rng.Range(-2f, 2f));
        }

        float side = dx > 0 ? -1 : 1;
        var slot = new Vector3(tp.X + side * brain.SlotOffset.X, 0, Math.Clamp(tp.Z + brain.SlotOffset.Y, -Chapters.LaneHalfDepth, Chapters.LaneHalfDepth));
        brain.MoveDir = MathX.GroundDistance(slot, t.Position) > 0.5f ? Steer(t.Position, slot) * 0.75f : Vector2.Zero;
    }

    private void ThinkRanged(ref AiBrain brain, EnemyDef def, in Transform t, float dx, float dz)
    {
        float adx = MathF.Abs(dx);
        if (adx < 1.3f && MathF.Abs(dz) < 0.5f && brain.AttackCooldown <= 0)
        {
            brain.State = AiState.Attack;
            brain.HasToken = true;
            brain.PlannedMove = def.Attacks[^1];
            brain.MoveDir = Vector2.Zero;
            return;
        }

        brain.State = AiState.Ranged;
        brain.PlannedMove = def.Attacks[0];
        float wantX = adx < 4.5f ? -MathF.Sign(dx) : adx > 8f ? MathF.Sign(dx) : 0;
        float wantZ = MathF.Abs(dz) > 0.3f ? MathF.Sign(dz) : 0;
        var dir = new Vector2(wantX, wantZ);
        brain.MoveDir = dir == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(dir);
    }

    private static Vector2 Steer(Vector3 from, Vector3 to)
    {
        var d = new Vector2(to.X - from.X, to.Z - from.Z);
        if (d.LengthSquared() < 0.01f)
        {
            return Vector2.Zero;
        }

        // Fix depth first: lining up the lane matters more than closing distance.
        if (MathF.Abs(d.Y) > 0.3f)
        {
            d.X *= 0.6f;
        }

        return Vector2.Normalize(d);
    }

    private int TokensOn(Entity target)
    {
        int n = 0;
        var brains = World.Pool<AiBrain>();
        var comps = brains.Components;
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i].HasToken && comps[i].Target == target)
            {
                n++;
            }
        }

        return n;
    }

    private MoveId PickEnemyMove(EnemyDef def, WeaponId weapon)
    {
        if (weapon != WeaponId.None)
        {
            var w = Weapons.Get(weapon);
            return _rng.Chance(0.3f) ? w.Heavy : w.Chain[0];
        }

        return def.Attacks[_rng.Range(0, def.Attacks.Length)];
    }

    private bool TryFindWeapon(Vector3 from, out Vector3 position, out Entity entity)
    {
        position = default;
        entity = Entity.Null;
        float best = 6f;
        var pool = World.Pool<Pickup>();
        foreach (int id in pool.EntityIds)
        {
            if (pool.Get(id).Kind != PickupKind.Weapon)
            {
                continue;
            }

            var pp = World.Pool<Transform>().Get(id).Position;
            if (pp.Y > 0.3f)
            {
                continue;
            }

            float d = MathX.GroundDistance(pp, from);
            if (d < best)
            {
                best = d;
                position = pp;
                entity = World.Handle(id);
            }
        }

        return !entity.IsNull;
    }

    // ───────────────────────────── bosses ─────────────────────────────

    private void UpdateBoss()
    {
        var e = BossEntity;
        if (!World.IsAlive(e) || World.Get<Health>(e).Dead)
        {
            return;
        }

        ref var boss = ref World.Get<Boss>(e);
        ref var f = ref World.Get<Fighter>(e);
        ref var brain = ref World.Get<AiBrain>(e);
        ref var t = ref World.Get<Transform>(e);
        ref var m = ref World.Get<Motion>(e);
        var def = Bosses.Get(boss.Id);

        if (!boss.IntroDone)
        {
            if (f.State != FighterState.Cinematic)
            {
                boss.IntroDone = true;
            }

            return;
        }

        boss.PhaseTicks++;
        if (boss.ShieldTicks > 0 && --boss.ShieldTicks == 0)
        {
            Emit(GameEventKind.Blocked, Entity.Null, e, t.Position + new Vector3(0, 1.5f, 0), param: 1);
        }

        // Phase changes at 66% and 33% health: roar, shockwave, smash the arena.
        float frac = World.Get<Health>(e).Fraction;
        int phase = frac < 0.33f ? 2 : frac < 0.66f ? 1 : 0;
        if (phase > boss.Phase)
        {
            boss.Phase = phase;
            boss.PhaseTicks = 0;
            boss.MechanicTimer = 90;
            f.InvulnTicks = 100;
            f.Armor = f.ArmorMax;
            CancelActions(e, ref f);
            SetState(ref f, FighterState.Attack);
            f.Move = MoveId.None;
            StartMove(e, ref f, def.Phases[phase].Attacks[0].Move);
            SpawnHazard(HazardKind.Shockwave, TeamId.Enemies, new Vector3(t.Position.X, 0, t.Position.Z), 3.2f, 6, 20, 6);
            DestroyNearbyProps(t.Position, 7f);
            Emit(GameEventKind.BossPhase, e, Entity.Null, t.Position, phase, phase);
            Emit(GameEventKind.BossTaunt, e, Entity.Null, t.Position, param: phase);
            return;
        }

        var phaseDef = def.Phases[boss.Phase];
        if (phaseDef.Mechanic != BossMechanic.None && --boss.MechanicTimer <= 0)
        {
            boss.MechanicTimer = phaseDef.MechanicInterval;
            TriggerMechanic(e, phaseDef, t.Position);
        }

        if (brain.AttackCooldown > 0)
        {
            brain.AttackCooldown--;
        }

        if (f.HitStopTicks > 0 || f.State is not (FighterState.Idle or FighterState.Walk) || State == SessionState.Cinematic)
        {
            return;
        }

        var target = NearestPlayer(t.Position);
        if (target.IsNull)
        {
            m.Velocity.X = m.Velocity.Z = 0;
            return;
        }

        var tp = World.Get<Transform>(target).Position;
        float dx = tp.X - t.Position.X;
        float adx = MathF.Abs(dx);
        float adz = MathF.Abs(tp.Z - t.Position.Z);
        t.Facing = dx >= 0 ? 1 : -1;

        if (brain.AttackCooldown <= 0)
        {
            float total = 0;
            foreach (var a in phaseDef.Attacks)
            {
                if (adx >= a.MinRange && adx <= a.MaxRange && adz < (a.Move.Def.Has(MoveFlags.Area) || a.Move.Def.Has(MoveFlags.SpawnsHazard) ? 2.5f : 0.6f))
                {
                    total += a.Weight;
                }
            }

            if (total > 0)
            {
                float pick = _rng.NextFloat() * total;
                foreach (var a in phaseDef.Attacks)
                {
                    if (adx >= a.MinRange && adx <= a.MaxRange && adz < (a.Move.Def.Has(MoveFlags.Area) || a.Move.Def.Has(MoveFlags.SpawnsHazard) ? 2.5f : 0.6f))
                    {
                        pick -= a.Weight;
                        if (pick <= 0)
                        {
                            StartMove(e, ref f, a.Move);
                            brain.AttackCooldown = (int)(phaseDef.AttackCooldown * _rng.Range(0.8f, 1.3f) / DifficultyScale);
                            return;
                        }
                    }
                }
            }
        }

        // Close in and line up.
        var goal = new Vector3(tp.X - t.Facing * 1.6f, 0, tp.Z);
        var dir = Steer(t.Position, goal);
        if (adx < 1.8f && adz < 0.3f)
        {
            dir = Vector2.Zero;
        }

        float speed = f.Stats.WalkSpeed * phaseDef.SpeedMultiplier;
        m.Velocity.X = dir.X * speed;
        m.Velocity.Z = dir.Y * f.Stats.DepthSpeed * phaseDef.SpeedMultiplier;
        var next = dir == Vector2.Zero ? FighterState.Idle : FighterState.Walk;
        if (f.State != next)
        {
            SetState(ref f, next);
        }
    }

    private void TriggerMechanic(Entity boss, BossPhase phase, Vector3 bossPos)
    {
        switch (phase.Mechanic)
        {
            case BossMechanic.SummonMinions:
            case BossMechanic.ShadowClones:
                if (AliveEnemyCount() < 5)
                {
                    int count = phase.Mechanic == BossMechanic.ShadowClones ? 3 : 2;
                    for (int i = 0; i < count; i++)
                    {
                        float side = i % 2 == 0 ? -1 : 1;
                        var pos = phase.Mechanic == BossMechanic.ShadowClones
                            ? bossPos + new Vector3(side * 1.5f, 0, _rng.Range(-1.5f, 1.5f))
                            : new Vector3(CameraX + side * (ViewHalfWidth + 1f), 0, _rng.Range(-2.5f, 2.5f));
                        SpawnEnemy(phase.MinionKind, pos);
                    }
                }

                break;
            case BossMechanic.LightningStorm:
                ScatterHazards(HazardKind.Lightning, 4, 1.3f, 14, 55);
                break;
            case BossMechanic.FallingRocks:
                ScatterHazards(HazardKind.FallingRock, 5, 1.2f, 16, 60);
                break;
            case BossMechanic.CrateDrop:
                ScatterHazards(HazardKind.CrateDrop, 3, 1.3f, 16, 60);
                break;
            case BossMechanic.FallingTrees:
                ScatterHazards(HazardKind.FallingTree, 3, 1.6f, 20, 65);
                break;
            case BossMechanic.LavaEruption:
                for (int i = 0; i < 3; i++)
                {
                    var pos = new Vector3(CameraX + _rng.Range(-ViewHalfWidth + 2, ViewHalfWidth - 2), 0, _rng.Range(-2.5f, 2.5f));
                    SpawnHazard(HazardKind.LavaPool, TeamId.Neutral, pos, 1.6f, 6, 45, 360, persistent: true);
                }

                break;
            case BossMechanic.AirStrike:
            {
                var target = NearestPlayer(bossPos);
                var tp = World.IsAlive(target) ? World.Get<Transform>(target).Position : bossPos;
                for (int i = 0; i < 5; i++)
                {
                    SpawnHazard(HazardKind.LaserSweep, TeamId.Enemies, new Vector3(tp.X + (i - 2) * 2f, 0, tp.Z), 1.4f, 12, 40 + i * 8, 8);
                }

                break;
            }

            case BossMechanic.WildBoars:
                SpawnBoar();
                break;
            case BossMechanic.MysticShield:
                World.Get<Boss>(boss).ShieldTicks = 240;
                Emit(GameEventKind.Blocked, Entity.Null, boss, bossPos + new Vector3(0, 1.5f, 0), param: 2);
                for (int i = 0; i < 2; i++)
                {
                    SpawnEnemy(EnemyKind.BayanganPatih, bossPos + new Vector3(i == 0 ? -2f : 2f, 0, _rng.Range(-1f, 1f)));
                }

                break;
        }
    }

    private void ScatterHazards(HazardKind kind, int count, float radius, float damage, int warn)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos;
            var player = _players[i % PlayerCount];
            if (i < PlayerCount && World.IsAlive(player))
            {
                pos = World.Get<Transform>(player).Position;
                pos.Y = 0;
            }
            else
            {
                pos = new Vector3(CameraX + _rng.Range(-ViewHalfWidth + 1.5f, ViewHalfWidth - 1.5f), 0, _rng.Range(-2.8f, 2.8f));
            }

            SpawnHazard(kind, TeamId.Neutral, pos, radius, damage, warn + i * 6, 8);
        }
    }

    private void SpawnBoar()
    {
        float side = _rng.Chance(0.5f) ? -1 : 1;
        float z = _rng.Range(-2.5f, 2.5f);
        var start = new Vector3(CameraX + side * (ViewHalfWidth + 2f), 0, z);
        SpawnHazard(HazardKind.WildBoar, TeamId.Neutral, start, 0.9f, 12, 40, 150, persistent: true, velocity: new Vector3(-side * 9f, 0, 0));
    }

    private void DestroyNearbyProps(Vector3 centre, float radius)
    {
        var props = World.Pool<Prop>();
        var ids = props.EntityIds;
        for (int i = ids.Length - 1; i >= 0; i--)
        {
            var p = World.Handle(ids[i]);
            if (MathX.GroundDistance(World.Get<Transform>(p).Position, centre) < radius && !World.IsAlive(props.Get(p.Id).Carrier))
            {
                BreakProp(p);
            }
        }
    }
}
