using SiJenggo.Game;

namespace SiJenggo.App.Ui;

/// <summary>Every interface string (Indonesian + English inline, other languages via assets/lang tables). Game content (moves, bosses, story) carries its own <see cref="LocText"/>.</summary>
public static class Strings
{
    private static Language s_language = Language.Indonesian;

    /// <summary>Current UI language. Setting a table-based language loads <c>assets/lang/*.json</c> on first use.</summary>
    public static Language Language
    {
        get => s_language;
        set
        {
            if (Localization.IsTableBased(value))
            {
                EnsureLanguagesLoaded();
            }

            s_language = value;
        }
    }

    public static string T(LocText text) => text[Language];

    private static readonly Dictionary<string, LocText> s_text = new()
    {
        ["title.tagline"] = new("Ketika preman terakhir memilih melindungi rakyat, seluruh Garuda Hitam harus tumbang.", "When the last thug chooses to protect the people, all of Garuda Hitam must fall."),
        ["credit"] = new("Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios", "Made by Ariana Mischa Fadhila of Syubadubin Studios"),
        ["menu.story"] = new("Mode Cerita", "Story Mode"),
        ["menu.arcade"] = new("Arcade", "Arcade"),
        ["menu.survival"] = new("Bertahan", "Survival"),
        ["menu.bossrush"] = new("Boss Rush", "Boss Rush"),
        ["menu.challenge"] = new("Tantangan", "Challenges"),
        ["menu.online"] = new("Main Online", "Play Online"),
        ["menu.progress"] = new("Jurus & Perlengkapan", "Skills & Gear"),
        ["menu.settings"] = new("Pengaturan", "Settings"),
        ["menu.credits"] = new("Kredit", "Credits"),
        ["menu.quit"] = new("Keluar", "Quit"),
        ["menu.hint"] = new("Panah / stik untuk memilih · Enter / A untuk masuk", "Arrows / stick to choose · Enter / A to confirm"),
        ["back"] = new("Kembali", "Back"),
        ["start"] = new("Mulai bertarung", "Start the fight"),
        ["setup.players"] = new("Siapa yang turun ke jalan?", "Who hits the streets?"),
        ["setup.p1"] = new("Pemain 1", "Player 1"),
        ["setup.p2"] = new("Pemain 2", "Player 2"),
        ["setup.p2.none"] = new("Sendirian", "Solo"),
        ["setup.p2.human"] = new("Teman (lokal)", "Friend (local)"),
        ["setup.p2.ai"] = new("Partner AI", "AI partner"),
        ["setup.chapter"] = new("Pilih bab", "Choose a chapter"),
        ["setup.arena"] = new("Pilih arena", "Choose an arena"),
        ["setup.difficulty"] = new("Tingkat kesulitan", "Difficulty"),
        ["setup.locked"] = new("Terkunci", "Locked"),
        ["setup.challenge"] = new("Pilih tantangan", "Choose a challenge"),
        ["setup.cleared"] = new("Selesai", "Cleared"),
        ["diff.Santai"] = new("Santai", "Relaxed"),
        ["diff.Normal"] = new("Normal", "Normal"),
        ["diff.Jagoan"] = new("Jagoan", "Champion"),
        ["stat.power"] = new("Tenaga", "Power"),
        ["stat.speed"] = new("Kecepatan", "Speed"),
        ["stat.defense"] = new("Pertahanan", "Defense"),
        ["stat.technique"] = new("Teknik", "Technique"),
        ["hud.rage"] = new("RAGE NUSANTARA", "RAGE NUSANTARA"),
        ["hud.rage.ready"] = new("RAGE SIAP — tekan O", "RAGE READY — press O"),
        ["hud.rage.on"] = new("MURKA! — O lagi untuk jurus pamungkas", "FURY! — O again for the ultimate"),
        ["hud.semangat"] = new("Semangat", "Spirit"),
        ["hud.go"] = new("MAJU", "GO"),
        ["hud.hits"] = new("PUKULAN", "HITS"),
        ["hud.lives"] = new("Nyawa", "Lives"),
        ["hud.down"] = new("TUMBANG — rekan tahan U untuk membangunkan", "DOWN — partner hold U to revive"),
        ["hud.wave"] = new("Gelombang", "Wave"),
        ["hud.spirit.empty"] = new("Semangat habis", "Out of spirit"),
        ["hud.waiting"] = new("Menunggu rekan online…", "Waiting for online partner…"),
        ["pause.title"] = new("Jeda", "Paused"),
        ["pause.resume"] = new("Lanjutkan", "Resume"),
        ["pause.restart"] = new("Ulangi tahap", "Restart stage"),
        ["pause.controls"] = new("Kontrol", "Controls"),
        ["pause.quit"] = new("Keluar ke menu", "Quit to menu"),
        ["result.clear"] = new("TAHAP SELESAI", "STAGE CLEAR"),
        ["result.over"] = new("TUMBANG", "KNOCKED OUT"),
        ["result.score"] = new("Skor", "Score"),
        ["result.ko"] = new("KO", "KOs"),
        ["result.combo"] = new("Kombo terbaik", "Best combo"),
        ["result.time"] = new("Waktu", "Time"),
        ["result.xp"] = new("XP didapat", "XP earned"),
        ["result.level"] = new("Naik level!", "Level up!"),
        ["result.unlocks"] = new("Terbuka", "Unlocked"),
        ["result.achievements"] = new("Pencapaian baru", "New achievements"),
        ["result.next"] = new("Tahap berikutnya", "Next stage"),
        ["result.retry"] = new("Coba lagi", "Try again"),
        ["result.menu"] = new("Ke menu", "To the menu"),
        ["result.waves"] = new("Gelombang bertahan", "Waves survived"),
        ["story.continue"] = new("Lanjut", "Continue"),
        ["story.skip"] = new("Lewati", "Skip"),
        ["cutscene.hint"] = new("J / Enter / A: lanjut  ·  Esc / Start: lewati", "J / Enter / A: continue  ·  Esc / Start: skip"),
        ["result.nextchapter"] = new("Bab berikutnya", "Next chapter"),
        ["result.epilogue"] = new("Epilog", "Epilogue"),
        ["progress.title"] = new("Jurus & Perlengkapan", "Skills & Gear"),
        ["progress.level"] = new("Level", "Level"),
        ["progress.points"] = new("Poin jurus", "Skill points"),
        ["progress.tree"] = new("Pohon jurus", "Skill tree"),
        ["progress.gear"] = new("Perlengkapan", "Gear"),
        ["progress.jurus"] = new("Jurus daerah (Spesial + bawah)", "Regional style (Special + down)"),
        ["progress.none"] = new("Belum ada", "None yet"),
        ["progress.cost"] = new("biaya", "cost"),
        ["settings.title"] = new("Pengaturan", "Settings"),
        ["settings.language"] = new("Bahasa", "Language"),
        ["settings.master"] = new("Volume utama", "Master volume"),
        ["settings.music"] = new("Musik", "Music"),
        ["settings.sfx"] = new("Efek suara", "Sound effects"),
        ["settings.quality"] = new("Kualitas grafis", "Graphics quality"),
        ["settings.shake"] = new("Guncangan layar", "Screen shake"),
        ["settings.fullscreen"] = new("Layar penuh", "Fullscreen"),
        ["settings.fps"] = new("Tampilkan FPS", "Show FPS"),
        ["settings.on"] = new("Nyala", "On"),
        ["settings.off"] = new("Mati", "Off"),
        ["q.0"] = new("Rendah", "Low"),
        ["q.1"] = new("Sedang", "Medium"),
        ["q.2"] = new("Tinggi", "High"),
        ["q.3"] = new("Ultra", "Ultra"),
        ["online.title"] = new("Main Online", "Play Online"),
        ["online.host"] = new("Buat ruang (host)", "Host a room"),
        ["online.join"] = new("Gabung ke host", "Join a host"),
        ["online.address"] = new("Alamat host", "Host address"),
        ["online.port"] = new("Port", "Port"),
        ["online.netcode"] = new("Netcode", "Netcode"),
        ["online.lockstep"] = new("Lockstep (jeda input)", "Lockstep (input delay)"),
        ["online.rollback"] = new("Rollback (prediksi)", "Rollback (prediction)"),
        ["online.waiting"] = new("Menunggu pemain kedua di port {0}…", "Waiting for a second player on port {0}…"),
        ["online.connecting"] = new("Menghubungkan ke {0}…", "Connecting to {0}…"),
        ["online.connected"] = new("Terhubung. Host memilih mode.", "Connected. The host picks the mode."),
        ["online.failed"] = new("Koneksi gagal: {0}", "Connection failed: {0}"),
        ["online.note"] = new("Co-op online deterministik: hanya input yang dikirim. Lockstep menunggu input rekan (jeda singkat saat lambat); rollback menebak dan mengoreksi, cocok untuk koneksi jauh.", "Deterministic online co-op: only inputs travel. Lockstep waits for the partner's input (a brief pause on a slow link); rollback predicts and corrects, better for long distances."),
        ["controls.title"] = new("Kontrol", "Controls"),
        ["credits.title"] = new("Kredit", "Credits"),
        ["credits.body"] = new(
            "Desain game, kode, dan arahan: Ariana Mischa Fadhila\nStudio: Syubadubin Studios\n\nMesin 3D: ThreeNet (Gravicode Studios)\nAntarmuka: Avalonia UI\nModel 3D & seni 2D: Rodin, dirig dan dianimasikan di Blender\nMusik & efek suara: disintesis prosedural saat permainan berjalan\nHuruf: Bungee (David Jonathan Ross) & Barlow Condensed (Jeremy Tribby), SIL Open Font License\n\nTerima kasih kepada semua pendekar kampung.",
            "Game design, code and direction: Ariana Mischa Fadhila\nStudio: Syubadubin Studios\n\n3D engine: ThreeNet (Gravicode Studios)\nInterface: Avalonia UI\n3D models & 2D art: Rodin, rigged and animated in Blender\nMusic & sound effects: procedurally synthesised at runtime\nFonts: Bungee (David Jonathan Ross) & Barlow Condensed (Jeremy Tribby), SIL Open Font License\n\nThanks to every village fighter out there."),
        ["mode.story.desc"] = new("Ikuti perjalanan Jenggo dari gang Jakarta sampai menara Garuda Hitam. Level, jurus, dan perlengkapan tersimpan.", "Follow Jenggo from the Jakarta alleys to Garuda Hitam's tower. Levels, skills and gear are kept."),
        ["mode.arcade.desc"] = new("Tujuh bab berturut-turut, tiga nyawa, murni skill. Kejar skor tertinggi.", "Seven chapters back to back, three lives, pure skill. Chase the high score."),
        ["mode.survival.desc"] = new("Gelombang musuh tanpa henti. Bos muncul setiap lima gelombang.", "Endless waves. A boss shows up every fifth wave."),
        ["mode.bossrush.desc"] = new("Ketujuh bos berurutan. Waktu tercepat tercatat.", "All seven bosses in a row. Fastest time is recorded."),
        ["mode.challenge.desc"] = new("Misi khusus dengan aturan sendiri.", "Special missions with their own rules."),
        ["mode.online.desc"] = new("Co-op berdua lewat jaringan, langsung antar komputer.", "Two-player co-op over the network, computer to computer."),
        ["best"] = new("Terbaik", "Best"),
        ["fps"] = new("fps", "fps"),
        ["coop.airlaunch"] = new("KOMBO UDARA BERSAMA!", "AIR LAUNCH COMBO!"),
        ["coop.revive"] = new("BANGKIT LAGI!", "BACK ON YOUR FEET!"),
        ["weapon.broke"] = new("{0} patah", "{0} broke"),
    };

    public static string Get(string key) => s_text.TryGetValue(key, out var t) ? t[Language] : key;

    public static string Format(string key, params object[] args) => string.Format(System.Globalization.CultureInfo.InvariantCulture, Get(key), args);

    /// <summary>Control reference (action, keyboard, gamepad) in the current language.</summary>
    public static IReadOnlyList<(string Action, string Keys, string Pad)> Controls
    {
        get
        {
            var rows = new (string, string, string)[s_controls.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var (action, keys, pad) = s_controls[i];
                rows[i] = (T(action), T(keys), T(pad));
            }

            return rows;
        }
    }

    private static readonly (LocText Action, LocText Keys, LocText Pad)[] s_controls =
    [
        (new("Bergerak", "Move"), new("WASD  ·  P2: panah", "WASD  ·  P2: arrows"), new("Stik kiri / D-pad", "Left stick / D-pad")),
        (new("Serangan ringan (kombo)", "Light attack (combo)"), LocText.Same("J  ·  P2: Num1"), LocText.Same("X")),
        (new("Serangan berat / serudukan", "Heavy attack / dash attack"), LocText.Same("K  ·  P2: Num2"), LocText.Same("Y")),
        (new("Lompat", "Jump"), new("Spasi / L  ·  P2: Num3", "Space / L  ·  P2: Num3"), LocText.Same("A")),
        (new("Jurus spesial (+ arah)", "Special (+ direction)"), LocText.Same("I  ·  P2: Num4"), LocText.Same("B")),
        (new("Pegang / ambil / lempar / bangunkan", "Grab / pick up / throw / revive"), LocText.Same("U  ·  P2: Num5"), LocText.Same("RB")),
        (new("Rage Nusantara / jurus pamungkas", "Rage Nusantara / ultimate"), LocText.Same("O  ·  P2: Num6"), LocText.Same("RT")),
        (new("Menghindar  (+ ringan = tangkis)", "Dodge  (+ light = counter)"), LocText.Same("Shift  ·  P2: Num0"), LocText.Same("LB")),
        (new("Jeda", "Pause"), LocText.Same("Esc"), LocText.Same("Start")),
    ];

    /// <summary>Languages that can be picked right now (inline ones plus every table found in <c>assets/lang</c>).</summary>
    public static IReadOnlyList<Language> AvailableLanguages
    {
        get
        {
            EnsureLanguagesLoaded();
            var list = new List<Language>();
            foreach (var l in Localization.All)
            {
                if (Localization.IsAvailable(l))
                {
                    list.Add(l);
                }
            }

            return list;
        }
    }

    /// <summary>
    /// Reads every <c>assets/lang/&lt;code&gt;.json</c> next to the executable once and registers it with
    /// <see cref="Localization"/>. Each file is a flat object mapping English source text to the translation.
    /// A missing or broken file just leaves that language unavailable (text falls back to English).
    /// </summary>
    public static void EnsureLanguagesLoaded()
    {
        if (s_languagesLoaded)
        {
            return;
        }

        s_languagesLoaded = true;
        string dir = Path.Combine(AppContext.BaseDirectory, "assets", "lang");
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*.json"))
        {
            if (!Localization.TryParseCode(Path.GetFileNameWithoutExtension(file), out var language) || !Localization.IsTableBased(language))
            {
                continue;
            }

            try
            {
                Localization.Register(language, ParseTable(File.ReadAllText(file)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                System.Diagnostics.Debug.WriteLine($"Language file {file} skipped: {ex.Message}");
            }
        }
    }

    /// <summary>Parses a flat {"English": "translation"} object without reflection (trim-safe).</summary>
    public static Dictionary<string, string> ParseTable(string json)
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        using var doc = System.Text.Json.JsonDocument.Parse(json, new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true });
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                table[prop.Name] = prop.Value.GetString()!;
            }
        }

        return table;
    }

    private static bool s_languagesLoaded;
}
