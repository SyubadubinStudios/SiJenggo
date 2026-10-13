# Balance sweep

Made by Ariana Mischa Fadhila of Syubadubin Studios. · [Versi Bahasa Indonesia](../id/KESEIMBANGAN.md)

`tools/SiJenggo.Balance` is a headless console tool that plays every boss (and, optionally, every full chapter)
with every character, on Santai, Normal and Jagoan, solo and in two-player co-op, over several seeds, and writes a
markdown report ([balance-report.md](balance-report.md)). The simulation needs no GPU and every session is
independent, so the sweep runs in parallel on all cores.

> **This does not replace human playtesting.** Both drivers are bots. They read the game state perfectly,
> do not learn, never block, rarely use specials well and ignore most of each character's kit. Treat the numbers
> as a way to find outliers and to check that a change moves things in the right direction, not as the
> experience a player will have. PLAN.md Milestone 7 item 1 (human playtesting) stays open.

## Running it

```bash
dotnet run -c Release --project tools/SiJenggo.Balance                 # boss arenas -> docs/en/balance-report.md
dotnet run -c Release --project tools/SiJenggo.Balance -- --chapters   # also every full chapter (waves + boss)
dotnet run -c Release --project tools/SiJenggo.Balance -- --chapter 4 --bot skilled --stdout   # one boss, print only
```

| Option | Meaning |
|---|---|
| `--seeds N` | seeds per configuration (default 8) |
| `--chapters` / `--only-chapters` | add / only run full chapters (waves, then the boss) |
| `--chapter I[,J]` | only these chapters (0-based) |
| `--bot skilled,companion` | which drivers to run (default both) |
| `--boss-cap S` / `--chapter-cap S` | tick cap in seconds (defaults 600 / 1500); a capped run counts as a loss |
| `--threads N` | worker threads (default: all cores) |
| `--out PATH` / `--stdout` | where the report goes |
| `--csv PATH` | one raw row per session, for before/after comparisons |

Timing on an 8-thread desktop (Release): the default sweep (7 bosses x 4 characters x 3 difficulties x solo/co-op
x 2 drivers x 8 seeds = 2,688 sessions, ~49 simulated hours) takes about **7 s**; with `--chapters` (5,376
sessions, ~187 simulated hours) about **23 s**. The output is deterministic: the same build gives the same report
(apart from the wall-time line), whatever the thread count.

To compare a tuning change: run once with `--csv before.csv`, change the data, run again with `--csv after.csv`,
and diff the two (the tables below were made that way).

## What a run records

Each session is a `GameSession` built like the game builds it: `StageKind.BossArena` for a boss (fresh players,
3 lives, `Scaling = 1`, so it matches the Story boss rather than the scaled Boss Rush) or `StageKind.Chapter` for a
full chapter. Players use `PlayerLoadout.Base` (no levels, skills or gear). The co-op partner is the next
character in roster order (Jenggo+Bima, Bima+Rani, Rani+Ucup, Ucup+Jenggo).

Per run: win / loss / timeout, time to clear, boss time per phase (intro excluded), boss health left on a loss,
damage taken by players, deaths (lives lost, so a loss is 4 per player), and the damage split by source, taken
from the `Hit` events the session already emits: the boss itself (by move), other enemies (minions, shadow clones,
wave enemies), and hazards (by `HazardKind`, attributed to the hazard under the victim).

Two drivers:

- **Skilled bot** (`SkilledBot.cs`, the main signal): presses buttons for a normal player slot. It walks out of
  hazard circles (choosing the nearby spot covered by the fewest), does not start a combo next to a pending hazard,
  and dodges an enemy attack it can see coming, but only after a 12-tick (200 ms) reaction delay and only 60% of
  the time. Fast jabs are therefore effectively unreactable, as for a human.
- **Companion AI** (`PlayerLoadout.IsAi`, the in-game partner): never dodges, blocks or avoids hazards. It is the
  pessimistic case and shows what an AI partner in co-op feels like.

## Targets

`solution-design.md` sets no numeric targets (only "3 phases, unique mechanic" per boss), so these are derived from
usual beat 'em up practice and written for the skilled bot with base stats:

1. **Difficulty is monotonic**: win% Santai >= Normal >= Jagoan for every boss.
2. **Bosses get harder through the story**, without a mid-game wall: no boss clearly harder than the ones after it.
   Base-stat bots get no Story progression, so a steady decline in win% is expected and fine.
3. **No dramatically weaker character**: per-character Normal win% within about 25 points of each other.
4. **Fight length**: a won Normal boss fight 45–150 s. The bots attack non-stop, so their fights sit at the short
   end; humans will be slower. Boss health was not raised for this reason.
5. Keep changes modest (roughly 15% or less per value) and only in data (`Bosses.cs`, `Characters.cs`,
   `Enemies.cs`), so ids, ordering and strings stay untouched.

## What the first sweep found

- **Jagal Merapi (ch5)** was a wall: 3% skilled / 3% companion solo Normal, 0% on Jagoan. 60–78% of the damage came
  from lava pools: every Magma Hammer swing leaves a 300-tick pool right in melee range (20-tick warning), and in
  melee the hammer was his only eligible attack.
- **Raja Penebang (ch4)**: Chainsaw Rush (7 hits of 5 damage with little knockback) was used point-blank, 44%
  skilled / 0% companion on Normal.
- **Nyi Angin Laut (ch3)**: lightning storm and Wind Vortex (an area attack too big to dodge out of) made her
  harder than chapters 4 and 6 (28% skilled Normal).
- **Patih Abadi (ch6)** was easier than chapters 3–5 for both drivers.
- **Jenderal Garuda (ch7)**: Air Strike lays five overlapping laser sweeps along X; escaping means walking out in
  depth, which is slow, so it was the top damage source (12% skilled Normal).
- **Characters**: Bima (95 HP, 0.9 defence) and Rani were far behind with the companion AI (20% and 38% vs ~60% for
  Jenggo and Ucup); with the skilled bot Rani and Ucup lagged (36% / 41% vs 64–66%).
- Minions and wave enemies were never the problem (at most 13% of boss-arena damage), so `Enemies.cs` is unchanged.

## Tuning changes

| What | Before | After | Why |
|---|---|---|---|
| Nyi Angin Laut health | 600 | 560 | ch3 was harder than ch4 and ch6 |
| Nyi lightning storm interval (P1/P2/P3, ticks) | 540 / 360 / 240 | 600 / 450 / 330 | lightning was ~30% of the damage |
| Nyi Badai Petir weight (P2/P3) | 2 / 3 | 1 / 2 | fewer extra lightning casts |
| Nyi Wind Vortex weight (P1/P2/P3) | 1 / 2 / 3 | 0.5 / 0.75 / 1 | area too large to dodge; top boss move |
| Raja Penebang power | 1.1 | 1.05 | |
| Penebang Chainsaw Rush weight (P1/P2/P3) | 2 / 2 / 3 | 1 / 1 / 2 | multi-hit lock was the top damage source |
| Penebang Chainsaw Rush min range | 0 | 1.8 | it is a rush: use it to close distance, not point-blank |
| Penebang attack cooldown (P1/P2/P3) | 70 / 55 / 40 | 75 / 60 / 46 | |
| Jagal Merapi health | 820 | 720 | the wall; still the hardest mid-game boss |
| Jagal Merapi power | 1.15 | 1.05 | |
| Merapi mechanic interval (rocks / lava / rocks) | 600 / 480 / 300 | 720 / 660 / 420 | fewer overlapping pools and rocks |
| Merapi attack cooldown (P1/P2/P3) | 70 / 55 / 40 | 100 / 80 / 65 | fewer hammers = fewer lava pools |
| Merapi Lava Rampage min range (P2/P3) | 3 | 0 | gives him a melee option besides the lava-leaving hammer |
| Patih Abadi health | 860 | 940 | ch6 was easier than ch3–5 |
| Garuda Air Strike interval (P2) | 480 | 600 | sweeps were the top damage source |
| Garuda Serangan Udara weight (P2/P3) | 2 / 2 | 1 / 1 | same |
| Bima max health | 95 | 105 | lowest win rate with the companion AI |
| Mbak Rani max health | 100 | 110 | lowest win rate with both drivers |
| Mbak Rani power | 1.0 | 1.05 | same |

Bang Kodir and Kapten Badai already met the targets and are unchanged. Every test still passes and the build has
0 warnings.

## Before / after

8 seeds per configuration, so each boss row is 32 runs (4 characters x 8 seeds) and a single percentage carries
roughly ±9 points of noise.

### Boss arenas, skilled bot, solo

| Ch | Boss | Santai win | Normal win | Jagoan win | Normal fight s |
|---:|---|---:|---:|---:|---:|
| 1 | Bang Kodir | 100% → **100%** | 97% → **100%** | 81% → **81%** | 49 → 50 |
| 2 | Kapten Badai | 100% → **100%** | 100% → **100%** | 66% → **69%** | 54 → 54 |
| 3 | Nyi Angin Laut | 88% → **100%** | 28% → **47%** | 6% → **41%** | 60 → 56 |
| 4 | Raja Penebang | 78% → **100%** | 44% → **84%** | 41% → **44%** | 65 → 53 |
| 5 | Jagal Merapi | 22% → **69%** | 3% → **12%** | 0% → **3%** | 53 → 73 |
| 6 | Patih Abadi | 100% → **100%** | 78% → **84%** | 53% → **47%** | 47 → 51 |
| 7 | Jenderal Garuda | 75% → **75%** | 12% → **25%** | 3% → **3%** | 58 → 75 |

### Boss arenas, skilled bot, co-op

| Ch | Boss | Santai win | Normal win | Jagoan win | Normal fight s |
|---:|---|---:|---:|---:|---:|
| 1 | Bang Kodir | 100% → **100%** | 100% → **100%** | 100% → **100%** | 37 → 39 |
| 2 | Kapten Badai | 100% → **100%** | 100% → **100%** | 91% → **97%** | 54 → 48 |
| 3 | Nyi Angin Laut | 97% → **97%** | 25% → **62%** | 6% → **12%** | 77 → 74 |
| 4 | Raja Penebang | 81% → **100%** | 72% → **91%** | 53% → **81%** | 58 → 51 |
| 5 | Jagal Merapi | 12% → **66%** | 12% → **31%** | 0% → **12%** | 61 → 66 |
| 6 | Patih Abadi | 100% → **100%** | 97% → **97%** | 75% → **84%** | 42 → 48 |
| 7 | Jenderal Garuda | 94% → **94%** | 47% → **62%** | 12% → **22%** | 67 → 55 |

### Boss arenas, companion AI, solo

| Ch | Boss | Santai win | Normal win | Jagoan win |
|---:|---|---:|---:|---:|
| 1 | Bang Kodir | 100% → **100%** | 100% → **100%** | 81% → **81%** |
| 2 | Kapten Badai | 100% → **100%** | 69% → **81%** | 12% → **12%** |
| 3 | Nyi Angin Laut | 97% → **97%** | 44% → **50%** | 3% → **16%** |
| 4 | Raja Penebang | 22% → **84%** | 0% → **22%** | 0% → **6%** |
| 5 | Jagal Merapi | 22% → **59%** | 3% → **3%** | 0% → **0%** |
| 6 | Patih Abadi | 94% → **91%** | 66% → **53%** | 31% → **22%** |
| 7 | Jenderal Garuda | 69% → **78%** | 28% → **38%** | 6% → **3%** |

### Per character, boss arenas, solo, Normal (all bosses)

| Character | Skilled bot | Companion AI |
|---|---:|---:|
| Si Jenggo | 64% → **73%** | 59% → **70%** |
| Bima | 66% → **77%** | 20% → **27%** |
| Mbak Rani | 36% → **50%** | 38% → **39%** |
| Ustad Ucup | 41% → **59%** | 61% → **62%** |

### Full chapters, skilled bot, solo (waves, then the boss)

| Ch | Santai win | Normal win | Jagoan win | Normal clear s |
|---:|---:|---:|---:|---:|
| 1 | 97% → **97%** | 94% → **91%** | 59% → **66%** | 164 → 165 |
| 2 | 100% → **100%** | 81% → **84%** | 53% → **53%** | 176 → 177 |
| 3 | 66% → **94%** | 22% → **31%** | 9% → **19%** | 163 → 164 |
| 4 | 38% → **69%** | 12% → **28%** | 9% → **28%** | 184 → 164 |
| 5 | 0% → **31%** | 0% → **3%** | 0% → **0%** | – → 176 |
| 6 | 72% → **88%** | 34% → **34%** | 3% → **3%** | 196 → 189 |
| 7 | 53% → **62%** | 12% → **22%** | 0% → **0%** | 212 → 207 |

## Second pass (code changes)

The Merapi pool was fixed in code after the data pass: `CombatSystem` now gives the Magma Hammer pool a 40-tick
warning and a 170-tick life (was 20 / 300), and the hammer no longer has `HitsDowned` and does 15 damage (was 18).
Health was then trimmed for the two late walls. Skilled bot, solo, Normal win rate (32 runs, ±9 points):

| Boss | Change | Before | After |
|---|---|---:|---:|
| Jagal Merapi | pool 40/170 ticks, hammer 18→15 without `HitsDowned`, health 720→620 | 12% | 44% |
| Jenderal Garuda | health 1000→900 | 25% | 38% |

The last two bosses are meant to stay the hardest; Story players also arrive with levels, skills and gear that the
sweep does not model. `docs/en/balance-report.md` is the full report after this pass.

## Still open

These are outside the data files this pass was allowed to touch, and are recommendations for whoever owns
`Moves.cs` / `Systems/`:

- ~~**Jagal Merapi is still the wall**~~ — addressed in a second pass (see below).
- **Hazards aimed at a knocked-down player** (lightning storm, Air Strike, falling trees) often land right after
  get-up, when there is no time to walk out (depth speed is only 2.5–3.3). Consider skipping downed players when
  picking targets, or adding the get-up time to the warning.
- **Co-op is not much easier than solo** against bosses that target every player (Nyi, Merapi), since the boss also
  gets +35% health per extra player. Worth checking with humans.
- **Mbak Rani** is still the weakest character with the skilled bot, and **Bima** with the companion AI (27% vs
  70% for Jenggo). Neither bot uses Rani's traps and gadgets or Bima's counters, so their real strength cannot be
  measured this way; the buffs were kept small on purpose.
- **The companion AI** never dodges or steps out of hazards, which is why it does so badly against Penebang and
  Merapi. Teaching it to step out of `Hazard` circles (as `SkilledBot` does) would make it a far better partner.
- **Story progression is not modelled**: by chapter 5–7 real players have levels, skills, gear and a regional style,
  so they should do better than these base-stat numbers.

Human playtesting is still required before any of these numbers are final.
