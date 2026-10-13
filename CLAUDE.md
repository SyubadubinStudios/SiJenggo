# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Si Jenggo – Preman Berhati Malaikat**: a 3D side-scrolling beat 'em up on .NET 10, ThreeNet (rendering) and
Avalonia 12 (UI). `solution-design.md` (Bahasa Indonesia) is the source of truth for gameplay, story,
characters, chapters, bosses, VFX, audio and modes; read the relevant section before adding mechanics.
`SiJenggo.txt` is the original brief.

## Commands

```bash
dotnet build SiJenggo.slnx                         # whole solution (expect 0 warnings)
dotnet test                                        # 51 xUnit tests (no GPU needed)
dotnet test --filter "FullyQualifiedName~CombatTests.Grab_then_throw"   # a single test
dotnet run --project src/SiJenggo.App              # play (add --quality 0..3 to force graphics)
dotnet run --project src/SiJenggo.App -- --capture docs/screenshots    # automatic screenshot tour (2 AI fighters)
dotnet run --project src/SiJenggo.Server -- 27960  # optional online relay
dotnet run -c Release --project tools/SiJenggo.Balance          # headless balance sweep -> docs/en/balance-report.md
dotnet build src/SiJenggo.Android -c Release                    # Android APK (not in the slnx; android workload)
blender --background --factory-startup --python tools/blender/character_styles.py   # re-author signature clips
python tools/voice/generate.py && python tools/voice/convert.py                     # voice lines (Rodin MCP + ffmpeg)
```

`global.json` pins SDK 10.0.401 (rollForward latestFeature). ThreeNet needs a DX12/Metal/Vulkan GPU; the
`ThreeNet.Native` 0.9 package ships win-x64, linux-x64, osx-arm64/x64, android-arm64/x64 and ios-arm64.

## Architecture (read several files to see this)

- **Core** (`src/SiJenggo.Core`): sparse-set ECS (`World`, `ComponentPool<T>`), versioned `Entity` handles
  where `default(Entity)` is null, deferred destroy (`World.Flush`), `DeterministicRandom`, `FixedStepClock`.
- **Game** (`src/SiJenggo.Game`): the entire simulation, no rendering. `GameSession` is one stage; its systems
  are `partial class GameSession` files in `Systems/` and run in a fixed order inside `Step()` (see
  `GameSession.cs`). All damage goes through `ApplyHit(target, ref HitSpec)` in `CombatSystem.cs`.
  Content is static data in `Data/` (moves are registered in declaration order in `Moves.cs`; ids must stay
  stable for lockstep). `Campaign` (Modes/) sequences stages per mode and writes rewards into `Profile`.
- **Determinism is a hard rule**: only `DeterministicRandom`, iterate fighters in id order, no wall clock,
  inputs are `InputButtons` bits. Online lockstep (`Net/Lockstep.cs`), rollback (`Net/Rollback.cs`) and the
  checksum tests depend on it. Rollback snapshots every mutable `GameSession` field (`GameSessionSnapshot.cs`):
  a new field must be added there, and a test fails if it is forgotten. Events are dropped while re-simulating.
  AI partners produce button presses (`CoopSystem.CompanionButtons`) rather than acting directly.
- **Presentation** (`src/SiJenggo.Presentation`): `GameView.Update(session, alpha, dt)` drains
  `session.Events` (VFX/audio/camera), then `ActorManager.Sync` mirrors entities with a `Visual` component.
  Rigged models are pooled per key (destroying clips invalidates other clips' handles in ThreeNet 0.9).
  `ActorAnimator` scrubs Blender clips to move frame data instead of playing them freely. Sim space → render
  space flips Z (`Space.ToRender`); the camera sits on +Z. Signature clips (`gadget`, `taunt`, `intro`) fall back to
  shared ones. `Cutscenes` builds camera shots from chapter data; `GameView.PlayCutscene` plays them while
  `GameController.HoldSimulation` freezes ticks. Voice lines: `VoiceBank` + `AudioDirector.HandleVoice`.
- **App** (`src/SiJenggo.App`): code-built Avalonia UI (`Screens/`, `Ui/UiKit.cs`, `Styles/Theme.axaml`),
  `GameController` (fixed-step loop, pause, slow-mo, lockstep), `InputMapper` (two keyboard layouts + ThreeNet
  gamepads), `OnlineLink` (ThreeNet UDP). All UI text goes through `Ui/Strings.cs`; game content uses `LocText`. ID/EN are inline;
  other languages (Malay) are `assets/lang/<code>.json` keyed by the English text: every new string needs an
  entry there or `LocalizationTests` fails.
- **Assets**: `assets/source/rodin` (raw Rodin output, large) → `tools/blender/sijenggo_pipeline.py` run inside
  Blender via Blender MCP (`build_character`, `build_prop`) → `assets/models/*.glb` (copied next to the exe by
  the App csproj). Missing models fall back to `ProceduralModels`. Bone names and clip names in the script are
  what the C# side looks up. `tools/blender/character_styles.py` runs headless (`blender --background`) and
  re-exports styled rigs from `assets/models`; review poses with `tools/blender/preview_clips.py`.
  `assets/source` (~370 MB raw Rodin output) is kept out of Git; the game only needs `assets/models`.

## Conventions

- Hot paths (per tick / per frame) must not allocate: no LINQ, reuse buffers, struct components.
- New moves: add to `Moves.cs` (append, do not reorder) and give them a `MovePose` the animator maps to a clip.
- Docs are bilingual: English in `docs/en`, Indonesian in `docs/id`, README has both. Keep `PLAN.md` (roadmap)
  and `Progress.md` (status, honest about what is unverified) up to date.
- The credit "Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios" must stay in the app (title, credits)
  and in the docs.
- UI follows the project-local `frontend-design` skill: palette aspal/kunyit/cabai/toska/kapur/soga, Bungee for
  display, Barlow Condensed for body, and the misregistered "sablon" print (`SablonText`) as the signature.
- MCP servers are configured in `.mcp.json`: `rodin` (3D/2D/voice generation) and `blender` (live Blender).
