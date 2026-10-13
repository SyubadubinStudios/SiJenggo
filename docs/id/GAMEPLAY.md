# Gameplay

Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios. · [English](../en/GAMEPLAY.md)

Semua angka ada di `src/SiJenggo.Game/Data/`. Waktu dalam tick 60 Hz, jarak dalam meter.

## Ruang

Jalanan membentang di sumbu X. Z adalah lajur kedalaman (±3,2 m). Y adalah ketinggian. Kamera hanya bergerak ke
kanan dan terkunci saat gelombang musuh datang; tulisan **MAJU →** berkedip saat gelombang habis.

## Bertarung

| Input | Efek |
|---|---|
| Ringan ×N | Rantai kombo karakter (4–5 pukulan); tiap pukulan bisa disambung setelah frame batalnya |
| Berat | Serangan berat; di tengah kombo setelah kena menjadi penutup; sambil berjalan maju menjadi serudukan |
| Lompat, lalu Ringan/Berat | Serangan udara / tukikan (satu per lompatan) |
| Spesial (+ arah) | Jurus netral, maju, dan atas memakai **Semangat** (pulih 8/detik). Bawah = *jurus daerah* yang dipasang |
| Pegang dekat musuh | Kuncian: Ringan = dengkul (sampai 3), Berat atau Ringan ke-4 = bantingan (tahan arah belakang untuk bantingan belakang). Musuh yang dilempar menabrak musuh lain |
| Pegang dekat prop | Angkat; Ringan/Berat/Pegang melemparnya. Prop berat (motor, gerobak, batu) butuh tenaga Jenggo |
| Pegang dekat senjata | Ambil; senjata mengganti kombo ringan dan serangan berat, aus tiap kena, dan bisa dilempar |
| Menghindar | Guling kebal. **Menghindar + Ringan = kuda-kuda tangkis**: serangan yang masuk di jendelanya dibalas |

Pukulan memberi hit-stop pada kedua pihak, dorongan sesuai berat, lalu hit-stun, juggle (peluncur, pukulan di udara),
atau jatuh. Juggle melemah tiap pukulan. Musuh berzirah (tukang pukul, robot, bos) menahan beberapa pukulan
sebelum goyah, dan jurus berzirah super tidak pernah goyah.

## Rage Nusantara

Meter terisi saat **memberi** kerusakan (per jurus) dan **menerima** kerusakan. Saat penuh, tekan Rage:
kerusakan ×1,35, kecepatan serang ×1,1, jurus spesial gratis, kombo ringan tanpa akhir, aura api, dan musik
memuncak. Tekan Rage sekali lagi saat aktif untuk **jurus pamungkas** (Murka Nusantara, Seribu Bayangan,
Operasi Elang Malam, Ceramah Kiamat).

## Co-op

- **Bantingan ganda**: pegang musuh yang sedang dikunci rekan → bantingan gabungan.
- **Kombo udara bersama**: memukul musuh yang diluncurkan rekan memberi kerusakan ×1,3.
- **Gotong Royong!**: kedua pemain rage ≥50, berdekatan, salah satu menekan Rage.
- **Membangunkan**: pemain yang darahnya habis tumbang 12 detik; rekan menahan Pegang di sampingnya 1,25 detik.
- **Pemain + AI**: partner AI menekan tombol lewat kode yang sama dengan manusia (adil dan deterministik).

## Musuh dan AI

Preman, preman pisau, tukang pukul (berzirah), jagoan silat, pelempar botol (menjaga jarak), ninja Garuda
(serudukan, shuriken), robot Garuda (berzirah, laser), dan bayangan Patih. Paling banyak 2 musuh (1 di Santai,
3 di Jagoan) memegang **token serang** per pemain; sisanya berputar di posisi tunggu dan memungut senjata dari lantai.

## Bos

Tiap bos punya tiga fase (66% dan 33% darah). Saat ganti fase, bos meraung, mengirim gelombang kejut, menghancurkan
prop di dekatnya, memulihkan zirah, dan mengganti pola serangan serta mekanik arena.

| Bab | Bos | Mekanik arena |
|---|---|---|
| 1 Jakarta Kumuh | Bang Kodir | memanggil preman, serudukan, hentakan bumi |
| 2 Pelabuhan | Kapten Badai | peti jatuh, tombak ikan, ayunan jangkar |
| 3 Pantai Selatan | Nyi Angin Laut | badai petir, pusaran angin |
| 4 Hutan Kalimantan | Raja Penebang Ilegal | babi hutan, pohon tumbang, gergaji mesin |
| 5 Gunung Berapi | Jagal Merapi | batu jatuh, semburan lava, lemparan batu |
| 6 Candi Kuno | Patih Abadi | bayangan kembar, perisai gaib, gelombang gaib |
| 7 Kota Futuristik | Jenderal Garuda Hitam | ninja dan robot, serangan udara, tukikan sayap |

## Progresi

- XP dari setiap KO (dibagi). Kurva level di `Leveling`; satu poin jurus per level (+1 tiap level kelipatan lima).
- Pohon jurus: lima cabang × empat tingkat. Tingkat butuh tingkat sebelumnya.
- Perlengkapan: empat slot, rampasan bos. Jurus daerah: satu per bab yang selesai di Mode Cerita.
- Rani terbuka setelah bab 3, Ucup setelah bab 5.
- Arcade mengabaikan progresi (murni skill); Cerita, Bertahan, Boss Rush, dan Tantangan memakainya.

## Mode

| Mode | Aturan |
|---|---|
| Cerita | Satu bab sekali main, kartu cerita, membuka konten |
| Arcade | Ketujuh bab berturut-turut, tiga nyawa, skor tertinggi |
| Bertahan | Gelombang tanpa henti di arena sebuah bab; bos setengah darah tiap lima gelombang |
| Boss Rush | Tujuh arena bos; waktu total terbaik |
| Tantangan | Delapan misi (KO berwaktu, KO senjata/prop, panjang kombo, tangkisan, bos tanpa tergores, bos berwaktu, gelombang) |

Tujuh belas pencapaian diperiksa setiap selesai tahap (`Persistence/Achievements.cs`).
