"""Collect exact resolved-package metadata and available upstream notices for a publish."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument("assets", type=Path)
parser.add_argument("publish", type=Path)
args = parser.parse_args()
assets = json.loads(args.assets.read_text(encoding="utf-8-sig"))
package_roots = [Path(path) for path in assets["packageFolders"]]
notices = args.publish / "third-party"
notices.mkdir(parents=True, exist_ok=True)
licenses = Path(__file__).parent / "licenses"
entries = []
for name, library in sorted(assets["libraries"].items()):
    if library["type"] != "package":
        continue
    folder = next((root / library["path"] for root in package_roots if (root / library["path"]).is_dir()), None)
    if folder is None:
        raise RuntimeError(f"Package not found: {name}")
    nuspec = next(folder.glob("*.nuspec"))
    metadata = ET.parse(nuspec).getroot().find("{*}metadata")
    def value(tag):
        node = metadata.find("{*}" + tag)
        return node.text if node is not None else None
    license_node = metadata.find("{*}license")
    expression = license_node.text if license_node is not None and license_node.get("type") == "expression" else None
    destination = notices / name.replace("/", "-")
    destination.mkdir(parents=True, exist_ok=True)
    shutil.copy2(nuspec, destination / nuspec.name)
    copied = []
    for source in folder.rglob("*"):
        if source.is_file() and ("license" in source.name.lower() or "notice" in source.name.lower()) and source.suffix.lower() not in (".dll", ".pdb"):
            relative = source.relative_to(folder)
            target = destination / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
            copied.append(relative.as_posix())
    if expression and (licenses / (expression + ".txt")).is_file():
        shutil.copy2(licenses / (expression + ".txt"), destination / "SPDX-LICENSE.txt")
    repository = metadata.find("{*}repository")
    entry = {"package": name, "license": expression or (license_node.text if license_node is not None else None),
             "authors": value("authors"), "copyright": value("copyright"),
             "repository": repository.attrib if repository is not None else None,
             "packageSha512": library.get("sha512"), "bundledNoticeFiles": copied,
             "buildOnly": name.startswith("Avalonia.BuildServices/")}
    if not entry["license"] and not entry["buildOnly"]:
        raise RuntimeError(f"License metadata missing: {name}")
    (destination / "PACKAGE-NOTICE.json").write_text(json.dumps(entry, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    entries.append(entry)
# A self-contained distribution also redistributes the .NET runtime pack.
for root in package_roots:
    for runtime in root.glob("microsoft.netcore.app.runtime.win-x64/*"):
        version = runtime.name
        if not (args.publish / "System.Private.CoreLib.dll").exists():
            continue
        # Use the exact runtime version selected by publish, recorded in its deps file.
        deps = json.loads((args.publish / "DevAtlas.App.deps.json").read_text(encoding="utf-8-sig"))
        if not any(version in name and "NETCore.App.Runtime" in name for name in deps["libraries"]):
            continue
        target = notices / ("dotnet-runtime-" + version)
        target.mkdir(exist_ok=True)
        for source in runtime.iterdir():
            if source.is_file() and ("license" in source.name.lower() or "notice" in source.name.lower()):
                shutil.copy2(source, target / source.name)
        entries.append({"package": "Microsoft.NETCore.App.Runtime.win-x64/" + version, "license": "MIT and bundled third-party notices", "buildOnly": False})
if not any(entry["package"].startswith("Microsoft.NETCore.App.Runtime.win-x64/") for entry in entries):
    raise RuntimeError("The published .NET runtime notices could not be identified")
(args.publish / "DEPENDENCIES.json").write_text(json.dumps(entries, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
manifest = {}
for source in sorted(args.publish.rglob("*")):
    if source.is_file() and source.name != "FILE-HASHES.json":
        manifest[source.relative_to(args.publish).as_posix()] = hashlib.sha256(source.read_bytes()).hexdigest()
(args.publish / "FILE-HASHES.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
print(f"Inventoried {len(entries)} resolved packages/runtime components and hashed {len(manifest)} files.")
