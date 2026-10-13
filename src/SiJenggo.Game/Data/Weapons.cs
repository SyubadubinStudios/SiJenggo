namespace SiJenggo.Game.Data;

public sealed class WeaponDef
{
    public required WeaponId Id { get; init; }
    public required LocText Name { get; init; }
    public required LocText Origin { get; init; }
    public required MoveId[] Chain { get; init; }
    public required MoveId Heavy { get; init; }
    /// <summary>Hits before the weapon breaks.</summary>
    public int Durability { get; init; } = 12;
    public float ThrowDamage { get; init; } = 14;
    public float Length { get; init; } = 0.6f;
}

public static class Weapons
{
    public static readonly WeaponDef[] All =
    [
        null!,
        new() { Id = WeaponId.Golok, Name = new("Golok", "Golok"), Origin = new("Betawi", "Betawi"), Chain = [Moves.GolokA, Moves.GolokB], Heavy = Moves.GolokHeavy, Durability = 14, Length = 0.55f },
        new() { Id = WeaponId.Keris, Name = new("Keris", "Keris"), Origin = new("Jawa", "Java"), Chain = [Moves.KerisA, Moves.KerisB, Moves.KerisA, Moves.KerisB], Heavy = Moves.KerisHeavy, Durability = 18, Length = 0.4f },
        new() { Id = WeaponId.Kujang, Name = new("Kujang", "Kujang"), Origin = new("Sunda", "Sundanese"), Chain = [Moves.KujangA, Moves.KujangB, Moves.KujangA], Heavy = Moves.KujangHeavy, Durability = 15, Length = 0.4f },
        new() { Id = WeaponId.Celurit, Name = new("Celurit", "Celurit"), Origin = new("Madura", "Madura"), Chain = [Moves.CeluritA, Moves.CeluritB], Heavy = Moves.CeluritHeavy, Durability = 14, Length = 0.5f },
        new() { Id = WeaponId.Rencong, Name = new("Rencong", "Rencong"), Origin = new("Aceh", "Aceh"), Chain = [Moves.RencongA, Moves.RencongB, Moves.RencongA], Heavy = Moves.RencongHeavy, Durability = 16, Length = 0.4f },
        new() { Id = WeaponId.TombakDayak, Name = new("Tombak Dayak", "Dayak Spear"), Origin = new("Kalimantan", "Borneo"), Chain = [Moves.TombakA, Moves.TombakB], Heavy = Moves.TombakHeavy, Durability = 12, Length = 1.8f, ThrowDamage = 20 },
        new() { Id = WeaponId.Mandau, Name = new("Mandau", "Mandau"), Origin = new("Dayak", "Dayak"), Chain = [Moves.MandauA, Moves.MandauB], Heavy = Moves.MandauHeavy, Durability = 12, Length = 0.7f },
        new() { Id = WeaponId.PipaBesi, Name = new("Pipa Besi", "Iron Pipe"), Origin = new("Jalanan", "The streets"), Chain = [Moves.PipaA, Moves.PipaB], Heavy = Moves.PipaHeavy, Durability = 10, Length = 0.9f },
    ];

    public static WeaponDef Get(WeaponId id) => All[(int)id];
}
