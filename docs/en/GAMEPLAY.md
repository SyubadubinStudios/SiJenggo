# Gameplay

Made by Ariana Mischa Fadhila of Syubadubin Studios. · [Bahasa Indonesia](../id/GAMEPLAY.md)

All numbers live in `src/SiJenggo.Game/Data/`. Timings are 60 Hz ticks, distances metres.

## Space

The street runs along X. Z is the depth lane (±3.2 m). Y is height. The camera scrolls right only and
locks when a wave starts; a blinking **GO →** appears when the wave is cleared.

## Fighting

| Input | Effect |
|---|---|
| Light ×N | Character combo chain (4–5 hits); each hit can cancel into the next after its cancel frame |
| Heavy | Heavy attack; during a chain after a landed hit it becomes the finisher; while walking forward it is the dash attack |
| Jump, then Light/Heavy | Air attack / dive (one per jump) |
| Special (+ direction) | Neutral, forward and up specials cost **Semangat** (spirit, regenerates 8/s). Down = the equipped *jurus daerah* |
| Grab near an enemy | Grab: Light = knee (up to 3), Heavy or 4th Light = throw (hold back for a back throw). Thrown enemies hit others |
| Grab near a prop | Lift it; Light/Heavy/Grab throws it. Heavy props (scooter, cart, boulder) need Jenggo's strength |
| Grab near a weapon | Pick it up; the weapon replaces the light chain and heavy, wears out per hit, and can be thrown (Grab) |
| Dodge | Invulnerable roll. **Dodge + Light = counter stance**: an attack landing in its window is reversed |
| Rage | See below |

Hits apply hit-stop to both sides, knockback scaled by weight, and either hit-stun, a juggle (launchers, air
hits) or a knockdown. Juggles decay with each hit. Armoured enemies (enforcers, robots, bosses) absorb a
number of hits before flinching, and moves with super armour never flinch.

## Rage Nusantara

The meter fills by **dealing** damage (per move) and **taking** damage. At 100 press Rage:
×1.35 damage, ×1.1 attack speed, specials cost nothing, the light chain loops forever, a flame aura appears and
the music goes all-in. Pressing Rage again during it unleashes the character's **ultimate**
(Murka Nusantara, Seribu Bayangan, Operasi Elang Malam, Ceramah Kiamat).

## Co-op

- **Double grab**: grab an enemy your partner is holding → a combined slam.
- **Air launch combo**: hitting an enemy your partner launched does ×1.3 damage.
- **Team finisher (Gotong Royong!)**: both players at ≥50 rage, close together, one presses Rage.
- **Revive**: a player at zero health stays down for 12 s; the partner holds Grab beside them for 1.25 s.
- **Player + AI**: the AI partner presses buttons through the same code as a human (fair and deterministic).

## Enemies and AI

Thugs, knife thugs, enforcers (armour), silat bruisers, bottle throwers (keep distance), Garuda ninjas
(dash, shuriken), Garuda mechs (armour, lasers) and the Patih's shadows. At most 2 enemies (1 on Santai,
3 on Jagoan) hold an **attack token** per player; the rest circle in slots and pick up weapons from the floor.

## Bosses

Each boss has three phases (66% and 33% health). A phase change roars, sends a shockwave, smashes props
nearby, restores armour and switches the attack pattern and arena mechanic.

| Chapter | Boss | Arena mechanics |
|---|---|---|
| 1 Jakarta Kumuh | Bang Kodir | summons thugs, charge, earth stomp |
| 2 Pelabuhan | Kapten Badai | crate drops, harpoon, anchor swing |
| 3 Pantai Selatan | Nyi Angin Laut | lightning storm, wind vortex |
| 4 Hutan Kalimantan | Raja Penebang Ilegal | wild boars, falling trees, chainsaw |
| 5 Gunung Berapi | Jagal Merapi | falling rocks, lava eruption, boulder throw |
| 6 Candi Kuno | Patih Abadi | shadow clones, mystic shield, mystic wave |
| 7 Kota Futuristik | Jenderal Garuda Hitam | ninjas and mechs, air strikes, wing dive |

## Progression

- XP from every KO (shared). Level curve in `Leveling`; one skill point per level (+1 every fifth).
- Skill tree: five branches × four tiers (`SkillTree`). Tiers need the previous tier.
- Gear: four slots, drops from bosses (`Equipment`). Jurus daerah: one per chapter cleared in Story.
- Rani unlocks after chapter 3, Ucup after chapter 5.
- Arcade ignores progression (pure skill); Story, Survival, Boss Rush and Challenges use it.

## Modes

| Mode | Rules |
|---|---|
| Story | One chapter at a time, story cards, unlocks |
| Arcade | All seven chapters, three lives, high score |
| Survival | Endless waves in a chapter's arena; a half-health boss every fifth wave |
| Boss Rush | Seven boss arenas; best total time |
| Challenges | Eight missions (KOs in time, weapon/prop KOs, combo length, counters, no-damage boss, timed boss, waves) |

Seventeen achievements are checked after every stage (`Persistence/Achievements.cs`).
