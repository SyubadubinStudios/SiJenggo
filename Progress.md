# Progress — Si Jenggo

Pelacakan pengembangan · Development tracking
Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios · Made by Ariana Mischa Fadhila of Syubadubin Studios

Legenda · Legend: ✅ selesai & terverifikasi · done & verified — 🟡 selesai, verifikasi terbatas · done, limited verification — ⏳ belum · not yet

_Terakhir diperbarui · Last updated: 2026-10-14_

## Ringkasan · Summary

| Area | Status | Bukti · Evidence |
|---|---|---|
| Build seluruh solusi · Full solution build | ✅ | `dotnet build SiJenggo.slnx` tanpa warning · no warnings |
| Tes · Tests | ✅ | `dotnet test`: 51/51 lulus (simulasi, rollback, bahasa, suara, sinematik) · passed |
| Render ThreeNet + UI Avalonia | ✅ | 16 screenshot dari game yang berjalan, termasuk sinematik bab · from the running game (`docs/screenshots`) |
| Online lockstep | ✅ | Tes 30% paket hilang + UDP loopback nyata, checksum identik · 30% loss test + real UDP loopback |
| Online rollback | 🟡 | Tes memori: latensi 2–9 tick + 20–35% hilang, 400+ rollback, checksum & hash penuh identik; belum dicoba lewat UDP/internet · in-memory tests only |
| Aset 3D · 3D assets | ✅ | 17 karakter ber-rig + 21 model statis; 11 rig dengan klip khas · 11 rigs with signature clips |
| Audio & suara · Audio & voice | 🟡 | 48 baris suara terdekode dan terhubung ke event; belum didengar/di-mix oleh manusia · not yet listened to by a human |
| Keseimbangan · Balance | 🟡 | Sweep AI 2.688 laga + 2 putaran penyetelan; belum ada uji main manusia · AI-only |
| Bahasa · Languages | 🟡 | ID, EN, MS (513 string, tes cakupan); teks Melayu belum ditinjau penutur asli di layar · Malay not yet reviewed on screen by a native speaker |
| Linux / macOS | 🟡 | `dotnet publish` linux-x64 & osx-arm64 berhasil; belum dijalankan · publishes, not run |
| Android | 🟡 | APK ter-build (native arm64 + x86_64, semua aset); belum dijalankan di perangkat · builds, not run |
| iOS | ⏳ | Butuh Mac + Xcode · Needs a Mac |

## Detail

### Inti & simulasi · Core & simulation
- ✅ ECS (`World`, `ComponentPool<T>`, handle berversi, flush tertunda), kini bisa disalin utuh (`CopyFrom`) dan di-hash penuh
- ✅ `DeterministicRandom`, `FixedStepClock`, `EventQueue<T>`
- ✅ `GameSession` + 10 sistem (pemain, combat, AI, bos, lingkungan, co-op, stage director, kamera, musik, tantangan)
- ✅ Determinisme: tes simulasi identik dan checksum
- ✅ Snapshot sesi (`SaveState`/`LoadState`) tanpa alokasi setelah pemanasan; ~4–15 µs simpan/muat (Debug); tes gagal bila ada field baru yang lupa disnapshot

### Konten · Content
- ✅ 4 karakter, 130 jurus, 8 senjata, 8 prop, 6 jenis pickup
- ✅ 8 jenis musuh, 7 bos × 3 fase, 10 mekanik arena, 10 jenis bahaya
- ✅ 7 bab dengan 3–4 area, cerita dwibahasa, jurus daerah per bab, epilog
- ✅ Pohon jurus 20 simpul, 11 perlengkapan, 17 pencapaian, 8 tantangan
- 🟡 Penyetelan: Nyi, Penebang, Merapi, Patih, Garuda, Bima, Rani (angka sebelum/sesudah di `docs/id/KESEIMBANGAN.md`); kolam lava Palu Magma kini peringatan 40 tick / 170 tick

### Mode · Modes
- ✅ Cerita (membuka bab, jurus, perlengkapan, karakter — teruji di `Story_clear_unlocks...`)
- ✅ Cerita: sinematik pembuka tiap bab dalam engine, tombol "Bab berikutnya", epilog setelah bab 7 (terlihat di tur screenshot)
- ✅ Arcade, Bertahan (teruji), Boss Rush (arena teruji), Tantangan (evaluasi teruji)
- 🟡 Epilog dan alur "Bab berikutnya" baru diuji lewat build + tur otomatis bab 1, belum dimainkan manusia sampai bab 7

### Tampilan · Presentation
- ✅ Aktor ber-rig dengan pooling, scrub klip sesuai frame data, senjata di tangan
- ✅ Klip khas per karakter (`gadget`, `taunt`, `intro` + varian `idle`/`walk`/`spin`/`victory`) dengan fallback ke klip bersama; pose ditinjau lewat lembar pose Blender
- 🟡 Klip khas belum dilihat bergerak di dalam game oleh manusia (hanya lembar pose dan screenshot)
- ✅ Pemandangan 7 bab dari geometri gabungan (16–56 draw call per frame)
- ✅ VFX: percikan per jenis pukulan, cincin kejut, kilat, api, debu, aura Rage, penanda bahaya, cuaca
- ✅ Kamera: follow, guncangan, zoom sinematik, gerak lambat saat bos kalah, kamera berskrip untuk sinematik
- ✅ Preset kualitas 0–3; ~13 ms/frame pada Intel UHD 620 preset 0

### Audio
- ✅ Efek dan musik disintesis (tiga lapisan per gaya)
- ✅ 48 baris suara (`tools/voice/lines.json` → ElevenLabs v3 → WAV 22 kHz), satu suara sekaligus, musik diturunkan, teriakan dibatasi
- 🟡 Belum didengar oleh manusia; suara hanya dalam Bahasa Indonesia

### Antarmuka · UI
- ✅ Judul, pilih karakter, tantangan, online (kini dengan pilihan netcode), jurus & perlengkapan, pengaturan, kredit
- ✅ HUD, jeda + tabel kontrol, layar hasil; letterbox + subtitle sinematik
- ✅ Tiga bahasa (ID/EN/MS), pemilih di judul dan pengaturan; kredit studio di judul, kredit, README, dan dokumen
- ✅ Keyboard 2 pemain + gamepad (ThreeNet `Gamepads`)
- 🟡 Navigasi menu dengan gamepad: diimplementasikan, belum diuji dengan gamepad fisik

### Aset · Assets (Rodin MCP + Blender)
- ✅ Rodin: 17 karakter, 8 prop, 8 senjata, 5 makanan, key art, 4 potret; 48 suara ElevenLabs
- ✅ Blender: normalisasi, LOD0/LOD1, rig 20 tulang, skin, 25 klip bersama, ekspor glTF
- ✅ Blender tanpa layar (`--background`): `character_styles.py` (klip khas) dan `preview_clips.py` (lembar pose)

### Jaringan · Networking
- ✅ `LockstepSession` (input delay 3, redundansi 10, checksum tiap detik)
- 🟡 `RollbackSession` (prediksi 8 frame, input delay 1, ack + kirim ulang, checksum pada frame terkonfirmasi); event selama re-simulasi dibuang
- ✅ `OnlineLink` (host/gabung, mode netcode dikirim dalam `MatchStart`) + `SiJenggo.Server` (relay)
- 🟡 Belum diuji antar dua mesin di internet; relay meneruskan paket secara reliable sehingga menambah jeda pada mode rollback

### Platform
- 🟡 Linux/macOS: publish self-contained berhasil, belum dijalankan
- 🟡 Android: `src/SiJenggo.Android` ter-build (APK ~45 MB), belum dijalankan; di luar `SiJenggo.slnx` agar build desktop tidak butuh workload Android
- ⏳ iOS

### Dokumentasi · Documentation
- ✅ README dwibahasa dengan screenshot; `docs/en` dan `docs/id` (arsitektur, gameplay, alur aset, jaringan, keseimbangan)
- ✅ PLAN.md, Progress.md, CLAUDE.md

## Log

| Tanggal · Date | Catatan · Notes |
|---|---|
| 2026-10-13 | Inti ECS, data game, semua sistem simulasi, tes · Core ECS, game data, all simulation systems, tests |
| 2026-10-13 | Aset karakter Rodin + rig/animasi Blender · Rodin characters + Blender rig/animation |
| 2026-10-14 | Prop/senjata/makanan Rodin, seni 2D, layer ThreeNet, UI Avalonia, relay, dokumentasi, screenshot · Props/weapons/food, 2D art, ThreeNet layer, Avalonia UI, relay, docs, screenshots |
| 2026-10-14 | M7: rollback, Bahasa Melayu, sweep keseimbangan + penyetelan, 48 suara, klip khas per karakter, sinematik antar-bab & epilog · rollback, Malay, balance sweep, voice, signature clips, cutscenes |
| 2026-10-14 | M8: publish Linux/macOS, proyek Android (APK ter-build) · Linux/macOS publish, Android head |
