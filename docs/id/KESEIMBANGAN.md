# Uji keseimbangan otomatis

Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios. · [English](../en/BALANCE.md)

`tools/SiJenggo.Balance` adalah alat konsol tanpa grafis yang memainkan setiap bos (dan, bila diminta, setiap bab
penuh) dengan setiap karakter, di tingkat Santai, Normal dan Jagoan, sendirian maupun co-op dua pemain, dengan
beberapa seed, lalu menulis laporan markdown ([balance-report.md](../en/balance-report.md), berbahasa Inggris).
Simulasi tidak butuh GPU dan setiap sesi berdiri sendiri, jadi seluruh uji berjalan paralel di semua inti CPU.

> **Ini bukan pengganti uji main oleh manusia.** Kedua pengendali adalah bot. Mereka membaca keadaan game dengan
> sempurna, tidak belajar, tidak pernah menangkis, jarang memakai jurus dengan baik dan mengabaikan sebagian besar
> kemampuan khas tiap karakter. Pakai angkanya untuk menemukan pencilan dan memeriksa arah sebuah perubahan, bukan
> sebagai gambaran pengalaman pemain. Butir 1 Milestone 7 di PLAN.md (uji main manusia) tetap terbuka.

## Cara menjalankan

```bash
dotnet run -c Release --project tools/SiJenggo.Balance                 # arena bos -> docs/en/balance-report.md
dotnet run -c Release --project tools/SiJenggo.Balance -- --chapters   # ditambah setiap bab penuh (gelombang + bos)
dotnet run -c Release --project tools/SiJenggo.Balance -- --chapter 4 --bot skilled --stdout   # satu bos, cetak saja
```

| Opsi | Arti |
|---|---|
| `--seeds N` | jumlah seed per konfigurasi (bawaan 8) |
| `--chapters` / `--only-chapters` | tambahkan / hanya jalankan bab penuh (gelombang, lalu bos) |
| `--chapter I[,J]` | hanya bab ini (mulai dari 0) |
| `--bot skilled,companion` | pengendali yang dipakai (bawaan keduanya) |
| `--boss-cap S` / `--chapter-cap S` | batas waktu dalam detik (bawaan 600 / 1500); sesi yang kena batas dihitung kalah |
| `--threads N` | jumlah thread (bawaan: semua inti) |
| `--out PATH` / `--stdout` | tujuan laporan |
| `--csv PATH` | satu baris mentah per sesi, untuk membandingkan sebelum/sesudah |

Waktu di desktop 8 thread (Release): uji bawaan (7 bos x 4 karakter x 3 tingkat x solo/co-op x 2 pengendali x 8 seed
= 2.688 sesi, ~49 jam simulasi) selesai sekitar **7 detik**; dengan `--chapters` (5.376 sesi, ~187 jam simulasi)
sekitar **23 detik**. Hasilnya deterministik: build yang sama memberi laporan yang sama (kecuali baris waktu),
berapa pun jumlah thread-nya.

Untuk menilai perubahan angka: jalankan sekali dengan `--csv before.csv`, ubah datanya, jalankan lagi dengan
`--csv after.csv`, lalu bandingkan keduanya (tabel di bawah dibuat dengan cara itu).

## Yang dicatat

Setiap sesi adalah `GameSession` yang dibangun seperti di game: `StageKind.BossArena` untuk bos (pemain segar,
3 nyawa, `Scaling = 1`, jadi sama dengan bos Mode Cerita, bukan Boss Rush yang diskalakan) atau `StageKind.Chapter`
untuk bab penuh. Pemain memakai `PlayerLoadout.Base` (tanpa level, skill atau perlengkapan). Rekan co-op adalah
karakter berikutnya dalam daftar (Jenggo+Bima, Bima+Rani, Rani+Ucup, Ucup+Jenggo).

Per sesi: menang / kalah / habis waktu, waktu selesai, waktu bos per fase (tanpa intro), sisa darah bos saat kalah,
kerusakan yang diterima pemain, kematian (nyawa hilang; kalah berarti 4 per pemain), dan pembagian kerusakan menurut
sumbernya, diambil dari event `Hit` yang memang sudah dikirim sesi: bos sendiri (per gerakan), musuh lain (anak
buah, bayangan, musuh gelombang), dan bahaya arena (per `HazardKind`, dikaitkan ke bahaya di bawah korban).

Dua pengendali:

- **Bot terampil** (`SkilledBot.cs`, sinyal utama): menekan tombol untuk slot pemain biasa. Bot ini keluar dari
  lingkaran bahaya (memilih titik terdekat yang paling sedikit tertutup bahaya), tidak memulai kombo di dekat bahaya
  yang akan meledak, dan menghindar dari serangan musuh yang terlihat datang, tetapi baru setelah jeda reaksi
  12 tick (200 ms) dan hanya 60% dari waktu. Pukulan cepat praktis tidak bisa dibaca, sama seperti bagi manusia.
- **AI rekan** (`PlayerLoadout.IsAi`, rekan di dalam game): tidak pernah menghindar, menangkis atau menjauhi bahaya.
  Ini kasus terburuk dan menunjukkan rasanya co-op bersama rekan AI.

## Target

`solution-design.md` tidak menetapkan angka (hanya "3 fase, mekanik unik" per bos), jadi target ini diturunkan dari
kebiasaan genre beat 'em up dan berlaku untuk bot terampil dengan statistik dasar:

1. **Tingkat kesulitan berurutan**: persentase menang Santai >= Normal >= Jagoan untuk setiap bos.
2. **Bos makin sulit sepanjang cerita**, tanpa tembok di tengah: tidak ada bos yang jelas lebih sulit dari bos
   sesudahnya. Bot tanpa progresi Mode Cerita, jadi penurunan persentase menang yang landai memang wajar.
3. **Tidak ada karakter yang jauh tertinggal**: persentase menang Normal per karakter berselisih sekitar 25 poin.
4. **Panjang pertarungan**: pertarungan bos Normal yang dimenangkan 45–150 detik. Bot menyerang tanpa henti,
   jadi pertarungannya di ujung pendek; manusia akan lebih lama. Karena itu darah bos tidak dinaikkan.
5. Perubahan secukupnya (kira-kira 15% atau kurang per nilai) dan hanya di data (`Bosses.cs`, `Characters.cs`,
   `Enemies.cs`), sehingga id, urutan dan teks tidak tersentuh.

## Temuan uji pertama

- **Jagal Merapi (bab 5)** menjadi tembok: 3% bot terampil / 3% AI rekan di Normal solo, 0% di Jagoan. 60–78%
  kerusakan berasal dari kolam lava: setiap ayunan Palu Magma meninggalkan kolam 300 tick tepat di jarak dekat
  (peringatan 20 tick), dan di jarak dekat palu itu satu-satunya serangannya.
- **Raja Penebang (bab 4)**: Gergaji Mesin (7 pukulan x 5 kerusakan dengan dorongan kecil) dipakai dari jarak
  nol; 44% bot terampil / 0% AI rekan di Normal.
- **Nyi Angin Laut (bab 3)**: badai petir dan Pusaran Angin (serangan area yang terlalu besar untuk dihindari)
  membuatnya lebih sulit dari bab 4 dan 6 (28% bot terampil, Normal).
- **Patih Abadi (bab 6)** lebih mudah daripada bab 3–5 untuk kedua pengendali.
- **Jenderal Garuda (bab 7)**: Serangan Udara menaruh lima sapuan laser bertumpuk sepanjang sumbu X; lolosnya harus
  berjalan ke arah kedalaman, yang lambat, jadi ini sumber kerusakan terbesar (12% bot terampil, Normal).
- **Karakter**: Bima (95 darah, pertahanan 0,9) dan Rani jauh tertinggal dengan AI rekan (20% dan 38% dibanding
  ~60% Jenggo dan Ucup); dengan bot terampil Rani dan Ucup yang tertinggal (36% / 41% dibanding 64–66%).
- Anak buah dan musuh gelombang tidak pernah jadi masalah (paling banyak 13% kerusakan di arena bos), jadi
  `Enemies.cs` tidak diubah.

## Perubahan angka

| Apa | Sebelum | Sesudah | Alasan |
|---|---|---|---|
| Darah Nyi Angin Laut | 600 | 560 | bab 3 lebih sulit dari bab 4 dan 6 |
| Interval badai petir Nyi (F1/F2/F3, tick) | 540 / 360 / 240 | 600 / 450 / 330 | petir ~30% kerusakan |
| Bobot Badai Petir Nyi (F2/F3) | 2 / 3 | 1 / 2 | lebih sedikit petir tambahan |
| Bobot Pusaran Angin Nyi (F1/F2/F3) | 1 / 2 / 3 | 0,5 / 0,75 / 1 | area terlalu besar untuk dihindari; gerakan bos paling merusak |
| Kekuatan Raja Penebang | 1,1 | 1,05 | |
| Bobot Gergaji Mesin (F1/F2/F3) | 2 / 2 / 3 | 1 / 1 / 2 | kuncian banyak-pukulan jadi sumber kerusakan terbesar |
| Jarak minimum Gergaji Mesin | 0 | 1,8 | ini serudukan: untuk menutup jarak, bukan dari jarak nol |
| Jeda serangan Penebang (F1/F2/F3) | 70 / 55 / 40 | 75 / 60 / 46 | |
| Darah Jagal Merapi | 820 | 720 | tembok; tetap bos tengah tersulit |
| Kekuatan Jagal Merapi | 1,15 | 1,05 | |
| Interval mekanik Merapi (batu / lava / batu) | 600 / 480 / 300 | 720 / 660 / 420 | kolam dan batu tidak terlalu bertumpuk |
| Jeda serangan Merapi (F1/F2/F3) | 70 / 55 / 40 | 100 / 80 / 65 | lebih sedikit palu = lebih sedikit kolam lava |
| Jarak minimum Amukan Lava Merapi (F2/F3) | 3 | 0 | memberi pilihan jarak dekat selain palu yang meninggalkan lava |
| Darah Patih Abadi | 860 | 940 | bab 6 lebih mudah dari bab 3–5 |
| Interval Serangan Udara Garuda (F2) | 480 | 600 | sapuan laser jadi sumber kerusakan terbesar |
| Bobot gerakan Serangan Udara Garuda (F2/F3) | 2 / 2 | 1 / 1 | sama |
| Darah maksimum Bima | 95 | 105 | persentase menang terendah dengan AI rekan |
| Darah maksimum Mbak Rani | 100 | 110 | terendah dengan kedua pengendali |
| Kekuatan Mbak Rani | 1,0 | 1,05 | sama |

Bang Kodir dan Kapten Badai sudah memenuhi target dan tidak diubah. Semua tes tetap lulus dan build tanpa peringatan.

## Sebelum / sesudah

8 seed per konfigurasi, jadi setiap baris bos berisi 32 sesi (4 karakter x 8 seed) dan setiap persentase punya
derau sekitar ±9 poin.

### Arena bos, bot terampil, solo

| Bab | Bos | Santai | Normal | Jagoan | Durasi Normal (detik) |
|---:|---|---:|---:|---:|---:|
| 1 | Bang Kodir | 100% → **100%** | 97% → **100%** | 81% → **81%** | 49 → 50 |
| 2 | Kapten Badai | 100% → **100%** | 100% → **100%** | 66% → **69%** | 54 → 54 |
| 3 | Nyi Angin Laut | 88% → **100%** | 28% → **47%** | 6% → **41%** | 60 → 56 |
| 4 | Raja Penebang | 78% → **100%** | 44% → **84%** | 41% → **44%** | 65 → 53 |
| 5 | Jagal Merapi | 22% → **69%** | 3% → **12%** | 0% → **3%** | 53 → 73 |
| 6 | Patih Abadi | 100% → **100%** | 78% → **84%** | 53% → **47%** | 47 → 51 |
| 7 | Jenderal Garuda | 75% → **75%** | 12% → **25%** | 3% → **3%** | 58 → 75 |

### Arena bos, bot terampil, co-op

| Bab | Bos | Santai | Normal | Jagoan | Durasi Normal (detik) |
|---:|---|---:|---:|---:|---:|
| 1 | Bang Kodir | 100% → **100%** | 100% → **100%** | 100% → **100%** | 37 → 39 |
| 2 | Kapten Badai | 100% → **100%** | 100% → **100%** | 91% → **97%** | 54 → 48 |
| 3 | Nyi Angin Laut | 97% → **97%** | 25% → **62%** | 6% → **12%** | 77 → 74 |
| 4 | Raja Penebang | 81% → **100%** | 72% → **91%** | 53% → **81%** | 58 → 51 |
| 5 | Jagal Merapi | 12% → **66%** | 12% → **31%** | 0% → **12%** | 61 → 66 |
| 6 | Patih Abadi | 100% → **100%** | 97% → **97%** | 75% → **84%** | 42 → 48 |
| 7 | Jenderal Garuda | 94% → **94%** | 47% → **62%** | 12% → **22%** | 67 → 55 |

### Arena bos, AI rekan, solo

| Bab | Bos | Santai | Normal | Jagoan |
|---:|---|---:|---:|---:|
| 1 | Bang Kodir | 100% → **100%** | 100% → **100%** | 81% → **81%** |
| 2 | Kapten Badai | 100% → **100%** | 69% → **81%** | 12% → **12%** |
| 3 | Nyi Angin Laut | 97% → **97%** | 44% → **50%** | 3% → **16%** |
| 4 | Raja Penebang | 22% → **84%** | 0% → **22%** | 0% → **6%** |
| 5 | Jagal Merapi | 22% → **59%** | 3% → **3%** | 0% → **0%** |
| 6 | Patih Abadi | 94% → **91%** | 66% → **53%** | 31% → **22%** |
| 7 | Jenderal Garuda | 69% → **78%** | 28% → **38%** | 6% → **3%** |

### Per karakter, arena bos, solo, Normal (semua bos)

| Karakter | Bot terampil | AI rekan |
|---|---:|---:|
| Si Jenggo | 64% → **73%** | 59% → **70%** |
| Bima | 66% → **77%** | 20% → **27%** |
| Mbak Rani | 36% → **50%** | 38% → **39%** |
| Ustad Ucup | 41% → **59%** | 61% → **62%** |

### Bab penuh, bot terampil, solo (gelombang, lalu bos)

| Bab | Santai | Normal | Jagoan | Waktu selesai Normal (detik) |
|---:|---:|---:|---:|---:|
| 1 | 97% → **97%** | 94% → **91%** | 59% → **66%** | 164 → 165 |
| 2 | 100% → **100%** | 81% → **84%** | 53% → **53%** | 176 → 177 |
| 3 | 66% → **94%** | 22% → **31%** | 9% → **19%** | 163 → 164 |
| 4 | 38% → **69%** | 12% → **28%** | 9% → **28%** | 184 → 164 |
| 5 | 0% → **31%** | 0% → **3%** | 0% → **0%** | – → 176 |
| 6 | 72% → **88%** | 34% → **34%** | 3% → **3%** | 196 → 189 |
| 7 | 53% → **62%** | 12% → **22%** | 0% → **0%** | 212 → 207 |

## Putaran kedua (perubahan kode)

Kolam lava Merapi diperbaiki di kode setelah putaran data: `CombatSystem` kini memberi kolam Palu Magma peringatan
40 tick dan umur 170 tick (sebelumnya 20 / 300), palu tidak lagi punya `HitsDowned` dan kerusakannya 15 (sebelumnya
18). Lalu darah dua "tembok" terakhir dikurangi. Bot terampil, solo, persentase menang Normal (32 laga, ±9 poin):

| Bos | Perubahan | Sebelum | Sesudah |
|---|---|---:|---:|
| Jagal Merapi | kolam 40/170 tick, palu 18→15 tanpa `HitsDowned`, darah 720→620 | 12% | 44% |
| Jenderal Garuda | darah 1000→900 | 25% | 38% |

Dua bos terakhir memang dibuat paling sulit; pemain mode Cerita juga datang dengan level, jurus, dan perlengkapan
yang tidak dimodelkan sweep ini. `docs/en/balance-report.md` adalah laporan lengkap setelah putaran ini.

## Masih terbuka

Hal-hal berikut berada di luar file data yang boleh diubah pada putaran ini, dan menjadi saran bagi pemilik
`Moves.cs` / `Systems/`:

- ~~**Jagal Merapi masih menjadi tembok**~~ — sudah ditangani pada putaran kedua (lihat di bawah).
- **Bahaya yang membidik pemain yang sedang jatuh** (badai petir, Serangan Udara, pohon tumbang) sering mendarat
  tepat setelah bangun, ketika tidak ada waktu untuk menjauh (kecepatan ke arah kedalaman hanya 2,5–3,3).
  Pertimbangkan untuk melewati pemain yang jatuh saat memilih sasaran, atau menambahkan waktu bangun ke peringatan.
- **Co-op tidak jauh lebih mudah dari solo** melawan bos yang membidik setiap pemain (Nyi, Merapi), karena bos juga
  mendapat +35% darah per pemain tambahan. Perlu dicek bersama manusia.
- **Mbak Rani** masih karakter terlemah dengan bot terampil, dan **Bima** dengan AI rekan (27% dibanding 70% untuk
  Jenggo). Tidak ada bot yang memakai jebakan dan gadget Rani atau serangan balik Bima, jadi kekuatan asli mereka
  tidak terukur dengan cara ini; penguatannya sengaja dibuat kecil.
- **AI rekan** tidak pernah menghindar atau keluar dari bahaya, itulah sebabnya ia sangat buruk melawan Penebang dan
  Merapi. Mengajarinya keluar dari lingkaran `Hazard` (seperti `SkilledBot`) akan membuatnya rekan yang jauh lebih baik.
- **Progresi Mode Cerita tidak dimodelkan**: di bab 5–7 pemain sungguhan sudah punya level, skill, perlengkapan dan
  jurus daerah, jadi seharusnya lebih baik daripada angka statistik dasar ini.

Uji main oleh manusia tetap wajib sebelum angka-angka ini dianggap final.
