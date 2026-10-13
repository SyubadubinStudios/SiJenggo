# Networking

Made by Ariana Mischa Fadhila of Syubadubin Studios. · [Bahasa Indonesia](../id/JARINGAN.md)

## Model: deterministic lockstep

Both computers run the full simulation. Only inputs travel (two bytes per player per tick).
`LockstepSession` (`src/SiJenggo.Game/Net/Lockstep.cs`):

- Local input for a tick is scheduled `InputDelay` (3) ticks ahead, back to back with no gaps.
- Every packet carries the last 10 frames of local input, so packets are sent **unreliably** and a lost one
  costs nothing.
- A tick is simulated only when every player's input for it is known; otherwise the game waits
  ("Waiting for online partner…").
- Every 60 ticks each peer sends `GameSession.Checksum()`; a mismatch marks the session as desynced.

Verified: an in-memory test with 30% packet loss, and a real ThreeNet UDP loopback run (1,800 ticks, both
peers ended with identical checksums, ~59 ms round trip).

## Optional model: rollback

The host can pick rollback netcode instead (`OnlineLink.NetMode = NetMode.Rollback` before the match starts;
the choice travels in the start message and the other peer adopts it). Lockstep stays the default.
`RollbackSession` (`src/SiJenggo.Game/Net/Rollback.cs`):

- Local input is scheduled only `InputDelay` (1) tick ahead. A remote input that has not arrived yet is
  **predicted**: the partner keeps holding what they held in their last confirmed frame.
- The game keeps running on predictions for up to `MaxRollback` (8) frames past the last frame where every
  input is confirmed; only beyond that does it stall like lockstep.
- Before simulating each frame it saves a full `SessionSnapshot` into a ring (`MaxRollback + 2` slots). When a
  remote input arrives that differs from what was used, the next tick restores the snapshot of that frame and
  re-simulates up to the present, all within one rendered frame.
- Packets are unreliable and carry acknowledgements: each peer sends its input from the oldest frame the
  partner has not confirmed yet (up to 64 frames), so any amount of loss only delays confirmation.
- Simple time sync: each packet carries the sender's frame and its frame advantage; a peer that runs two or
  more frames ahead skips a tick now and then so neither side keeps rolling back deep.
- Desync detection: every 60 ticks the checksum is sent **only once that frame is confirmed** (all inputs
  known, rollbacks applied), and compared with the partner's.
- Each stage gets a new session with a new epoch byte, so stray packets from the previous stage are ignored.
- `OnlineLink.RollbackFrames` and `OnlineLink.RollbackInputDelay` tune the window and delay locally (peers may differ).

### Snapshots

`GameSession.SaveState(SessionSnapshot)` / `LoadState(SessionSnapshot)` (`src/SiJenggo.Game/GameSessionSnapshot.cs`)
copy the whole simulation: `World.CopyFrom` copies entity versions, alive flags, the free-id stack, pending
destroys and every component pool's dense and sparse arrays; the session copies its RNG, stats, camera, music,
director/wave state, challenge result and its lists. Components are plain structs without references, so a
flat array copy is a deep copy. Buffers are reused: after warm-up (done when the `RollbackSession` is created)
neither saving, loading nor a rollback tick allocates. Measured in the test run with ~20 live entities:
SaveState about 4 to 14 µs, LoadState about 4 to 15 µs, against about 30 to 50 µs for one `Step`; a worst-case
8-frame rollback stays well under a millisecond.

A test (`RollbackTests.Every_session_field_is_covered_by_the_snapshot`) fails when a new `GameSession` field is
added without being snapshotted; `GameSession.FullStateHash()` hashes every pool's raw bytes and every session
field, for tests and desync debugging.

### Presentation events during a rollback

Frames that are re-simulated were already shown to the player. While re-simulating, the session's
`SuppressEvents` flag is set and `Emit` drops events, so sparks, sounds, camera shakes and toasts are never
played twice. Trade-off: an outcome that only appears after the correction (a hit that did land after all)
shows up without its effect; its consequences (health, knockback, KO) are of course correct. Events of the
mispredicted timeline that were already queued stay queued. The renderer simply follows the corrected entity
state on the next frame.

### Rollback verification

Unit tests run two `RollbackSession`s over an in-memory link with 2 to 9 ticks of jittered latency (so packets
arrive out of order) and 20 to 35% loss: both peers make hundreds of rollbacks, never desync, and end with the
same `Checksum()` and `FullStateHash()` as a reference session fed only the confirmed inputs. Not verified yet:
a real ThreeNet UDP match in rollback mode, and the feel on real latency (the in-game UI to pick the mode is
still to come). Through the relay every packet is forwarded reliably, which adds head-of-line delay on a lossy
link; direct play does not have this.

## Wire format (first byte = type)

| Type | Bytes | Meaning |
|---|---|---|
| 1 Inputs | `[1][slot][startFrame:int32][count][count × uint16 buttons]` | redundant input history |
| 2 Checksum | `[2][slot][frame:int32][checksum:uint32]` | desync detection (reliable) |
| 3 Start | `[3][seed:uint64][mode][chapter][difficulty][n][n × character][netMode]` | match setup from player one (reliable); `netMode` 0 = lockstep, 1 = rollback, optional |
| 4 Rollback inputs | `[4][slot][epoch][n][n × ack:int32][frame:int32][advantage:int8][startFrame:int32][count][count × uint16]` | rollback: unacknowledged input history plus acks and time sync |
| 5 Rollback checksum | `[5][slot][epoch][tick:int32][checksum:uint32]` | rollback: checksum of a confirmed frame (reliable) |
| 10 Role | `[10][slot]` | relay → client: your player slot |
| 11 Peers | `[11][count]` | relay → clients: players connected |

## Playing online

- **Direct**: one player chooses *Host a room* (UDP port 27960 by default), the other *Join a host* with the
  host's address. The host picks the mode.
- **Relay** (`SiJenggo.Server`): run `dotnet run --project src/SiJenggo.Server -- 27960` on a reachable machine;
  both players *Join* it. The relay assigns slots, announces peers and forwards packets; it never simulates.

Online matches use base character stats on both sides so the two simulations are identical.
