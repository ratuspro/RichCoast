#!/usr/bin/env python3
"""Unpack TextMeshPro's "Essential Resources" into Assets/TextMesh Pro/.

TMP ships its default font, settings and shaders as a .unitypackage inside the uGUI package;
the editor normally imports it through an async, interactive importer that never completes
in a -quit batch run. A .unitypackage is just a tar.gz of <guid>/{asset,asset.meta,pathname}
entries, so unpack it directly — deterministic and editor-free. Idempotent.
"""
import glob
import io
import os
import sys
import tarfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
TARGET_MARKER = os.path.join(ROOT, "Assets", "TextMesh Pro", "Resources", "TMP Settings.asset")


def find_package():
    pattern = os.path.join(ROOT, "Library", "PackageCache", "com.unity.ugui*", "Package Resources", "TMP Essential Resources.unitypackage")
    hits = glob.glob(pattern)
    if hits:
        return hits[0]
    # Fall back to the editor's built-in copy (before the first package resolve).
    version = ""
    with open(os.path.join(ROOT, "ProjectSettings", "ProjectVersion.txt"), encoding="utf-8") as f:
        for line in f:
            if line.startswith("m_EditorVersion:"):
                version = line.split(":", 1)[1].strip()
    for base in (r"C:\Program Files\Unity\Hub\Editor", "/Applications/Unity/Hub/Editor", os.path.expanduser("~/Unity/Hub/Editor")):
        hit = glob.glob(os.path.join(base, version, "**", "com.unity.ugui", "Package Resources", "TMP Essential Resources.unitypackage"), recursive=True)
        if hit:
            return hit[0]
    return None


def main():
    if os.path.exists(TARGET_MARKER):
        print("TMP essentials already present")
        return 0
    pkg = find_package()
    if not pkg:
        print("TMP Essential Resources.unitypackage not found (resolve packages first)", file=sys.stderr)
        return 1
    entries = {}
    with tarfile.open(pkg, "r:gz") as tar:
        for member in tar.getmembers():
            if not member.isfile():
                continue
            guid, _, kind = member.name.replace("\\", "/").lstrip("./").partition("/")
            entries.setdefault(guid, {})[kind] = tar.extractfile(member).read()
    written = 0
    for guid, parts in entries.items():
        if "pathname" not in parts:
            continue
        rel = parts["pathname"].decode("utf-8").splitlines()[0].strip()
        dest = os.path.join(ROOT, rel)
        if "asset" in parts:
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with open(dest, "wb") as f:
                f.write(parts["asset"])
            written += 1
        else:
            os.makedirs(dest, exist_ok=True)
        if "asset.meta" in parts:
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with open(dest + ".meta", "wb") as f:
                f.write(parts["asset.meta"])
    print(f"unpacked {written} TMP essential files from {pkg}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
