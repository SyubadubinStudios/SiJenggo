# PLAN — Si Jenggo: Preman Berhati Malaikat

Roadmap pengembangan · Development roadmap
Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios · Made by Ariana Mischa Fadhila of Syubadubin Studios

Status per item ada di [Progress.md](Progress.md). Desain lengkap ada di [solution-design.md](solution-design.md).
· Per-item status lives in [Progress.md](Progress.md); the full design is [solution-design.md](solution-design.md).

## Milestone 1 — Fondasi · Foundation ✅

- ECS sparse-set, RNG deterministik, jam fixed-step · Sparse-set ECS, deterministic RNG, fixed-step clock
- Simulasi 60 Hz yang terpisah total dari render · 60 Hz simulation fully separated from rendering
- Suite tes xUnit · xUnit test suite

## Milestone 2 — Pertarungan inti · Core combat ✅

- 4 karakter, 130 jurus dengan frame data, kombo, cancel, buffer input · 4 fighters, 130 moves with frame data, combos, cancels, input buffer
- Kuncian & bantingan, tangkisan, menghindar, serangan udara, juggle · Grabs & throws, counters, dodge, air attacks, juggles
- Rage Nusantara + jurus pamungkas, semangat + jurus spesial, jurus daerah · Rage + ultimates, spirit + specials, regional styles
- Senjata tradisional dan properti lingkungan · Traditional weapons and environmental props

## Milestone 3 — Dunia & musuh · World & enemies ✅

- 7 bab, area, gelombang, sutradara kamera, bahaya lingkungan · 7 chapters, areas, waves, camera director, hazards
- 7 jenis musuh dengan token serang; 7 bos tiga fase dengan mekanik arena · 7 enemy types with attack tokens; 7 three-phase bosses
- Co-op lokal, partner AI, jurus co-op, membangunkan rekan · Local co-op, AI partner, co-op moves, revive

## Milestone 4 — Progresi & mode · Progression & modes ✅

- XP/level, pohon jurus, perlengkapan, pembukaan karakter · XP/levels, skill tree, gear, character unlocks
- Cerita, Arcade, Bertahan, Boss Rush, Tantangan; pencapaian; simpan/profil · Story, Arcade, Survival, Boss Rush, Challenges; achievements; save/profile

## Milestone 5 — Tampilan & suara · Presentation & audio ✅

- Aset: Rodin → Blender (rig, 25 klip, LOD) → ThreeNet · Assets: Rodin → Blender (rig, 25 clips, LOD) → ThreeNet
- Pemandangan 7 bab, cuaca, VFX, kamera sinematik · Scenery for 7 chapters, weather, VFX, cinematic camera
- Musik dinamis & SFX yang disintesis · Synthesised dynamic music & SFX
- UI Avalonia: menu, HUD, jeda, hasil, progresi, pengaturan, dwibahasa · Avalonia UI: menus, HUD, pause, results, progression, settings, bilingual

## Milestone 6 — Online ✅ (dasar · baseline)

- Lockstep deterministik lewat ThreeNet UDP, deteksi desync · Deterministic lockstep over ThreeNet UDP, desync detection
- Server relay opsional · Optional relay server
- Rollback netcode opsional (lihat M7) · Optional rollback netcode (see M7)

## Milestone 7 — Polesan berikutnya · Next polish ✅ (kecuali uji manusia · except human playtesting)

1. **Uji main & penyeimbangan** · Playtesting & balance — 🟡 sweep AI tanpa layar (`tools/SiJenggo.Balance`,
   2.688 laga dalam ~8 detik) + dua putaran penyetelan; lihat [docs/id/KESEIMBANGAN.md](docs/id/KESEIMBANGAN.md).
   **Uji main manusia masih wajib.** · Headless AI sweep and two tuning passes; human playtesting still required.
2. **Animasi khusus per karakter** · Per-character animation — ✅ `tools/blender/character_styles.py`: kuda-kuda &
   sembah Bima, earpiece & gadget Rani, tasbih/sarung/doa Ucup, pamer otot Jenggo, klip `intro` untuk 7 bos.
3. **Rekaman suara** · Voice lines — ✅ 48 baris ElevenLabs v3 (bos: intro, 2 ejekan, kalah; pemain: 5 teriakan).
4. **Rollback netcode** — ✅ opsional di layar Online (default tetap lockstep) · optional, lockstep stays default.
5. **Sinematik antar-bab** · Between-chapter cutscenes — ✅ intro dalam engine tiap bab, "Bab berikutnya", epilog.
6. **Lokalisasi tambahan** · More languages — ✅ Bahasa Melayu lewat tabel `assets/lang/ms.json`; bahasa lain cukup
   enum + file JSON · Malay via a JSON table; further languages need only an enum value and a file.

Sisa M7 · Still open in M7:
- Uji main manusia, lalu setel ulang (terutama Jagal Merapi, Jenderal Garuda, Nyi Angin Laut) · Human playtests.
- AI rekan belum menghindari bahaya (lihat KESEIMBANGAN.md) · The companion AI does not step out of hazards.
- Rekaman suara baru Bahasa Indonesia; teriakan EN/MS belum ada · Voice is Indonesian only.

## Milestone 8 — Platform 🟡

- **Linux / macOS** — 🟡 `dotnet publish -r linux-x64` dan `-r osx-arm64` berhasil dengan native ThreeNet yang
  benar (ELF x86-64 / Mach-O arm64); belum dijalankan di mesin Linux/Mac · Publishes cleanly; not run on those OSes yet.
- **Android** — 🟡 `src/SiJenggo.Android`: SurfaceView + `Renderer.CreateForAndroid`, kontrol sentuh, mode Cerita.
  APK ter-build (ThreeNet 0.9 kini menyertakan native android-arm64/x64) tetapi belum dicoba di perangkat/emulator.
  · APK builds; not yet run on a device or emulator.
- **iOS** — ⏳ native ios-arm64 ada di paket ThreeNet, tetapi build butuh Mac + Xcode · Needs a Mac to build.

## Pasca rilis · Post-launch (solution-design §17)

- Season 1: Ekspansi Bali, bos Barong, tahap Pantai Kuta · Bali expansion
- Season 2: Ekspansi Papua, perang hutan · Papua expansion
- Season 3: Candi bawah air, makhluk mitologi · Underwater temple
