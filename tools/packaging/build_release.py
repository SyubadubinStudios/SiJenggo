"""Builds the release installers for every platform Si Jenggo ships on.

    python tools/packaging/build_release.py [--version 0.1.0] [--only win,linux,mac,android,server]

Output goes to artifacts/release (git-ignored):
  SiJenggo-<v>-win-x64-setup.exe     Inno Setup installer (per-user by default, no admin needed)
  SiJenggo-<v>-win-x64-portable.zip  the same files, unzip and run SiJenggo.exe
  sijenggo_<v>_amd64.deb             Debian/Ubuntu package (/opt/sijenggo, menu entry, `sijenggo` command)
  SiJenggo-<v>-linux-x64.tar.gz      any distro: unpack and run ./install.sh (per user) or ./SiJenggo
  SiJenggo-<v>-macos-arm64.zip       "Si Jenggo.app" for Apple silicon (ad-hoc signed, not notarised)
  SiJenggo-<v>-macos-x64.zip         "Si Jenggo.app" for Intel Macs (ad-hoc signed, not notarised)
  SiJenggo-<v>-android.apk           Android 8+ APK (arm64 + x86_64), debug-signed
  SiJenggo-Server-<v>-<rid>.zip      the optional online relay, single file

Tools it needs: the .NET 10 SDK (+ the android workload for the APK), Inno Setup's ISCC.exe (set ISCC or
install to the default per-user folder) and rcodesign (set RCODESIGN) to ad-hoc sign the macOS bundles,
which Apple silicon requires before it will run native code. Everything else is plain Python.
Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.
"""
import argparse
import gzip
import hashlib
import io
import os
import pathlib
import plistlib
import shutil
import struct
import subprocess
import tarfile
import time
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
ART = ROOT / "artifacts"
PUBLISH = ART / "publish"
OUT = ART / "release"
APP = ROOT / "src" / "SiJenggo.App" / "SiJenggo.App.csproj"
SERVER = ROOT / "src" / "SiJenggo.Server" / "SiJenggo.Server.csproj"
ANDROID = ROOT / "src" / "SiJenggo.Android" / "SiJenggo.Android.csproj"
ICO = ROOT / "src" / "SiJenggo.App" / "Assets" / "sijenggo.ico"
CREDIT = "Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios"
DESCRIPTION = "3D side-scrolling beat 'em up set across Indonesia"


def run(*cmd, cwd=ROOT):
    print("  $", " ".join(str(c) for c in cmd))
    subprocess.run([str(c) for c in cmd], cwd=cwd, check=True)


def publish(project, rid, out, single_file=False):
    if out.exists():
        shutil.rmtree(out)
    args = ["dotnet", "publish", project, "-c", "Release", "-r", rid, "--self-contained", "true", "-o", out,
            "-p:DebugType=none", "-p:DebugSymbols=false"]
    if single_file:
        args += ["-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true"]
    run(*args)
    return out


def is_executable(path: pathlib.Path) -> bool:
    return path.suffix in ("", ".so", ".dylib", ".sh") and path.is_file()


def icon_png(size: int) -> bytes:
    from PIL import Image  # Pillow, only needed for the Linux/macOS icons

    im = Image.open(ICO)
    im.size = max(im.ico.sizes())
    im = im.convert("RGBA").resize((size, size), Image.LANCZOS)
    buf = io.BytesIO()
    im.save(buf, "PNG")
    return buf.getvalue()


def zip_dir(src: pathlib.Path, dest: pathlib.Path, prefix: str = ""):
    """Zips a folder keeping Unix exec bits (macOS/Linux need them; Windows ignores them)."""
    with zipfile.ZipFile(dest, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for path in sorted(src.rglob("*")):
            rel = (pathlib.PurePosixPath(prefix) / path.relative_to(src).as_posix()).as_posix()
            if path.is_dir():
                info = zipfile.ZipInfo(rel + "/")
                info.external_attr = (0o40755 << 16) | 0x10
                z.writestr(info, b"")
                continue
            info = zipfile.ZipInfo.from_file(path, rel)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = ((0o100755 if is_executable(path) else 0o100644) << 16)
            with open(path, "rb") as f:
                z.writestr(info, f.read())


def tar_add_dir(tar: tarfile.TarFile, src: pathlib.Path, arc_root: str):
    for path in sorted(src.rglob("*")):
        info = tar.gettarinfo(str(path), arcname=f"{arc_root}/{path.relative_to(src).as_posix()}")
        info.uid = info.gid = 0
        info.uname = info.gname = "root"
        info.mode = 0o755 if path.is_dir() or is_executable(path) else 0o644
        if path.is_file():
            with open(path, "rb") as f:
                tar.addfile(info, f)
        else:
            tar.addfile(info)


def tar_add_dirs(tar: tarfile.TarFile, *names: str):
    for name in names:
        info = tarfile.TarInfo(name)
        info.type = tarfile.DIRTYPE
        info.mode = 0o755
        info.mtime = int(time.time())
        info.uname = info.gname = "root"
        tar.addfile(info)


def tar_add_bytes(tar: tarfile.TarFile, name: str, data: bytes, mode=0o644):
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mode = mode
    info.mtime = int(time.time())
    info.uname = info.gname = "root"
    tar.addfile(info, io.BytesIO(data))


def desktop_entry(exec_path: str, icon: str) -> bytes:
    return f"""[Desktop Entry]
Type=Application
Name=Si Jenggo
GenericName=Beat 'em up
Comment={DESCRIPTION}. {CREDIT}
Exec={exec_path}
Icon={icon}
Terminal=false
Categories=Game;ActionGame;
""".encode()


# ───────────────────────────── Windows ─────────────────────────────

def build_windows(version):
    print("== Windows")
    src = publish(APP, "win-x64", PUBLISH / "win-x64")
    zip_dir(src, OUT / f"SiJenggo-{version}-win-x64-portable.zip", f"SiJenggo-{version}")
    iscc = os.environ.get("ISCC") or str(pathlib.Path(os.environ["LOCALAPPDATA"]) / "Programs" / "InnoSetup7" / "ISCC.exe")
    run(iscc, f"/DAppVersion={version}", f"/DSourceDir={src}", f"/DOutputDir={OUT}", "/Q",
        ROOT / "tools" / "packaging" / "windows" / "SiJenggo.iss")


# ───────────────────────────── Linux ─────────────────────────────

def ar_member(name: str, data: bytes) -> bytes:
    header = f"{name:<16}{int(time.time()):<12}{0:<6}{0:<6}{0o100644:<8o}{len(data):<10}`\n".encode()
    return header + data + (b"\n" if len(data) % 2 else b"")


def build_linux(version):
    print("== Linux")
    src = publish(APP, "linux-x64", PUBLISH / "linux-x64")
    png = icon_png(256)

    # .deb: files under /opt/sijenggo, a launcher on PATH, a menu entry and an icon.
    data = io.BytesIO()
    with tarfile.open(fileobj=data, mode="w:gz") as tar:
        tar_add_dirs(tar, "./opt", "./opt/sijenggo", "./usr", "./usr/bin", "./usr/share", "./usr/share/applications",
                     "./usr/share/icons", "./usr/share/icons/hicolor", "./usr/share/icons/hicolor/256x256",
                     "./usr/share/icons/hicolor/256x256/apps")
        tar_add_dir(tar, src, "./opt/sijenggo")
        tar_add_bytes(tar, "./usr/bin/sijenggo", b'#!/bin/sh\nexec /opt/sijenggo/SiJenggo "$@"\n', 0o755)
        tar_add_bytes(tar, "./usr/share/applications/sijenggo.desktop", desktop_entry("sijenggo", "sijenggo"))
        tar_add_bytes(tar, "./usr/share/icons/hicolor/256x256/apps/sijenggo.png", png)
    installed_kb = sum(p.stat().st_size for p in src.rglob("*") if p.is_file()) // 1024
    control_text = f"""Package: sijenggo
Version: {version}
Architecture: amd64
Maintainer: Syubadubin Studios <noreply@syubadubin.studio>
Installed-Size: {installed_kb}
Depends: libc6, libgcc-s1, libstdc++6, libvulkan1, libx11-6, libice6, libsm6, libfontconfig1, libasound2 | libasound2t64
Section: games
Priority: optional
Homepage: https://github.com/SyubadubinStudios/SiJenggo
Description: Si Jenggo - Preman Berhati Malaikat
 {DESCRIPTION}, built on .NET 10, ThreeNet and Avalonia.
 {CREDIT}.
""".encode()
    control = io.BytesIO()
    with tarfile.open(fileobj=control, mode="w:gz") as tar:
        tar_add_bytes(tar, "./control", control_text)
    deb = b"!<arch>\n" + ar_member("debian-binary", b"2.0\n") + ar_member("control.tar.gz", control.getvalue()) + ar_member("data.tar.gz", data.getvalue())
    (OUT / f"sijenggo_{version}_amd64.deb").write_bytes(deb)

    # tar.gz for any distribution, with a per-user installer script.
    root = f"SiJenggo-{version}-linux-x64"
    install_sh = f"""#!/bin/sh
# Installs Si Jenggo for the current user (no root needed). {CREDIT}.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
DEST="${{XDG_DATA_HOME:-$HOME/.local/share}}/sijenggo"
mkdir -p "$DEST" "$HOME/.local/bin" "${{XDG_DATA_HOME:-$HOME/.local/share}}/applications" "${{XDG_DATA_HOME:-$HOME/.local/share}}/icons/hicolor/256x256/apps"
cp -R "$HERE/." "$DEST/"
ln -sf "$DEST/SiJenggo" "$HOME/.local/bin/sijenggo"
sed "s|@EXEC@|$DEST/SiJenggo|" "$HERE/sijenggo.desktop" > "${{XDG_DATA_HOME:-$HOME/.local/share}}/applications/sijenggo.desktop"
cp "$HERE/sijenggo.png" "${{XDG_DATA_HOME:-$HOME/.local/share}}/icons/hicolor/256x256/apps/sijenggo.png"
echo "Si Jenggo installed to $DEST (run: sijenggo)"
""".encode()
    with tarfile.open(OUT / f"{root}.tar.gz", "w:gz") as tar:
        tar_add_dirs(tar, root)
        tar_add_dir(tar, src, root)
        tar_add_bytes(tar, f"{root}/install.sh", install_sh, 0o755)
        tar_add_bytes(tar, f"{root}/sijenggo.desktop", desktop_entry("@EXEC@", "sijenggo"))
        tar_add_bytes(tar, f"{root}/sijenggo.png", png)


# ───────────────────────────── macOS ─────────────────────────────

def icns() -> bytes:
    """An .icns built from PNG chunks (ic07 = 128 px, ic08 = 256 px, ic09 = 512 px)."""
    chunks = b"".join(t + struct.pack(">I", len(p) + 8) + p for t, p in ((b"ic07", icon_png(128)), (b"ic08", icon_png(256)), (b"ic09", icon_png(512))))
    return b"icns" + struct.pack(">I", len(chunks) + 8) + chunks


def build_mac(version, rid, label):
    print(f"== macOS {label}")
    src = publish(APP, rid, PUBLISH / rid)
    bundle_root = PUBLISH / f"{rid}-bundle"
    if bundle_root.exists():
        shutil.rmtree(bundle_root)
    app = bundle_root / "Si Jenggo.app"
    shutil.copytree(src, app / "Contents" / "MacOS")
    (app / "Contents" / "Resources").mkdir(parents=True)
    (app / "Contents" / "Resources" / "sijenggo.icns").write_bytes(icns())
    with open(app / "Contents" / "Info.plist", "wb") as f:
        plistlib.dump({
            "CFBundleName": "Si Jenggo",
            "CFBundleDisplayName": "Si Jenggo",
            "CFBundleIdentifier": "com.syubadubin.sijenggo",
            "CFBundleVersion": version,
            "CFBundleShortVersionString": version,
            "CFBundleExecutable": "SiJenggo",
            "CFBundleIconFile": "sijenggo.icns",
            "CFBundlePackageType": "APPL",
            "CFBundleInfoDictionaryVersion": "6.0",
            "LSApplicationCategoryType": "public.app-category.action-games",
            "LSMinimumSystemVersion": "12.0",
            "NSHighResolutionCapable": True,
            "NSHumanReadableCopyright": CREDIT,
        }, f)
    rcodesign = os.environ.get("RCODESIGN")
    if rcodesign:
        # Ad-hoc signature: Apple silicon refuses to run unsigned native code. Not notarised, so the first
        # launch still needs right-click > Open (or `xattr -dr com.apple.quarantine "Si Jenggo.app"`).
        run(rcodesign, "sign", app)
    else:
        print("  ! RCODESIGN not set: the bundle is unsigned and will not start on Apple silicon")
    zip_dir(bundle_root, OUT / f"SiJenggo-{version}-macos-{label}.zip")


# ───────────────────────────── Android / server ─────────────────────────────

def build_android(version):
    print("== Android")
    run("dotnet", "build", ANDROID, "-c", "Release")
    apk = next((ROOT / "src" / "SiJenggo.Android" / "bin" / "Release").rglob("*-Signed.apk"))
    shutil.copyfile(apk, OUT / f"SiJenggo-{version}-android.apk")


def build_server(version):
    print("== Relay server")
    for rid in ("win-x64", "linux-x64", "osx-arm64"):
        out = publish(SERVER, rid, PUBLISH / f"server-{rid}", single_file=True)
        zip_dir(out, OUT / f"SiJenggo-Server-{version}-{rid}.zip", f"SiJenggo-Server-{version}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", default="0.1.0")
    parser.add_argument("--only", default="win,linux,mac,android,server")
    args = parser.parse_args()
    parts = set(args.only.split(","))
    OUT.mkdir(parents=True, exist_ok=True)
    if "win" in parts:
        build_windows(args.version)
    if "linux" in parts:
        build_linux(args.version)
    if "mac" in parts:
        build_mac(args.version, "osx-arm64", "arm64")
        build_mac(args.version, "osx-x64", "x64")
    if "android" in parts:
        build_android(args.version)
    if "server" in parts:
        build_server(args.version)

    lines = []
    for f in sorted(OUT.iterdir()):
        if f.name == "SHA256SUMS.txt":
            continue
        lines.append(f"{hashlib.sha256(f.read_bytes()).hexdigest()}  {f.name}")
        print(f"{f.stat().st_size / 1e6:8.1f} MB  {f.name}")
    (OUT / "SHA256SUMS.txt").write_text("\n".join(lines) + "\n")


if __name__ == "__main__":
    main()
