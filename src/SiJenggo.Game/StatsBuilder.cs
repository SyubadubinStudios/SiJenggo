using SiJenggo.Game.Components;
using SiJenggo.Game.Data;
using SiJenggo.Game.Persistence;

namespace SiJenggo.Game;

/// <summary>Folds character data, skill tree nodes and equipment into the numbers the simulation uses.</summary>
public static class StatsBuilder
{
    public static PlayerLoadout Build(CharacterId id, CharacterProgress? progress, bool isAi = false)
    {
        var def = Characters.Get(id);
        var s = FighterStats.Default;
        s.WalkSpeed = def.WalkSpeed;
        s.DepthSpeed = def.DepthSpeed;
        s.JumpVelocity = def.JumpVelocity;
        s.Power = def.Power;
        s.Defense = def.Defense;
        s.AttackSpeed = def.AttackSpeed;
        s.CounterWindow = def.CounterWindow;

        float maxHealth = def.MaxHealth;
        float specialCost = 1f;
        float semangatRegen = 1f;
        float damageTaken = 1f;
        MoveId jurus = MoveId.None;

        if (progress is not null)
        {
            // Level: +1.5% power, +2 health per level.
            int level = Leveling.LevelForXp(progress.Xp);
            s.Power *= 1f + 0.015f * (level - 1);
            maxHealth += 2f * (level - 1);

            foreach (string key in progress.Skills)
            {
                var node = SkillTree.Find(key);
                if (node is null)
                {
                    continue;
                }

                switch (node.Key)
                {
                    case "power.1" or "power.2" or "power.3" or "power.4": s.Power *= 1f + node.Value; break;
                    case "speed.1" or "speed.3": s.WalkSpeed *= 1f + node.Value; s.DepthSpeed *= 1f + node.Value; break;
                    case "speed.2" or "speed.4": s.AttackSpeed *= 1f + node.Value; break;
                    case "defense.1" or "defense.4": damageTaken *= 1f - node.Value; break;
                    case "defense.2": s.KnockbackResist += node.Value; break;
                    case "defense.3": maxHealth += node.Value; break;
                    case "technique.1": s.ComboWindow *= 1f + node.Value; break;
                    case "technique.2": s.CounterWindow *= 1f + node.Value; break;
                    case "technique.3": semangatRegen *= 1f + node.Value; break;
                    case "technique.4": specialCost *= 1f - node.Value; break;
                    case "rage.1" or "rage.3": s.RageGain *= 1f + node.Value; break;
                    case "rage.2" or "rage.4": s.RageDuration *= 1f + node.Value; break;
                }
            }

            foreach (var equipped in progress.Equipped)
            {
                var item = Equipment.Get(equipped);
                if (item is null)
                {
                    continue;
                }

                s.Power *= 1f + item.Power;
                damageTaken *= 1f - item.Defense;
                s.WalkSpeed *= 1f + item.Speed;
                s.AttackSpeed *= 1f + item.Speed * 0.5f;
                s.RageGain *= 1f + item.RageGain;
                maxHealth += item.Health;
            }

            jurus = progress.EquippedJurus;
        }

        s.Defense /= Math.Max(0.3f, damageTaken);
        return new PlayerLoadout
        {
            Character = id,
            IsAi = isAi,
            Stats = s,
            MaxHealth = maxHealth,
            Jurus = jurus,
            SpecialCostMultiplier = specialCost,
            SemangatRegen = semangatRegen,
        };
    }
}
