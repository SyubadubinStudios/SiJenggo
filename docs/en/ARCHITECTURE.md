# Architecture

Made by Ariana Mischa Fadhila of Syubadubin Studios. · [Bahasa Indonesia](../id/ARSITEKTUR.md)

## Projects and dependencies

```
SiJenggo.Core ◄── SiJenggo.Game ◄── SiJenggo.Presentation ◄── SiJenggo.App
                       ▲                                        (Avalonia 12, ThreeNet.Avalonia)
                       ├── SiJenggo.Server (ThreeNet networking only)
                       └── SiJenggo.Tests (xUnit)
```

| Project | Responsibility | Depends on rendering? |
|---|---|---|
| **Core** | `World`/`ComponentPool<T>` ECS, `Entity` handles, `DeterministicRandom` (xoshiro128**), `FixedStepClock`, `EventQueue<T>` | No |
| **Game** | Components, all game data (moves, characters, enemies, bosses, chapters, weapons, skills, gear), every system, game modes, save/profile/achievements, lockstep netcode | No |
| **Presentation** | Mirrors the ECS into a ThreeNet scene: rigged actors and Blender clips, procedural props, chapter scenery, VFX, camera, synthesised audio | ThreeNet |
| **App** | Avalonia window, menus, HUD, input mapping, the frame loop, online lobby, screenshot tour | Avalonia + ThreeNet |
| **Server** | Optional UDP relay for online play | ThreeNet networking |

Platform-specific code is confined to App (window, input) and ThreeNet's native core. Game and Core are
platform-neutral and AOT-compatible, so a mobile head only needs a new App project.

## The ECS

- `World` hands out `Entity(Id, Version)` handles. Versions bump on recycle, so a stale grab partner or
  projectile owner is detected with `World.IsAlive`. `default(Entity)` is the null handle.
- Each component type lives in a `ComponentPool<T>`: a sparse set with packed arrays, O(1) add/remove,
  linear iteration, no allocation once grown.
- Destruction is deferred to `World.Flush()` at the end of the tick, so systems can destroy while iterating.
- Components are plain structs (`Components/Components.cs`): `Transform`, `Motion`, `Body`, `Health`, `Team`,
  `Fighter`, `Player`, `Rage`, `WeaponHolder`, `Enemy`, `AiBrain`, `Boss`, `Prop`, `Pickup`, `Projectile`,
  `Hazard`, `HitMemory`, `Visual`.

## The simulation (`GameSession`)

`GameSession.Step(inputs)` advances exactly one 60 Hz tick. Systems are partial-class files in
`src/SiJenggo.Game/Systems/`, called in this fixed order:

1. snapshot positions (for render interpolation) · collect fighters (sorted by id for determinism)
2. `ReadInputs` / `UpdateCompanions` (AI partners write button presses into their own input)
3. `UpdatePlayers` — combos, cancels, specials, rage, grabs, props, input buffer (`PlayerSystem.cs`)
4. `UpdateEnemyAi`, `UpdateBoss` — attack tokens, ranged kiting, weapon fetching, boss patterns and mechanics (`AiSystem.cs`)
5. `UpdateFighters` — move frame data, lunges, invulnerability, hit-stun, knockdown, grabs (`CombatSystem.cs`)
6. `UpdateMotion` — gravity, friction, lanes, camera bounds, body separation, landing
7. `UpdateHits` — hitbox vs. hurtbox, counters, `ApplyHit` (the single place damage happens)
8. `UpdateProjectiles`, `UpdateHazards`, `UpdateProps`, `UpdatePickups`, `UpdateRage` (`EnvironmentSystem.cs`)
9. `UpdateCoop`, `UpdateDeaths` — revive, lives, respawn, KO credit, drops (`CoopSystem.cs`)
10. `UpdateDirector`, `UpdateEndOfStage`, `UpdateCamera`, `UpdateMusic`, `UpdateChallenge` (`StageSystem.cs`)
11. `World.Flush()`

Everything that the presentation should react to is pushed as a `GameEvent` into `session.Events`
(hit, KO, special, boss intro, hazard strike...). The simulation never reads events back, so dropping them is safe.

### Determinism rules

- Only `DeterministicRandom` is used; no `Random.Shared`, no wall-clock time.
- Fighters are processed in entity-id order; iteration over pools that can change mid-loop walks backwards.
- Inputs are digital (`InputButtons`, 2 bytes); analog sticks are quantised in the App before the simulation.
- `GameSession.Checksum()` hashes RNG state, health and positions; peers compare it every second.

Tests prove it: `Same_seed_and_inputs_give_identical_simulations` and `Lockstep_peers_stay_in_sync_over_a_lossy_link`.

## Presentation

- `GameView.Update(session, alpha, dt)` drains events (VFX, audio, camera shake/zoom), syncs actors, updates
  the camera, effects, lamp light pool and music mix, then `Scene.UpdateAnimations`.
- `ActorManager` creates one view per entity with a `Visual` and interpolates `PreviousPosition → Position`
  with the clock's alpha. Rigged fighters are **pooled per model** (their clips stay valid and nothing reloads).
- `ActorAnimator` does not let clips run freely: it **scrubs** each Blender clip to the exact point the
  move's frame data is at, so the fist lands on the frame the hitbox is active even with attack-speed bonuses,
  rage or hit-stop.
- `EnvironmentBuilder` builds each chapter from merged geometry (`MeshBuilder`): one draw call per material per
  stage. A typical frame is 25–55 draw calls.
- `VfxSystem` uses seven shared `ParticleEffect`s (one draw call each), pooled rings, two flash lights and pooled
  hazard telegraphs.
- `AudioDirector`/`Synth` synthesise every effect and three music stems per style at startup. The only audio files
  are the voice lines (`VoiceBank`, `assets/voice/*.wav`), one voice at a time over a ducked mix.
- **Cutscenes** (`Cutscene.cs`): `Cutscenes.ChapterIntro` builds camera shots from the chapter's own data (an
  establishing shot of area 1, tracking shots over the middle areas, a push into the boss arena, the heroes at the
  start line). `GameView.PlayCutscene` drives the camera with them; the simulation is held, not paused, so idle
  clips, weather and lamps keep moving. `Cutscenes.Epilogue` closes the last chapter.

## App

- `MainWindow` hosts `ThreeNetView` (renders offscreen and blits into Avalonia), the key-art backdrop and the
  current `Screen`. Menus hide and pause the 3D view.
- `GameController.Frame(dt)` runs `FixedStepClock` → `Step` per tick (local input, or `LockstepSession` when
  online), then `GameView.Update` with the interpolation alpha. Boss defeats trigger 1.6 s of slow motion.
- `InputMapper` merges two keyboard layouts and two gamepads (ThreeNet `Gamepads`).
- `GameController.HoldSimulation` freezes the tick count while the view keeps real time (cutscenes);
  `GameScreen` draws the letterbox and subtitles, and Story results offer "Next chapter" (or the epilogue).

## Android

`src/SiJenggo.Android` (not in `SiJenggo.slnx`, needs the android workload) renders straight into a `SurfaceView`
with `Renderer.CreateForAndroid` and runs the same `GameSession` on its render thread. `TouchControls` is a
floating stick plus seven buttons producing `InputButtons`; the APK's assets are unpacked to the app's files and
`SIJENGGO_ASSETS` points `AssetPaths` at them. It plays Story mode chapter by chapter with an AI partner. It builds
(arm64 + x86_64 natives from the ThreeNet package) but has not been run on a device yet.

## Adding a language

Indonesian and English are written inline in every `LocText(Id, En)` and in `Ui/Strings.cs`. Every other
language is table based: `assets/lang/<code>.json` is a flat JSON object mapping the **English source text** to
the translation (Bahasa Melayu ships as `ms.json`). `Strings` reads the files next to the executable once and
hands them to `Localization.Register`; the Game project stays IO-free. `LocText[language]` is a dictionary lookup
that falls back to English when an entry is missing; composed text such as `"New chapter: <name>"` is translated
half by half.

To add a language:

1. Append a value to `enum Language` (`Game/Text.cs`, never reorder: saves store the number) and give it a code
   and native name in `Localization.Code` / `Localization.NativeName`.
2. Copy `assets/lang/ms.json` to `assets/lang/<code>.json` and translate the values (keep `{0}` placeholders).
3. The title chips and the settings picker list every language whose table loaded; nothing else to wire.
4. Copy the coverage test in `tests/SiJenggo.Tests/LocalizationTests.cs` for the new code. It scans every inline
   `new("..", "..")` in the Game sources and `Strings.cs`, so new content without a translation fails `dotnet test`.

## Performance notes

- No LINQ or allocation in per-tick code; events, pools, hit memory and particle buffers are reused.
- `HitMemory` is a fixed 6-slot struct per target (one swing hits once; multi-hit moves respect rehit intervals).
- Lamps: the stage records anchors; a pool of 6 point lights moves to the anchors nearest the camera.
- Quality presets (`Presentation/GameView.cs → Quality`) toggle shadows, SSAO, MSAA, bloom and render scale.
  On an Intel UHD 620, preset 0 renders a busy fight in about 13 ms per frame.
