# Si Jenggo – Preman Berhati Malaikat
## Solution Design Document

Version: 1.0
Target Platform: Windows, Linux, macOS, Android, iOS
Technology Stack: .NET, Avalonia UI, ThreeNet (Vibe_Graphics), MCP Blender, MCP Rodin
Genre: 3D Side-Scrolling Beat 'em Up / Action Adventure

---

# 1. Vision

Si Jenggo adalah spiritual successor modern dari Bare Knuckle (Streets of Rage), Golden Axe, dan Double Dragon yang dihadirkan dalam dunia 3D dengan identitas Indonesia yang kuat.

Pemain akan menjelajahi berbagai wilayah Indonesia mulai dari gang kumuh Jakarta, pelabuhan, pasar tradisional, pantai eksotis, hutan tropis, gunung berapi, hingga kompleks candi kuno yang menyimpan rahasia besar.

Fokus utama:

- Combat brutal namun fun
- Co-op lokal dan online
- Atmosfer Indonesia yang autentik
- Boss fight epik
- Humor khas Indonesia
- Cerita penuh aksi dan emosi
- VFX spektakuler
- Soundtrack rock, blues dan metal yang dinamis

---

# 2. High Level Story

Nama asli Si Jenggo adalah Jenggo Prasetyo.

Mantan preman legendaris Jakarta yang pernah menguasai berbagai wilayah keras ibu kota.

Setelah menyelamatkan seorang anak kecil dari kerusuhan jalanan, hidupnya berubah.

Ia memilih membantu masyarakat kecil.

Namun organisasi kriminal terbesar Indonesia:

THE GARUDA HITAM

muncul dan mulai menguasai:

- Pelabuhan
- Pertambangan ilegal
- Penebangan liar
- Perdagangan artefak kuno
- Narkoba
- Korupsi daerah

Ketika kampung tempat Jenggo tinggal dibakar, dia kembali turun ke jalan.

Misinya:

Membongkar Garuda Hitam dari Jakarta hingga ke pulau-pulau terpencil Indonesia.

---

# 3. Playable Characters

## Player 1 – Si Jenggo

Style:

- Power Brawler
- Grappler
- Crowd Control

Special:

- Tendangan Putar Betawi
- Bantingan Monas
- Tinju Petir Kampung

Ultimate:

Murka Nusantara

---

## Player 2 – Bima

Mantan atlet pencak silat.

Style:

- Fast Combo
- Counter Attack
- Mobility

Ultimate:

Seribu Bayangan

---

## Unlockable Character – Mbak Rani

Mantan anggota intel.

Style:

- Tactical
- Trap
- Gadget

Ultimate:

Operasi Elang Malam

---

## Unlockable Character – Ustad Ucup

Karakter humor.

Style:

- Weird Fighter
- Improvised Weapons

Ultimate:

Ceramah Kiamat

Musuh stun massal.

---

# 4. Core Gameplay

## Combat

Kombinasi:

40% Bare Knuckle
30% Double Dragon
30% Golden Axe

Fitur:

- Light attack
- Heavy attack
- Air combo
- Throw
- Counter
- Dodge
- Finisher
- Environmental attack
- Weapon pickup

---

## Rage System

Saat menerima atau memberi damage.

Mengisi meter:

RAGE NUSANTARA

Saat aktif:

- Damage naik
- VFX api merah emas
- Combo baru terbuka
- Musik menjadi lebih agresif

---

## Co-op System

Mode:

- Single Player
- 2 Player Local
- 2 Player Online
- Player + AI

Co-op Combo:

- Double Grab
- Air Launch Combo
- Team Finisher
- Revive System

---

# 5. Unique Indonesian Combat Features

## Jurus Daerah

Setiap chapter memberikan jurus khas daerah.

Contoh:

- Silat Betawi
- Debus Banten
- Pencak Jawa Barat
- Silat Minang
- Silat Bugis
- Silat Bali

---

## Senjata Tradisional

- Golok
- Keris
- Kujang
- Celurit
- Rencong
- Tombak Dayak
- Mandau

Setiap senjata memiliki combo unik.

---

# 6. World Progression

## Chapter 1
Jakarta Kumuh

Area:

- Gang sempit
- Terminal liar
- Pasar malam
- Rumah susun

Boss:

Bang Kodir

Raja Preman Jakarta.

---

## Chapter 2
Pelabuhan Tanjung Harapan

Area:

- Kontainer
- Gudang gelap
- Kapal besar

Boss:

 Kapten Badai

---

## Chapter 3
Pantai Selatan

VFX:

- Ombak besar
- Petir
- Hujan deras

Boss:

Nyi Angin Laut

---

## Chapter 4
Hutan Kalimantan

Fitur:

- Kabut volumetrik
- Pohon raksasa
- Hewan liar

Boss:

Raja Penebang Ilegal

---

## Chapter 5
Gunung Berapi

Fitur:

- Lava
- Batu jatuh
- Abu vulkanik

Boss:

Jagal Merapi

---

## Chapter 6
 Candi Kuno Nusantara

Dungeon misterius.

Boss:

Patih Abadi

Guardian kuno.

---

## Chapter 7
Kota Futuristik Garuda Hitam

Markas akhir.

Boss:

Jenderal Garuda Hitam

---

# 7. Boss Design Philosophy

Setiap boss memiliki:

- 3 phase
- Unique mechanic
- Destruction sequence
- Cinematic intro
- Cinematic finish

---

# 8. Environmental Interaction

Objek yang bisa digunakan:

- Kursi
- Motor
- Gerobak
- Tong sampah
- Bambu
- Pagar
- Peti kayu
- Batu besar

Physics driven destruction.

---

# 9. RPG Lite Features

## Skill Tree

Power
Speed
Defense
Technique
Rage

---

## Equipment

- Jaket
- Sabuk
- Sarung tangan
- Kalung pusaka

Memberikan bonus statistik.

---

# 10. VFX System

ThreeNet Showcase Features

## Combat VFX

- Motion trail
- Energy slash
- Shockwave
- Sparks
- Ground cracks

## Weather VFX

- Tropical rain
- Thunderstorm
- Ocean spray
- Mist
- Sand wind
- Volcanic ash

## Advanced Effects

- Screen shake
- Dynamic lighting
- Volumetric fog
- Bloom
- HDR
- SSAO
- SSR
- God rays
- Particles

---

# 11. Audio Design

## Jakarta

- Funk rock
- Metal blues

## Pantai

- Surf rock
- Blues guitar

## Hutan

- Tribal percussion
- Ethnic guitar

## Candi

- Gamelan cinematic metal

## Final Boss

- Symphonic metal
- Epic guitar solo

Dynamic soundtrack system.

---

# 12. Game Modes

## Story Mode

Campaign utama.

## Arcade Mode

Langsung bertarung.

## Survival Mode

Gelombang tanpa henti.

## Boss Rush

Semua boss berurutan.

## Challenge Mode

Misi spesial.

---

# 13. Progression Loop

Fight
↓
Gain XP
↓
Unlock Skills
↓
Collect Weapons
↓
Improve Character
↓
Fight Bigger Bosses
↓
Unlock New Regions

---

# 14. Visual Art Direction

Style:

- Semi realistic
- Stylized realism
- Colorful but gritty

Inspirasi:

- Streets of Rage 4
- Yakuza
- Sleeping Dogs
- Sifu

Namun dengan identitas Indonesia yang sangat kuat.

---

# 15. Technical Architecture

Frontend:

- Avalonia UI

Game Engine Layer:

- ThreeNet
- ECS Architecture
- Physics Module
- Audio Module

Tools:

- MCP Blender
- MCP Rodin

Backend:

- Save System
- Profile System
- Achievement System

Multiplayer:

- Peer-to-peer
- Dedicated server optional

---

# 16. Asset Pipeline

Concept Art
↓
MCP Rodin
↓
3D Mesh
↓
Blender MCP
↓
Rigging
↓
Animation
↓
LOD Generation
↓
ThreeNet Import
↓
Optimization

---

# 17. Post Launch Roadmap

Season 1

- Bali Expansion
- Barong Boss
- Pantai Kuta Stage

Season 2

- Papua Expansion
- Jungle Warfare

Season 3

- Underwater Temple
- Mythological Creatures

---

# 18. Key Selling Points

1. Beat'em up 3D pertama dengan skala lokasi Indonesia yang luas.
2. Kombinasi Bare Knuckle, Golden Axe dan Double Dragon modern.
3. Visual ThreeNet dengan efek cuaca dan destruction spektakuler.
4. Co-op sangat seru dimainkan bersama teman.
5. Soundtrack rock, blues dan metal khas tiap daerah.
6. Banyak humor, budaya dan referensi Indonesia.
7. Boss fight yang sinematik dan memorable.
8. Sangat cocok untuk PC dan mobile modern.

---

# Tagline

"Ketika preman terakhir memilih melindungi rakyat, seluruh Garuda Hitam harus tumbang."

SI JENGGO
PREMAN BERHATI MALAIKAT
