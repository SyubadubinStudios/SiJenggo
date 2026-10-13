# Arsitektur

Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios. · [English](../en/ARCHITECTURE.md)

## Proyek dan ketergantungan

```
SiJenggo.Core ◄── SiJenggo.Game ◄── SiJenggo.Presentation ◄── SiJenggo.App
                       ▲                                        (Avalonia 12, ThreeNet.Avalonia)
                       ├── SiJenggo.Server (hanya jaringan ThreeNet)
                       └── SiJenggo.Tests (xUnit)
```

| Proyek | Tanggung jawab | Bergantung pada render? |
|---|---|---|
| **Core** | ECS `World`/`ComponentPool<T>`, handle `Entity`, `DeterministicRandom` (xoshiro128**), `FixedStepClock`, `EventQueue<T>` | Tidak |
| **Game** | Komponen, seluruh data game (jurus, karakter, musuh, bos, bab, senjata, skill, perlengkapan), semua sistem, mode, simpan/profil/pencapaian, netcode lockstep | Tidak |
| **Presentation** | Mencerminkan ECS ke scene ThreeNet: aktor ber-rig dan klip Blender, prop prosedural, pemandangan tiap bab, VFX, kamera, audio sintesis | ThreeNet |
| **App** | Jendela Avalonia, menu, HUD, pemetaan input, loop frame, lobi online, tur screenshot | Avalonia + ThreeNet |
| **Server** | Relay UDP opsional untuk main online | Jaringan ThreeNet |

Kode khusus platform hanya ada di App (jendela, input) dan inti native ThreeNet. Game dan Core netral platform
dan kompatibel AOT, jadi versi mobile cukup menambah proyek App baru.

## ECS

- `World` membagikan handle `Entity(Id, Version)`. Versi naik saat slot dipakai ulang, sehingga referensi basi
  (rekan kuncian, pemilik proyektil) terdeteksi lewat `World.IsAlive`. `default(Entity)` adalah handle null.
- Tiap jenis komponen disimpan di `ComponentPool<T>`: sparse set dengan array padat, tambah/hapus O(1),
  iterasi linear, tanpa alokasi setelah ukurannya stabil.
- Penghapusan ditunda sampai `World.Flush()` di akhir tick, jadi sistem aman menghapus sambil beriterasi.
- Komponen adalah struct biasa (`Components/Components.cs`).

## Simulasi (`GameSession`)

`GameSession.Step(inputs)` memajukan tepat satu tick 60 Hz. Sistem ada di file partial `src/SiJenggo.Game/Systems/`,
dipanggil berurutan: input & partner AI → kontrol pemain → AI musuh dan bos → state fighter → gerak/fisika →
deteksi pukulan (`ApplyHit`, satu-satunya tempat kerusakan terjadi) → proyektil, bahaya, prop, pickup, rage →
co-op, nyawa, KO → sutradara tahap, kamera, musik, tantangan → `World.Flush()`.

Semua hal yang perlu ditanggapi tampilan dikirim sebagai `GameEvent` ke `session.Events`. Simulasi tidak pernah
membaca event kembali, jadi aman jika event dibuang.

### Aturan determinisme

- Hanya `DeterministicRandom`; tanpa `Random.Shared`, tanpa jam dinding.
- Fighter diproses berurutan menurut id entity.
- Input digital (`InputButtons`, 2 byte); stik analog dikuantisasi di App sebelum masuk simulasi.
- `GameSession.Checksum()` meng-hash RNG, darah, dan posisi; para peer membandingkannya tiap detik.

Dibuktikan oleh tes `Same_seed_and_inputs_give_identical_simulations` dan `Lockstep_peers_stay_in_sync_over_a_lossy_link`.

## Presentation

- `GameView.Update(session, alpha, dt)` menguras event (VFX, audio, guncangan/zoom kamera), menyinkronkan aktor,
  kamera, efek, pool lampu, dan campuran musik, lalu `Scene.UpdateAnimations`.
- `ActorManager` membuat satu tampilan per entity yang punya `Visual`, menginterpolasi posisi dengan alpha jam.
  Fighter ber-rig **di-pool per model** (klipnya tetap valid, tidak ada pemuatan ulang).
- `ActorAnimator` tidak membiarkan klip berjalan bebas: klip Blender **di-scrub** ke titik yang sesuai frame data
  jurus, sehingga kepalan mendarat tepat saat hitbox aktif, walaupun ada bonus kecepatan, rage, atau hit-stop.
- `EnvironmentBuilder` menyusun tiap bab dari geometri gabungan (`MeshBuilder`): satu draw call per material
  per tahap. Frame biasa 25–55 draw call.
- `VfxSystem` memakai tujuh `ParticleEffect` bersama, cincin ter-pool, dua lampu kilat, dan penanda bahaya ter-pool.
- `AudioDirector`/`Synth` mensintesis semua efek dan tiga lapisan musik per gaya saat start. Satu-satunya file
  audio adalah rekaman suara (`VoiceBank`, `assets/voice/*.wav`), satu suara sekaligus di atas musik yang diturunkan.
- **Sinematik** (`Cutscene.cs`): `Cutscenes.ChapterIntro` menyusun shot kamera dari data bab itu sendiri (shot
  pembuka area 1, shot mengikuti area tengah, dorongan ke arena bos, para pahlawan di garis start).
  `GameView.PlayCutscene` menggerakkan kamera dengannya; simulasi ditahan, bukan dijeda, sehingga klip idle, cuaca,
  dan lampu tetap bergerak. `Cutscenes.Epilogue` menutup bab terakhir.

## App

- `MainWindow` memuat `ThreeNetView`, latar key art, dan `Screen` aktif. Di menu, tampilan 3D disembunyikan dan dijeda.
- `GameController.Frame(dt)`: `FixedStepClock` → `Step` per tick (input lokal, atau `LockstepSession` saat online),
  lalu `GameView.Update` dengan alpha interpolasi. Kekalahan bos memicu gerak lambat 1,6 detik.
- `InputMapper` menggabungkan dua tata letak keyboard dan dua gamepad.
- `GameController.HoldSimulation` membekukan tick sementara tampilan tetap berjalan dengan waktu nyata (sinematik);
  `GameScreen` menggambar letterbox dan subtitle, dan hasil mode Cerita menawarkan "Bab berikutnya" (atau epilog).

## Android

`src/SiJenggo.Android` (tidak masuk `SiJenggo.slnx`, butuh workload android) merender langsung ke `SurfaceView`
lewat `Renderer.CreateForAndroid` dan menjalankan `GameSession` yang sama di thread render. `TouchControls` adalah
stik mengambang ditambah tujuh tombol yang menghasilkan `InputButtons`; aset APK dibongkar ke file aplikasi dan
`SIJENGGO_ASSETS` mengarahkan `AssetPaths` ke sana. Memainkan mode Cerita bab demi bab dengan partner AI. Sudah
bisa di-build (native arm64 + x86_64 dari paket ThreeNet) tetapi belum dijalankan di perangkat.

## Menambah bahasa

Bahasa Indonesia dan Inggris ditulis langsung di setiap `LocText(Id, En)` dan di `Ui/Strings.cs`. Bahasa lain
berbasis tabel: `assets/lang/<kode>.json` berisi objek JSON datar yang memetakan **teks sumber bahasa Inggris** ke
terjemahannya (Bahasa Melayu tersedia sebagai `ms.json`). `Strings` membaca berkas di samping executable sekali saja
lalu menyerahkannya ke `Localization.Register`; proyek Game tetap bebas IO. `LocText[language]` hanya lookup
dictionary dan kembali ke bahasa Inggris bila entri tidak ada; teks gabungan seperti `"New chapter: <nama>"`
diterjemahkan per bagian.

Cara menambah bahasa:

1. Tambahkan nilai di akhir `enum Language` (`Game/Text.cs`, jangan diurutkan ulang: save menyimpan angkanya) lalu
   beri kode dan nama aslinya di `Localization.Code` / `Localization.NativeName`.
2. Salin `assets/lang/ms.json` menjadi `assets/lang/<kode>.json` dan terjemahkan nilainya (pertahankan `{0}`).
3. Chip di layar judul dan pilihan di Pengaturan otomatis menampilkan setiap bahasa yang tabelnya berhasil dimuat.
4. Salin uji cakupan di `tests/SiJenggo.Tests/LocalizationTests.cs` untuk kode baru. Uji itu memindai semua
   `new("..", "..")` di sumber Game dan `Strings.cs`, jadi konten baru tanpa terjemahan akan gagal diuji.

## Catatan performa

- Tanpa LINQ atau alokasi di kode per-tick; event, pool, memori pukulan, dan buffer partikel dipakai ulang.
- `HitMemory` adalah struct 6 slot tetap per target.
- Lampu: tahap mencatat titik lampu; pool 6 point light berpindah ke titik terdekat kamera.
- Preset kualitas mengatur bayangan, SSAO, MSAA, bloom, dan skala render. Di Intel UHD 620, preset 0 merender
  pertarungan ramai sekitar 13 ms per frame.
