using SiJenggo.Game.Components;

namespace SiJenggo.Game.Data;

public readonly record struct EnemyGroup(EnemyKind Kind, int Count);

public sealed class WaveDef
{
    /// <summary>The camera locks when the lead player passes this X.</summary>
    public required float TriggerX { get; init; }
    public required EnemyGroup[] Groups { get; init; }
    /// <summary>Most enemies on screen at once; the rest stream in as others fall.</summary>
    public int MaxAlive { get; init; } = 5;
}

public readonly record struct PropPlacement(PropKind Kind, float X, float Z);

public readonly record struct PickupPlacement(PickupKind Kind, float X, float Z, WeaponId Weapon = WeaponId.None);

public sealed class AreaDef
{
    public required LocText Name { get; init; }
    public required float StartX { get; init; }
    public required float EndX { get; init; }
    /// <summary>Set-dressing key the renderer understands (e.g. "gang", "terminal", "pasar").</summary>
    public required string Dressing { get; init; }
}

public sealed class ChapterDef
{
    public required int Index { get; init; }
    public required LocText Name { get; init; }
    public required LocText Subtitle { get; init; }
    public required EnvironmentTheme Theme { get; init; }
    public required WeatherKind Weather { get; init; }
    public required MusicStyle Music { get; init; }
    public required LocText[] Story { get; init; }
    public required AreaDef[] Areas { get; init; }
    public required WaveDef[] Waves { get; init; }
    public PropPlacement[] Props { get; init; } = [];
    public PickupPlacement[] Pickups { get; init; } = [];
    public required BossId Boss { get; init; }
    /// <summary>X where the boss arena starts; the stage ends at <see cref="Length"/>.</summary>
    public required float BossArenaX { get; init; }
    public required float Length { get; init; }
    /// <summary>Ambient hazard that keeps firing during the chapter (storm lightning, rocks).</summary>
    public HazardKind? AmbientHazard { get; init; }
    public int AmbientHazardInterval { get; init; } = 600;
    public LocText JurusName { get; init; }
    public MoveId JurusReward => Moves.JurusForChapter(Index);
}

public static class Chapters
{
    /// <summary>Walkable depth: Z stays within ±this.</summary>
    public const float LaneHalfDepth = 3.2f;

    private static WaveDef W(float x, int maxAlive, params EnemyGroup[] groups) => new() { TriggerX = x, Groups = groups, MaxAlive = maxAlive };
    private static EnemyGroup G(EnemyKind k, int n) => new(k, n);

    /// <summary>Played in the engine after the last chapter is cleared in Story mode.</summary>
    public static readonly LocText[] Epilogue =
    [
        new("Menara Garuda Hitam runtuh. Untuk pertama kalinya dalam bertahun-tahun, kota itu tidur tanpa rasa takut.", "The Garuda Hitam tower falls. For the first time in years, the city sleeps without fear."),
        new("Bima kembali melatih anak-anak kampung, Mbak Rani menyerahkan berkas terakhirnya, dan Ustad Ucup membuka dapur umum.", "Bima goes back to training the village kids, Mbak Rani hands in her last file, and Ustad Ucup opens a public kitchen."),
        new("Dan Jenggo? Dia duduk di warung nasi padang langganannya. Preman berhati malaikat itu akhirnya boleh pensiun.", "And Jenggo? He sits at his favourite nasi padang stall. The thug with an angel's heart can finally retire."),
    ];

    public static readonly ChapterDef[] All =
    [
        new()
        {
            Index = 0,
            Name = new("Jakarta Kumuh", "Jakarta Slums"),
            Subtitle = new("Bab 1 — Kampung yang terbakar", "Chapter 1 — The burning village"),
            Theme = EnvironmentTheme.JakartaKumuh,
            Weather = WeatherKind.NightNeon,
            Music = MusicStyle.FunkRock,
            Story =
            [
                new("Malam itu kampung Jenggo dilalap api. Orang-orang Bang Kodir tertawa di ujung gang.", "That night Jenggo's village went up in flames. Bang Kodir's men laughed at the end of the alley."),
                new("Jenggo menggulung lengan bajunya. Sudah lama dia tidak berkelahi. Malam ini, dia akan ingat caranya.", "Jenggo rolls up his sleeves. It has been a long time since he fought. Tonight, he will remember how."),
            ],
            Areas =
            [
                new() { Name = new("Gang Sempit", "Narrow Alley"), StartX = 0, EndX = 45, Dressing = "gang" },
                new() { Name = new("Terminal Liar", "Wildcat Bus Terminal"), StartX = 45, EndX = 90, Dressing = "terminal" },
                new() { Name = new("Pasar Malam", "Night Market"), StartX = 90, EndX = 135, Dressing = "pasar" },
                new() { Name = new("Rumah Susun", "Tenement Blocks"), StartX = 135, EndX = 170, Dressing = "rusun" },
            ],
            Waves =
            [
                W(14, 3, G(EnemyKind.PremanGang, 4)),
                W(38, 4, G(EnemyKind.PremanGang, 4), G(EnemyKind.PremanPisau, 2)),
                W(64, 4, G(EnemyKind.PremanGang, 3), G(EnemyKind.PelemparBotol, 2), G(EnemyKind.PremanPisau, 2)),
                W(96, 5, G(EnemyKind.TukangPukul, 1), G(EnemyKind.PremanGang, 5)),
                W(122, 5, G(EnemyKind.PremanPisau, 3), G(EnemyKind.PelemparBotol, 2), G(EnemyKind.TukangPukul, 1)),
            ],
            Props =
            [
                new(PropKind.TongSampah, 8, 2.4f), new(PropKind.Kursi, 18, -1.5f), new(PropKind.Motor, 30, 2.6f), new(PropKind.PetiKayu, 42, -2.2f),
                new(PropKind.Gerobak, 58, 2.2f), new(PropKind.Kursi, 70, -2f), new(PropKind.TongSampah, 84, 2.5f), new(PropKind.Gerobak, 100, -2f),
                new(PropKind.PetiKayu, 112, 2.4f), new(PropKind.Bambu, 128, -2.4f), new(PropKind.Pagar, 140, 2.8f), new(PropKind.Kursi, 152, -1.8f),
            ],
            Pickups = [new(PickupKind.Weapon, 26, 0.5f, WeaponId.PipaBesi), new(PickupKind.Weapon, 76, -1f, WeaponId.Golok), new(PickupKind.Sate, 110, 1f), new(PickupKind.Weapon, 118, -0.5f, WeaponId.Celurit)],
            Boss = BossId.BangKodir,
            BossArenaX = 148,
            Length = 170,
            JurusName = new("Silat Betawi", "Betawi Silat"),
        },
        new()
        {
            Index = 1,
            Name = new("Pelabuhan Tanjung Harapan", "Tanjung Harapan Harbour"),
            Subtitle = new("Bab 2 — Muatan gelap", "Chapter 2 — Dark cargo"),
            Theme = EnvironmentTheme.Pelabuhan,
            Weather = WeatherKind.TropicalRain,
            Music = MusicStyle.MetalBlues,
            Story =
            [
                new("Bang Kodir menyebut satu nama: Kapten Badai. Semua barang haram Garuda Hitam lewat pelabuhannya.", "Bang Kodir gave up one name: Captain Badai. Every piece of Garuda Hitam contraband moves through his harbour."),
                new("Di bawah hujan, Bima, sahabat lama Jenggo, menunggu di gerbang pelabuhan.", "Under the rain, Bima, Jenggo's old friend, waits at the harbour gate."),
            ],
            Areas =
            [
                new() { Name = new("Tumpukan Kontainer", "Container Stacks"), StartX = 0, EndX = 60, Dressing = "kontainer" },
                new() { Name = new("Gudang Gelap", "Dark Warehouse"), StartX = 60, EndX = 120, Dressing = "gudang" },
                new() { Name = new("Kapal Besar", "The Freighter"), StartX = 120, EndX = 175, Dressing = "kapal" },
            ],
            Waves =
            [
                W(16, 4, G(EnemyKind.PremanGang, 3), G(EnemyKind.PremanPisau, 2)),
                W(42, 4, G(EnemyKind.PelemparBotol, 2), G(EnemyKind.PremanPisau, 3)),
                W(70, 5, G(EnemyKind.TukangPukul, 2), G(EnemyKind.PremanGang, 4)),
                W(98, 5, G(EnemyKind.JagoanSilat, 3), G(EnemyKind.PremanPisau, 3)),
                W(128, 5, G(EnemyKind.TukangPukul, 1), G(EnemyKind.JagoanSilat, 2), G(EnemyKind.PelemparBotol, 3)),
            ],
            Props =
            [
                new(PropKind.PetiKayu, 10, 2.4f), new(PropKind.PetiKayu, 11.5f, 2.4f), new(PropKind.TongSampah, 28, -2.4f), new(PropKind.PetiKayu, 48, 2f),
                new(PropKind.Gerobak, 66, -2f), new(PropKind.PetiKayu, 82, 2.6f), new(PropKind.Pagar, 104, -2.8f), new(PropKind.PetiKayu, 132, 2.2f), new(PropKind.TongSampah, 146, -2.2f),
            ],
            Pickups = [new(PickupKind.Weapon, 36, 0f, WeaponId.Celurit), new(PickupKind.Weapon, 90, 1f, WeaponId.Rencong), new(PickupKind.NasiPadang, 140, 0f)],
            Boss = BossId.KaptenBadai,
            BossArenaX = 152,
            Length = 175,
            JurusName = new("Debus Banten", "Banten Debus"),
        },
        new()
        {
            Index = 2,
            Name = new("Pantai Selatan", "The Southern Shore"),
            Subtitle = new("Bab 3 — Badai di laut selatan", "Chapter 3 — Storm on the southern sea"),
            Theme = EnvironmentTheme.PantaiSelatan,
            Weather = WeatherKind.Thunderstorm,
            Music = MusicStyle.SurfRock,
            Story =
            [
                new("Muatan Kapten Badai menuju pantai selatan, ke tempat seorang penyihir menyimpan artefak curian.", "Captain Badai's cargo was bound for the southern shore, where a witch hoards stolen artefacts."),
                new("Langit menggelap. Ombak setinggi rumah. Mbak Rani muncul dari balik karang: \"Aku tahu jalan masuknya.\"", "The sky darkens. Waves as tall as houses. Mbak Rani steps out from behind the reef: \"I know the way in.\""),
            ],
            Areas =
            [
                new() { Name = new("Pesisir Berkarang", "Rocky Coast"), StartX = 0, EndX = 70, Dressing = "pantai" },
                new() { Name = new("Desa Nelayan", "Fishing Village"), StartX = 70, EndX = 130, Dressing = "nelayan" },
                new() { Name = new("Tanjung Petir", "Lightning Cape"), StartX = 130, EndX = 175, Dressing = "tanjung" },
            ],
            Waves =
            [
                W(18, 4, G(EnemyKind.JagoanSilat, 2), G(EnemyKind.PremanGang, 3)),
                W(46, 5, G(EnemyKind.PelemparBotol, 3), G(EnemyKind.PremanPisau, 3)),
                W(80, 5, G(EnemyKind.JagoanSilat, 3), G(EnemyKind.TukangPukul, 1), G(EnemyKind.PremanGang, 2)),
                W(110, 5, G(EnemyKind.NinjaGaruda, 2), G(EnemyKind.JagoanSilat, 3)),
            ],
            Props = [new(PropKind.BatuBesar, 12, 2.6f), new(PropKind.Bambu, 34, -2.4f), new(PropKind.Gerobak, 74, 2.2f), new(PropKind.PetiKayu, 92, -2.2f), new(PropKind.Bambu, 120, 2.4f), new(PropKind.BatuBesar, 140, -2.6f)],
            Pickups = [new(PickupKind.Weapon, 30, 0f, WeaponId.Keris), new(PickupKind.EsTeh, 64, 1f), new(PickupKind.Weapon, 100, -1f, WeaponId.Kujang), new(PickupKind.Sate, 136, 0f)],
            Boss = BossId.NyiAnginLaut,
            BossArenaX = 150,
            Length = 175,
            AmbientHazard = HazardKind.Lightning,
            AmbientHazardInterval = 420,
            JurusName = new("Pencak Jawa Barat", "West Java Pencak"),
        },
        new()
        {
            Index = 3,
            Name = new("Hutan Kalimantan", "Borneo Rainforest"),
            Subtitle = new("Bab 4 — Suara gergaji di tengah kabut", "Chapter 4 — Chainsaws in the mist"),
            Theme = EnvironmentTheme.HutanKalimantan,
            Weather = WeatherKind.Mist,
            Music = MusicStyle.TribalPercussion,
            Story =
            [
                new("Uang Garuda Hitam mengalir dari kayu curian. Di jantung Kalimantan, hutan ditebang siang dan malam.", "Garuda Hitam's money flows from stolen timber. In the heart of Borneo, the forest is cut day and night."),
                new("Para tetua Dayak menitipkan sebilah mandau: \"Lindungi hutan kami.\"", "The Dayak elders hand over a mandau: \"Protect our forest.\""),
            ],
            Areas =
            [
                new() { Name = new("Hutan Berkabut", "Misty Forest"), StartX = 0, EndX = 70, Dressing = "hutan" },
                new() { Name = new("Pohon Raksasa", "Giant Trees"), StartX = 70, EndX = 130, Dressing = "raksasa" },
                new() { Name = new("Kamp Penebangan", "Logging Camp"), StartX = 130, EndX = 180, Dressing = "kamp" },
            ],
            Waves =
            [
                W(18, 5, G(EnemyKind.PremanGang, 3), G(EnemyKind.JagoanSilat, 2)),
                W(46, 5, G(EnemyKind.TukangPukul, 2), G(EnemyKind.PelemparBotol, 2)),
                W(78, 5, G(EnemyKind.NinjaGaruda, 2), G(EnemyKind.JagoanSilat, 3)),
                W(108, 6, G(EnemyKind.TukangPukul, 2), G(EnemyKind.PremanPisau, 4)),
                W(134, 6, G(EnemyKind.NinjaGaruda, 3), G(EnemyKind.PelemparBotol, 2)),
            ],
            Props = [new(PropKind.Bambu, 10, 2.4f), new(PropKind.BatuBesar, 30, -2.6f), new(PropKind.PetiKayu, 58, 2.2f), new(PropKind.Bambu, 86, -2.2f), new(PropKind.Gerobak, 120, 2.4f), new(PropKind.PetiKayu, 142, -2.4f)],
            Pickups = [new(PickupKind.Weapon, 24, 0f, WeaponId.Mandau), new(PickupKind.Weapon, 70, 1f, WeaponId.TombakDayak), new(PickupKind.KopiTubruk, 100, 0f), new(PickupKind.NasiPadang, 146, 0f)],
            Boss = BossId.RajaPenebang,
            BossArenaX = 155,
            Length = 180,
            AmbientHazard = HazardKind.WildBoar,
            AmbientHazardInterval = 900,
            JurusName = new("Silat Minang", "Minang Silat"),
        },
        new()
        {
            Index = 4,
            Name = new("Gunung Berapi", "The Volcano"),
            Subtitle = new("Bab 5 — Tambang di kawah", "Chapter 5 — A mine in the crater"),
            Theme = EnvironmentTheme.GunungBerapi,
            Weather = WeatherKind.VolcanicAsh,
            Music = MusicStyle.MetalBlues,
            Story =
            [
                new("Tambang ilegal di lereng gunung api menggali sesuatu yang lebih tua dari emas.", "An illegal mine on the volcano's slope is digging for something older than gold."),
                new("Abu turun seperti salju. Tanah bergetar. Di atas sana, Jagal Merapi menunggu.", "Ash falls like snow. The ground trembles. Up there, the Merapi Butcher waits."),
            ],
            Areas =
            [
                new() { Name = new("Lereng Abu", "Ash Slope"), StartX = 0, EndX = 70, Dressing = "lereng" },
                new() { Name = new("Tambang Ilegal", "Illegal Mine"), StartX = 70, EndX = 130, Dressing = "tambang" },
                new() { Name = new("Bibir Kawah", "Crater Rim"), StartX = 130, EndX = 180, Dressing = "kawah" },
            ],
            Waves =
            [
                W(18, 5, G(EnemyKind.TukangPukul, 1), G(EnemyKind.PremanPisau, 4)),
                W(48, 5, G(EnemyKind.NinjaGaruda, 2), G(EnemyKind.PelemparBotol, 3)),
                W(80, 6, G(EnemyKind.TukangPukul, 2), G(EnemyKind.JagoanSilat, 3)),
                W(112, 6, G(EnemyKind.NinjaGaruda, 3), G(EnemyKind.TukangPukul, 2)),
                W(138, 6, G(EnemyKind.RobotGaruda, 1), G(EnemyKind.NinjaGaruda, 3)),
            ],
            Props = [new(PropKind.BatuBesar, 12, 2.6f), new(PropKind.PetiKayu, 36, -2.4f), new(PropKind.Gerobak, 74, 2.4f), new(PropKind.TongSampah, 96, -2.4f), new(PropKind.BatuBesar, 126, 2.6f)],
            Pickups = [new(PickupKind.Weapon, 30, 0f, WeaponId.Kujang), new(PickupKind.Sate, 70, 0f), new(PickupKind.Weapon, 104, -1f, WeaponId.PipaBesi), new(PickupKind.NasiPadang, 150, 0f)],
            Boss = BossId.JagalMerapi,
            BossArenaX = 156,
            Length = 180,
            AmbientHazard = HazardKind.FallingRock,
            AmbientHazardInterval = 360,
            JurusName = new("Silat Bugis", "Bugis Silat"),
        },
        new()
        {
            Index = 5,
            Name = new("Candi Kuno Nusantara", "Ancient Archipelago Temple"),
            Subtitle = new("Bab 6 — Penjaga seribu tahun", "Chapter 6 — The thousand-year guardian"),
            Theme = EnvironmentTheme.CandiKuno,
            Weather = WeatherKind.Mist,
            Music = MusicStyle.GamelanMetal,
            Story =
            [
                new("Yang digali dari kawah adalah kunci sebuah candi tersembunyi. Garuda Hitam ingin pusakanya.", "What they dug from the crater was the key to a hidden temple. Garuda Hitam wants its heirloom."),
                new("Ustad Ucup sudah menunggu di gerbang candi. \"Tempat suci jangan dijual, Jenggo.\"", "Ustad Ucup is already waiting at the temple gate. \"Sacred places are not for sale, Jenggo.\""),
            ],
            Areas =
            [
                new() { Name = new("Gerbang Candi", "Temple Gate"), StartX = 0, EndX = 60, Dressing = "gerbang" },
                new() { Name = new("Lorong Relief", "Relief Corridor"), StartX = 60, EndX = 125, Dressing = "relief" },
                new() { Name = new("Pelataran Stupa", "Stupa Terrace"), StartX = 125, EndX = 180, Dressing = "stupa" },
            ],
            Waves =
            [
                W(16, 5, G(EnemyKind.NinjaGaruda, 2), G(EnemyKind.JagoanSilat, 3)),
                W(46, 6, G(EnemyKind.TukangPukul, 2), G(EnemyKind.NinjaGaruda, 2), G(EnemyKind.PelemparBotol, 2)),
                W(80, 6, G(EnemyKind.JagoanSilat, 4), G(EnemyKind.NinjaGaruda, 2)),
                W(110, 6, G(EnemyKind.RobotGaruda, 1), G(EnemyKind.NinjaGaruda, 3), G(EnemyKind.TukangPukul, 1)),
                W(136, 6, G(EnemyKind.BayanganPatih, 6), G(EnemyKind.NinjaGaruda, 2)),
            ],
            Props = [new(PropKind.BatuBesar, 14, 2.6f), new(PropKind.PetiKayu, 40, -2.4f), new(PropKind.BatuBesar, 72, -2.6f), new(PropKind.Bambu, 98, 2.4f), new(PropKind.BatuBesar, 130, 2.6f)],
            Pickups = [new(PickupKind.Weapon, 26, 0f, WeaponId.Keris), new(PickupKind.EsTeh, 60, 1f), new(PickupKind.Weapon, 94, 0f, WeaponId.Kujang), new(PickupKind.NasiPadang, 150, 0f)],
            Boss = BossId.PatihAbadi,
            BossArenaX = 156,
            Length = 180,
            AmbientHazard = HazardKind.SpearTrap,
            AmbientHazardInterval = 480,
            JurusName = new("Silat Bali", "Bali Silat"),
        },
        new()
        {
            Index = 6,
            Name = new("Kota Futuristik Garuda Hitam", "Garuda Hitam's Future City"),
            Subtitle = new("Bab 7 — Menara di atas awan", "Chapter 7 — The tower above the clouds"),
            Theme = EnvironmentTheme.KotaFuturistik,
            Weather = WeatherKind.NightNeon,
            Music = MusicStyle.SymphonicMetal,
            Story =
            [
                new("Semua jalan berujung di sini: kota baru yang dibangun dari tanah rakyat yang dirampas.", "Every road ends here: a new city built on land stolen from the people."),
                new("Empat petarung. Satu menara. Garuda Hitam harus tumbang malam ini.", "Four fighters. One tower. Garuda Hitam falls tonight."),
            ],
            Areas =
            [
                new() { Name = new("Jalan Layang Neon", "Neon Skyway"), StartX = 0, EndX = 70, Dressing = "neon" },
                new() { Name = new("Pabrik Robot", "Robot Factory"), StartX = 70, EndX = 135, Dressing = "pabrik" },
                new() { Name = new("Puncak Menara Garuda", "Garuda Tower Summit"), StartX = 135, EndX = 190, Dressing = "menara" },
            ],
            Waves =
            [
                W(16, 6, G(EnemyKind.NinjaGaruda, 3), G(EnemyKind.PremanPisau, 3)),
                W(46, 6, G(EnemyKind.RobotGaruda, 2), G(EnemyKind.NinjaGaruda, 2)),
                W(78, 6, G(EnemyKind.TukangPukul, 2), G(EnemyKind.JagoanSilat, 3), G(EnemyKind.PelemparBotol, 2)),
                W(108, 6, G(EnemyKind.RobotGaruda, 2), G(EnemyKind.NinjaGaruda, 4)),
                W(140, 7, G(EnemyKind.RobotGaruda, 2), G(EnemyKind.NinjaGaruda, 3), G(EnemyKind.TukangPukul, 2)),
            ],
            Props = [new(PropKind.Motor, 12, 2.6f), new(PropKind.TongSampah, 34, -2.4f), new(PropKind.PetiKayu, 64, 2.4f), new(PropKind.Pagar, 92, -2.8f), new(PropKind.Motor, 122, 2.6f), new(PropKind.PetiKayu, 150, -2.4f)],
            Pickups = [new(PickupKind.Weapon, 28, 0f, WeaponId.Rencong), new(PickupKind.Weapon, 72, 1f, WeaponId.Mandau), new(PickupKind.NasiPadang, 104, 0f), new(PickupKind.KopiTubruk, 132, 0f), new(PickupKind.NasiPadang, 162, 0f)],
            Boss = BossId.JenderalGaruda,
            BossArenaX = 166,
            Length = 190,
            AmbientHazard = HazardKind.LaserSweep,
            AmbientHazardInterval = 720,
        },
    ];

    public static ChapterDef Get(int index) => All[index];
}
