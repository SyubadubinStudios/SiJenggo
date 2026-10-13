namespace SiJenggo.Game.Data;

public enum AiArchetype : byte
{
    Brawler,
    Ranged,
    Brute,
    Agile,
}

public sealed class EnemyDef
{
    public required EnemyKind Kind { get; init; }
    public required LocText Name { get; init; }
    public AiArchetype Archetype { get; init; }
    public float Health { get; init; } = 40;
    public float WalkSpeed { get; init; } = 3f;
    public float Power { get; init; } = 1f;
    public float Weight { get; init; } = 1f;
    public float Armor { get; init; }
    public float Aggression { get; init; } = 0.5f;
    public float Scale { get; init; } = 1f;
    public required MoveId[] Attacks { get; init; }
    public WeaponId StartingWeapon { get; init; }
    public int Score { get; init; } = 100;
    public int Xp { get; init; } = 10;
    /// <summary>Chance to drop a pickup when knocked out.</summary>
    public float DropChance { get; init; } = 0.15f;
}

public static class Enemies
{
    public static readonly EnemyDef[] All =
    [
        new() { Kind = EnemyKind.PremanGang, Name = new("Preman Gang", "Alley Thug"), Health = 38, WalkSpeed = 3f, Attacks = [Moves.PremanJab, Moves.PremanJab, Moves.PremanKick], Score = 100, Xp = 8 },
        new() { Kind = EnemyKind.PremanPisau, Name = new("Preman Pisau", "Knife Thug"), Health = 34, WalkSpeed = 3.4f, Aggression = 0.65f, Attacks = [Moves.PisauSlash, Moves.PisauLunge], Score = 150, Xp = 10 },
        new() { Kind = EnemyKind.TukangPukul, Name = new("Tukang Pukul", "Enforcer"), Archetype = AiArchetype.Brute, Health = 90, WalkSpeed = 2.3f, Power = 1.2f, Weight = 2f, Armor = 2, Aggression = 0.45f, Scale = 1.3f, Attacks = [Moves.BruteSwing, Moves.BruteSlam], Score = 300, Xp = 22, DropChance = 0.35f },
        new() { Kind = EnemyKind.JagoanSilat, Name = new("Jagoan Silat", "Silat Bruiser"), Archetype = AiArchetype.Agile, Health = 50, WalkSpeed = 4f, Aggression = 0.7f, Attacks = [Moves.SilatEnemyKick, Moves.SilatEnemyKick, Moves.SilatEnemySweep], Score = 200, Xp = 14 },
        new() { Kind = EnemyKind.PelemparBotol, Name = new("Pelempar Botol", "Bottle Tosser"), Archetype = AiArchetype.Ranged, Health = 30, WalkSpeed = 3.2f, Aggression = 0.4f, Attacks = [Moves.BottleThrow, Moves.BottleSlap], Score = 150, Xp = 10 },
        new() { Kind = EnemyKind.NinjaGaruda, Name = new("Ninja Garuda", "Garuda Ninja"), Archetype = AiArchetype.Agile, Health = 60, WalkSpeed = 4.6f, Power = 1.1f, Aggression = 0.8f, Attacks = [Moves.NinjaSlash, Moves.NinjaDash, Moves.NinjaShuriken], Score = 300, Xp = 20 },
        new() { Kind = EnemyKind.RobotGaruda, Name = new("Robot Garuda", "Garuda Mech"), Archetype = AiArchetype.Brute, Health = 120, WalkSpeed = 2.4f, Power = 1.25f, Weight = 2.5f, Armor = 3, Aggression = 0.5f, Scale = 1.35f, Attacks = [Moves.RobotPunch, Moves.RobotLaser], Score = 450, Xp = 30, DropChance = 0.4f },
        new() { Kind = EnemyKind.BayanganPatih, Name = new("Bayangan Patih", "Patih's Shadow"), Archetype = AiArchetype.Agile, Health = 26, WalkSpeed = 4.2f, Aggression = 0.85f, Attacks = [Moves.ShadowSlash], Score = 50, Xp = 4, DropChance = 0f },
    ];

    public static EnemyDef Get(EnemyKind kind) => All[(int)kind];
}
