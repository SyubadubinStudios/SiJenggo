"""Converts the ElevenLabs MP3s in assets/source/voice to the 22 kHz mono 16-bit WAVs the game loads.

Run from the repo root:  python tools/voice/convert.py
Needs ffmpeg on PATH (any build: no filters are used). Leading/trailing silence is trimmed and every
line is peak-normalised here in Python so the voices sit evenly in the mix.
"""
import array
import pathlib
import subprocess
import wave

root = pathlib.Path(__file__).resolve().parents[2]
src = root / "assets" / "source" / "voice"
dst = root / "assets" / "voice"
dst.mkdir(parents=True, exist_ok=True)
RATE = 22050
PEAK = 0.85
SILENCE = 0.02

for mp3 in sorted(src.glob("*.mp3")):
    wav = dst / (mp3.stem + ".wav")
    if wav.exists() and wav.stat().st_mtime >= mp3.stat().st_mtime:
        continue
    raw = subprocess.run(
        ["ffmpeg", "-loglevel", "error", "-i", str(mp3), "-ar", str(RATE), "-ac", "1", "-f", "s16le", "-"],
        check=True, capture_output=True).stdout
    samples = array.array("h", raw)
    loud = [i for i, s in enumerate(samples) if abs(s) > SILENCE * 32768]
    if loud:
        start = max(0, loud[0] - RATE // 50)
        end = min(len(samples), loud[-1] + RATE // 10)
        samples = samples[start:end]
    peak = max((abs(s) for s in samples), default=0)
    if peak:
        gain = PEAK * 32767 / peak
        samples = array.array("h", (max(-32768, min(32767, int(s * gain))) for s in samples))
    with wave.open(str(wav), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(samples.tobytes())
    print(f"converted {wav.name} ({len(samples) / RATE:.1f} s)")
