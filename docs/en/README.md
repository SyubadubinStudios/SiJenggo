# Si Jenggo — Documentation (English)

Made by Ariana Mischa Fadhila of Syubadubin Studios. · [Versi Bahasa Indonesia](../id/README.md)

| Document | What it covers |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Projects, the ECS, the fixed-step simulation, how rendering mirrors it, performance rules |
| [GAMEPLAY.md](GAMEPLAY.md) | Combat rules, rage, specials, co-op, AI, bosses, chapters, progression, modes |
| [ASSET-PIPELINE.md](ASSET-PIPELINE.md) | Rodin → Blender (rig, animate, LOD) → ThreeNet, and how to add a model |
| [NETWORKING.md](NETWORKING.md) | Deterministic lockstep and rollback, the wire format, the relay server |
| [BALANCE.md](BALANCE.md) | Headless AI balance sweep, targets, tuning history ([report](balance-report.md)) |
| [../../PLAN.md](../../PLAN.md) | Roadmap |
| [../../Progress.md](../../Progress.md) | What is done and verified |

## Quick start

```bash
dotnet run --project src/SiJenggo.App
```

Graphics quality can be forced from the command line: `--quality 0` (low, for integrated GPUs) to `--quality 3`.

## Screenshots

| | |
|---|---|
| ![](../screenshots/05-story-intro.png) In-engine chapter cutscene | ![](../screenshots/07-pause-controls.png) Pause menu with controls |
| ![](../screenshots/09-pelabuhan-rain.png) Chapter 2 — harbour | ![](../screenshots/10-pantai-selatan-storm.png) Chapter 3 — storm on the shore |
| ![](../screenshots/13-candi-kuno.png) Chapter 6 — ancient temple | ![](../screenshots/14-kota-garuda-hitam.png) Chapter 7 — Garuda Hitam's city |
| ![](../screenshots/15-survival-temple.png) Survival | ![](../screenshots/04-credits.png) Credits |

All screenshots are real captures from `SiJenggo --capture docs/screenshots`, which plays the game with two AI fighters.
