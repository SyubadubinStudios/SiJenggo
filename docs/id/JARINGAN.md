# Jaringan

Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios. · [English](../en/NETWORKING.md)

## Model: lockstep deterministik

Kedua komputer menjalankan simulasi penuh. Yang dikirim hanya input (dua byte per pemain per tick).
`LockstepSession` (`src/SiJenggo.Game/Net/Lockstep.cs`):

- Input lokal dijadwalkan `InputDelay` (3) tick ke depan, berurutan tanpa celah.
- Setiap paket membawa 10 frame input terakhir, jadi paket dikirim **tidak andal (unreliable)** dan paket yang
  hilang tidak berpengaruh.
- Satu tick baru disimulasikan jika input semua pemain untuk tick itu sudah ada; jika belum, game menunggu
  ("Menunggu rekan online…").
- Tiap 60 tick, masing-masing peer mengirim `GameSession.Checksum()`; jika berbeda, sesi ditandai desync.

Terverifikasi: tes in-memory dengan 30% paket hilang, dan uji UDP loopback ThreeNet sungguhan (1.800 tick, kedua
peer berakhir dengan checksum identik, round trip ~59 ms).

## Model opsional: rollback

Host bisa memilih netcode rollback (`OnlineLink.NetMode = NetMode.Rollback` sebelum pertandingan dimulai;
pilihan ini ikut di pesan mulai dan peer lain mengikutinya). Lockstep tetap menjadi bawaan.
`RollbackSession` (`src/SiJenggo.Game/Net/Rollback.cs`):

- Input lokal hanya dijadwalkan `InputDelay` (1) tick ke depan. Input rekan yang belum tiba **diprediksi**:
  rekan dianggap tetap menahan tombol yang sama seperti di frame terakhirnya yang sudah pasti.
- Game terus berjalan dengan prediksi sampai `MaxRollback` (8) frame melewati frame terakhir yang semua inputnya
  sudah pasti; baru setelah itu game menunggu seperti lockstep.
- Sebelum menyimulasikan tiap frame, `SessionSnapshot` penuh disimpan ke ring (`MaxRollback + 2` slot). Jika input
  rekan yang tiba berbeda dari yang dipakai, tick berikutnya memulihkan snapshot frame itu lalu menyimulasikan
  ulang sampai saat ini, semuanya dalam satu frame render.
- Paket dikirim tidak andal dan membawa acknowledgement: tiap peer mengirim inputnya mulai dari frame tertua yang
  belum dikonfirmasi rekan (maksimal 64 frame), jadi paket hilang hanya menunda konfirmasi.
- Sinkronisasi waktu sederhana: tiap paket membawa frame pengirim dan selisih frame-nya; peer yang dua frame
  atau lebih di depan sesekali melewatkan satu tick agar tidak ada sisi yang terus rollback dalam-dalam.
- Deteksi desync: tiap 60 tick checksum dikirim **hanya setelah frame itu pasti** (semua input diketahui, rollback
  sudah diterapkan), lalu dibandingkan dengan milik rekan.
- Tiap stage memakai sesi baru dengan byte epoch baru, jadi paket nyasar dari stage sebelumnya diabaikan.
- `OnlineLink.RollbackFrames` dan `OnlineLink.RollbackInputDelay` mengatur jendela dan delay secara lokal (boleh beda antar peer).

### Snapshot

`GameSession.SaveState(SessionSnapshot)` / `LoadState(SessionSnapshot)` (`src/SiJenggo.Game/GameSessionSnapshot.cs`)
menyalin seluruh simulasi: `World.CopyFrom` menyalin versi entitas, penanda hidup, stack id bebas, antrean hapus
dan array dense/sparse setiap pool komponen; sesi menyalin RNG, statistik, kamera, musik, status
director/gelombang, hasil tantangan dan daftar-daftarnya. Komponen berupa struct tanpa referensi, jadi salinan
array datar sudah merupakan salinan penuh. Buffer dipakai ulang: setelah pemanasan (saat `RollbackSession`
dibuat) menyimpan, memuat, maupun tick rollback tidak mengalokasi memori. Terukur saat tes dengan ~20 entitas
hidup: SaveState sekitar 4 sampai 14 µs, LoadState sekitar 4 sampai 15 µs, dibanding sekitar 30 sampai 50 µs untuk
satu `Step`; rollback terburuk 8 frame tetap jauh di bawah satu milidetik.

Tes `RollbackTests.Every_session_field_is_covered_by_the_snapshot` gagal jika field baru ditambahkan ke
`GameSession` tanpa ikut di-snapshot; `GameSession.FullStateHash()` meng-hash byte mentah semua pool dan semua
field sesi, untuk tes dan debugging desync.

### Event presentasi saat rollback

Frame yang disimulasikan ulang sudah pernah ditampilkan ke pemain. Selama simulasi ulang, flag `SuppressEvents`
sesi dinyalakan dan `Emit` membuang event, jadi percikan, suara, guncangan kamera dan notifikasi tidak pernah
diputar dua kali. Konsekuensinya: hasil yang baru muncul setelah koreksi (pukulan yang ternyata kena) tampil
tanpa efeknya; akibatnya (darah, knockback, KO) tentu tetap benar. Event dari timeline yang salah prediksi dan
sudah masuk antrean tetap di antrean. Renderer cukup mengikuti state entitas yang sudah dikoreksi di frame
berikutnya.

### Verifikasi rollback

Tes unit menjalankan dua `RollbackSession` lewat jalur in-memory dengan latensi acak 2 sampai 9 tick (paket bisa
tiba tidak berurutan) dan 20 sampai 35% paket hilang: kedua peer melakukan ratusan rollback, tidak pernah desync,
dan berakhir dengan `Checksum()` serta `FullStateHash()` yang sama dengan sesi referensi yang hanya diberi input
yang sudah pasti. Belum terverifikasi: pertandingan UDP ThreeNet sungguhan dalam mode rollback, dan rasanya pada
latensi nyata (UI untuk memilih mode belum ada). Lewat relay semua paket diteruskan secara andal, sehingga ada
jeda head-of-line pada jalur yang sering kehilangan paket; main langsung tidak mengalami ini.

## Format paket (byte pertama = jenis)

| Jenis | Byte | Arti |
|---|---|---|
| 1 Input | `[1][slot][frameAwal:int32][jumlah][jumlah × uint16 tombol]` | riwayat input redundan |
| 2 Checksum | `[2][slot][frame:int32][checksum:uint32]` | deteksi desync (andal) |
| 3 Mulai | `[3][seed:uint64][mode][bab][kesulitan][n][n × karakter][netMode]` | pengaturan pertandingan dari pemain satu (andal); `netMode` 0 = lockstep, 1 = rollback, opsional |
| 4 Input rollback | `[4][slot][epoch][n][n × ack:int32][frame:int32][selisih:int8][frameAwal:int32][jumlah][jumlah × uint16]` | rollback: riwayat input yang belum dikonfirmasi plus ack dan sinkronisasi waktu |
| 5 Checksum rollback | `[5][slot][epoch][tick:int32][checksum:uint32]` | rollback: checksum frame yang sudah pasti (andal) |
| 10 Peran | `[10][slot]` | relay → klien: slot pemainmu |
| 11 Peer | `[11][jumlah]` | relay → klien: jumlah pemain terhubung |

## Main online

- **Langsung**: satu pemain memilih *Buat ruang (host)* (port UDP 27960 secara bawaan), yang lain *Gabung ke host*
  dengan alamat host. Host memilih mode.
- **Relay** (`SiJenggo.Server`): jalankan `dotnet run --project src/SiJenggo.Server -- 27960` di mesin yang bisa
  dijangkau keduanya; kedua pemain *Gabung* ke sana. Relay membagi slot, mengumumkan peer, dan meneruskan paket;
  relay tidak pernah menjalankan simulasi.

Pertandingan online memakai statistik dasar karakter di kedua sisi agar kedua simulasi identik.
