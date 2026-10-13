"""Generates every voice line in tools/voice/lines.json with ElevenLabs v3 through the Rodin MCP server.

Run from the repo root:  python tools/voice/generate.py [path-to-rodin.exe]
Lines whose MP3 already exists in assets/source/voice are skipped, so the script can be re-run after a
failure. The server answers "Failed to initiate" intermittently, so each call is retried; a voice that
keeps failing falls back to the next voice of the same kind. Afterwards run tools/voice/convert.py.
"""
import json
import pathlib
import shutil
import subprocess
import sys
import time

root = pathlib.Path(__file__).resolve().parents[2]
manifest = json.loads((root / "tools" / "voice" / "lines.json").read_text(encoding="utf-8"))
out = root / "assets" / "source" / "voice"
out.mkdir(parents=True, exist_ok=True)
exe = sys.argv[1] if len(sys.argv) > 1 else r"C:\experiment\MCP\release\rodin\rodin.exe"
FEMALE = {"Rachel", "Aria", "Domi", "Sarah", "Jane", "Juniper", "Arabella", "Hope", "Blondie", "Priyanka", "Alexandra", "Monika"}

server = subprocess.Popen([exe], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, encoding="utf-8")
next_id = 0


def call(method, params=None, notify=False):
    global next_id
    msg = {"jsonrpc": "2.0", "method": method}
    if params is not None:
        msg["params"] = params
    if not notify:
        next_id += 1
        msg["id"] = next_id
    server.stdin.write(json.dumps(msg) + "\n")
    server.stdin.flush()
    if notify:
        return None
    while True:
        line = server.stdout.readline()
        if not line:
            raise RuntimeError("rodin server exited")
        try:
            reply = json.loads(line)
        except json.JSONDecodeError:
            continue
        if reply.get("id") == next_id:
            return reply


call("initialize", {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "sijenggo-voice", "version": "1"}})
call("notifications/initialized", notify=True)

failed = []
for line in manifest["lines"]:
    target = out / (line["key"] + ".mp3")
    if target.exists():
        continue
    kind = "female" if line["voice"] in FEMALE else "male"
    voices = [line["voice"]] * 4 + [v for v in manifest["fallbacks"][kind] if v != line["voice"]]
    for attempt, voice in enumerate(voices):
        reply = call("tools/call", {"name": "GenerateVoiceWithElevenLabsV3", "arguments": {"text": line["text"], "voiceId": voice, "LangCode": manifest["lang"]}})
        text = reply.get("result", {}).get("content", [{}])[0].get("text", "")
        if "ready for download at:" in text:
            shutil.copyfile(text.split("at:", 1)[1].strip(), target)
            print(f"{line['key']}: {voice}" + ("" if voice == line["voice"] else " (fallback)"))
            break
        time.sleep(2 + attempt)
    else:
        failed.append(line["key"])
        print(f"{line['key']}: FAILED")

server.kill()
if failed:
    print("failed:", ", ".join(failed))
    sys.exit(1)
