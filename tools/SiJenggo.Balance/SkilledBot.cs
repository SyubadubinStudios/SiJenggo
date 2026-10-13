using System.Numerics;
using SiJenggo.Core;
using SiJenggo.Core.Ecs;
using SiJenggo.Game;
using SiJenggo.Game.Components;
using SiJenggo.Game.Data;

namespace SiJenggo.Balance;

/// <summary>
/// A rough stand-in for a competent human, driving a non-AI player slot through ordinary button presses.
/// Unlike the in-game companion (which face-tanks everything), it steps out of telegraphed hazards and
/// dodges enemy attacks it sees coming, but only after a reaction delay and only with probability
/// <see cref="DodgeSkill"/>, so it still gets hit. It reads the world, never writes it.
/// </summary>
internal sealed class SkilledBot
{
    /// <summary>Chance to react to any one telegraphed attack.</summary>
    public const float DodgeSkill = 0.6f;
    /// <summary>Ticks after an attack starts before the bot can react (~200 ms).</summary>
    public const int ReactionTicks = 12;

    private readonly int _slot;
    private DeterministicRandom _rng;
    private int _timer;
    private int _dodgeCooldown;
    private readonly Dictionary<int, int> _seenAttack = [];

    public SkilledBot(int slot, ulong seed)
    {
        _slot = slot;
        _rng = new DeterministicRandom(seed ^ (0xB07UL * (ulong)(slot + 1)));
    }

    public InputButtons Think(GameSession s)
    {
        _timer++;
        if (_dodgeCooldown > 0)
        {
            _dodgeCooldown--;
        }

        var e = s.PlayerEntity(_slot);
        if (!s.World.IsAlive(e))
        {
            return InputButtons.None;
        }

        var w = s.World;
        ref var f = ref w.Get<Fighter>(e);
        var pos = w.Get<Transform>(e).Position;
        float facing = w.Get<Transform>(e).Facing;

        // 1. Revive a downed partner.
        for (int i = 0; i < s.PlayerCount; i++)
        {
            var o = s.PlayerEntity(i);
            if (o != e && w.IsAlive(o) && w.Get<Fighter>(o).State == FighterState.Downed)
            {
                var op = w.Get<Transform>(o).Position;
                if (HazardsAt(s, pos) == 0)
                {
                    return MathX.GroundDistance(op, pos) < 1.2f ? InputButtons.Grab : Steer(pos, op, 0.4f);
                }
            }
        }

        float minX = s.CameraX - GameSession.ViewHalfWidth + 1f;
        float maxX = s.CameraX + GameSession.ViewHalfWidth - 1f;

        // 2. Dodge an attack that is about to land: away from it, unless that is into the screen edge.
        if (_dodgeCooldown == 0 && f.State is FighterState.Idle or FighterState.Walk && ThreatFrom(s, e, pos, out var threatPos))
        {
            _dodgeCooldown = 30;
            float away = pos.X >= threatPos.X ? 1 : -1;
            bool cornered = away > 0 ? pos.X > maxX - 1.5f : pos.X < minX + 1.5f;
            var dir = pos.Z >= 0 ? InputButtons.Down : InputButtons.Up;
            if (!cornered)
            {
                dir |= away > 0 ? InputButtons.Right : InputButtons.Left;
            }

            return InputButtons.Dodge | dir;
        }

        // 3. Step out of hazards (warning circles, lava, lightning): head for the closest spot on screen that
        //    the fewest hazards cover.
        if (HazardsAt(s, pos) > 0)
        {
            var bestGoal = pos;
            float bestScore = float.MaxValue;
            for (int ring = 1; ring <= 3; ring++)
            {
                float radius = 1.2f * ring;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45f * (MathF.PI / 180f);
                    var candidate = pos + new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * radius;
                    if (MathF.Abs(candidate.Z) > Chapters.LaneHalfDepth - 0.3f || candidate.X < minX || candidate.X > maxX)
                    {
                        continue;
                    }

                    // Depth moves are slower than walking along X, so weigh them as longer.
                    float score = HazardsAt(s, candidate) * 100f + radius * (MathF.Abs(MathF.Sin(angle)) > 0.5f ? 1.4f : 1f);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestGoal = candidate;
                    }
                }
            }

            return Steer(pos, bestGoal, 0.05f);
        }

        // 4. Fight the nearest enemy on screen.
        Entity target = Entity.Null;
        float best = float.MaxValue;
        int nearby = 0;
        foreach (int id in w.Pool<Enemy>().EntityIds)
        {
            if (w.Pool<Health>().Get(id).Dead)
            {
                continue;
            }

            var op = w.Pool<Transform>().Get(id).Position;
            if (MathF.Abs(op.X - s.CameraX) > GameSession.ViewHalfWidth + 0.5f)
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
                target = w.Handle(id);
            }
        }

        ref var rage = ref w.Get<Rage>(e);
        bool slowTick = (_timer & 7) == 0;
        if (target.IsNull)
        {
            ref var health = ref w.Get<Health>(e);
            if (health.Fraction < 0.7f && FindFood(s, pos, out var food))
            {
                return Steer(pos, food, 0.3f);
            }

            var ahead = new Vector3(s.CameraLocked ? pos.X : pos.X + 3f, 0, 0);
            return MathX.GroundDistance(ahead, pos) > 0.8f ? Steer(pos, ahead, 0.5f) : InputButtons.None;
        }

        bool isBoss = w.Has<Boss>(target);
        var tp = w.Get<Transform>(target).Position;
        float dx = tp.X - pos.X;
        float dz = tp.Z - pos.Z;
        float side = dx >= 0 ? 1 : -1;

        if (slowTick && (rage.Active ? nearby >= 2 || isBoss && best < 2.5f : rage.Meter >= 100))
        {
            return InputButtons.Rage;
        }

        if (MathF.Abs(dx) > 1.25f || MathF.Abs(dz) > 0.35f)
        {
            return Steer(pos, new Vector3(tp.X - side * 1.05f, 0, tp.Z), 0.15f);
        }

        // Do not start or extend a combo next to a pending hazard: a chain cannot be cancelled into a dodge.
        if (HazardsAt(s, pos, 1.8f) > 0)
        {
            return InputButtons.None;
        }

        var buttons = InputButtons.None;
        if (side != facing)
        {
            buttons |= side > 0 ? InputButtons.Right : InputButtons.Left;
        }

        int beat = _timer % 48;
        if (rage.Semangat >= 60 && (nearby >= 2 || isBoss) && beat == 0)
        {
            buttons |= InputButtons.Special;
        }
        else if (beat == 40)
        {
            buttons |= InputButtons.Heavy;
        }
        else if ((_timer & 3) == 0)
        {
            buttons |= InputButtons.Light;
        }

        return buttons;
    }

    /// <summary>An enemy attack whose hitbox is about to come out near us, noticed after the reaction delay.</summary>
    private bool ThreatFrom(GameSession s, Entity self, Vector3 pos, out Vector3 attackerPos)
    {
        attackerPos = default;
        var w = s.World;
        foreach (int id in w.Pool<Enemy>().EntityIds)
        {
            ref var ef = ref w.Pool<Fighter>().Get(id);
            if (ef.State != FighterState.Attack || ef.Move.IsNone)
            {
                continue;
            }

            var def = ef.Move.Def;
            if (def.Damage <= 0 || ef.MoveTick < ReactionTicks || ef.MoveTick > def.Startup + def.Active)
            {
                continue;
            }

            var ep = w.Pool<Transform>().Get(id).Position;
            float dx = MathF.Abs(ep.X - pos.X);
            float dz = MathF.Abs(ep.Z - pos.Z);
            bool threatened = def.Has(MoveFlags.Projectile)
                ? dz < 0.9f && dx < 12f
                : dx < MathF.Abs(def.HitboxOffset.X) + def.HitboxHalfSize.X + def.Lunge * 0.35f + 0.6f && dz < def.HitboxHalfSize.Z + 0.6f;
            if (!threatened)
            {
                continue;
            }

            // One roll per attack instance: either the bot reads it or it does not.
            if (_seenAttack.TryGetValue(id, out int serial) && serial == ef.AttackSerial)
            {
                continue;
            }

            _seenAttack[id] = ef.AttackSerial;
            if (_rng.Chance(DodgeSkill))
            {
                attackerPos = ep;
                return true;
            }
        }

        return false;
    }

    private static int HazardsAt(GameSession s, Vector3 pos, float margin = 0.8f)
    {
        var w = s.World;
        var pool = w.Pool<Hazard>();
        var ids = pool.EntityIds;
        var comps = pool.Components;
        int count = 0;
        for (int i = 0; i < ids.Length; i++)
        {
            if (comps[i].Team != TeamId.Players && MathX.GroundDistance(w.Pool<Transform>().Get(ids[i]).Position, pos) < comps[i].Radius + margin)
            {
                count++;
            }
        }

        return count;
    }

    private static bool FindFood(GameSession s, Vector3 from, out Vector3 position)
    {
        position = default;
        float best = 14f;
        bool found = false;
        var pool = s.World.Pool<Pickup>();
        foreach (int id in pool.EntityIds)
        {
            if (pool.Get(id).Kind is not (PickupKind.NasiPadang or PickupKind.Sate or PickupKind.EsTeh))
            {
                continue;
            }

            var pp = s.World.Pool<Transform>().Get(id).Position;
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

    private static InputButtons Steer(Vector3 from, Vector3 to, float deadZone)
    {
        var b = InputButtons.None;
        if (to.X - from.X > deadZone) b |= InputButtons.Right;
        else if (from.X - to.X > deadZone) b |= InputButtons.Left;
        if (to.Z - from.Z > deadZone) b |= InputButtons.Up;
        else if (from.Z - to.Z > deadZone) b |= InputButtons.Down;
        return b;
    }
}
