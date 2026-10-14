#!/usr/bin/env bash
# Zips the builds for release (M7) into Builds/Release/, named with the version from BuildScript.Version:
#   HumanError-v0.7.0-Windows.zip, HumanError-Demo-v0.7.0-Windows.zip   (with the font licences in Licenses/)
#   HumanError-v0.7.0-Android.apk,  HumanError-Demo-v0.7.0-Android.apk
#   SHA256SUMS.txt
# Builds that don't exist yet are skipped. Unity's *_DoNotShip debug folders are left out.
#
#   Tools/package.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
version="$(grep -oE 'Version = "[0-9.]+"' "$ROOT/Assets/_Game/Scripts/Editor/BuildScript.cs" | grep -oE '[0-9.]+')"
python - "$ROOT" "$version" <<'EOF'
import sys, os, zipfile, shutil, hashlib
root, version = sys.argv[1], sys.argv[2]
builds = os.path.join(root, "Builds")
out = os.path.join(builds, "Release")
os.makedirs(out, exist_ok=True)
licences = os.path.join(root, "Assets", "_Game", "Resources", "Licenses")
made = []

def zip_windows(folder, name):
    src = os.path.join(builds, folder)
    if not os.path.isdir(src):
        print(f"  skip {folder} (not built)"); return
    path = os.path.join(out, f"{name}-v{version}-Windows.zip")
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for dirpath, dirs, files in os.walk(src):
            dirs[:] = [d for d in dirs if "DoNotShip" not in d]
            for f in files:
                full = os.path.join(dirpath, f)
                z.write(full, os.path.join(name, os.path.relpath(full, src)))
        for f in sorted(os.listdir(licences)):
            if f.endswith(".txt"):
                z.write(os.path.join(licences, f), os.path.join(name, "Licenses", f))
    made.append(path)

def copy_apk(rel, name):
    src = os.path.join(builds, rel)
    if not os.path.isfile(src):
        print(f"  skip {rel} (not built)"); return
    path = os.path.join(out, f"{name}-v{version}-Android.apk")
    shutil.copyfile(src, path)
    made.append(path)

zip_windows("Windows", "HumanError")
zip_windows("WindowsDemo", "HumanError-Demo")
copy_apk("Android/HumanError.apk", "HumanError")
copy_apk("AndroidDemo/HumanErrorDemo.apk", "HumanError-Demo")

with open(os.path.join(out, "SHA256SUMS.txt"), "w") as sums:
    for p in made:
        h = hashlib.sha256(open(p, "rb").read()).hexdigest()
        sums.write(f"{h}  {os.path.basename(p)}\n")
        print(f"  {os.path.basename(p)}  {os.path.getsize(p) // (1024 * 1024)} MB")
print(f"Release v{version} -> {out}")
EOF
