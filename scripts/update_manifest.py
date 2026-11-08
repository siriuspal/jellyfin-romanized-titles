"""Adds a release to manifest.json, the plugin repository file Jellyfin reads."""
import datetime
import hashlib
import json
import pathlib
import sys

TARGET_ABI = "12.4.0.0"

version, zip_path, url, changelog = sys.argv[1:5]
manifest_path = pathlib.Path("manifest.json")
manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else [{
    "guid": "a506157a-9053-4134-9f42-745f120a1859",
    "name": "Romanized Titles",
    "description": "Shows Indian films and series under their original title in Roman script "
                   "(Teen Kanya, not Three Daughters), using IMDb's original titles.",
    "overview": "Original titles in Roman script for Indian films and series",
    "owner": "siriuspal",
    "category": "Metadata",
    "versions": [],
}]
plugin = manifest[0]
plugin["versions"] = [v for v in plugin["versions"] if v["version"] != version]
plugin["versions"].insert(0, {
    "version": version,
    "changelog": changelog.strip() or f"Version {version}",
    "targetAbi": TARGET_ABI,
    "sourceUrl": url,
    "checksum": hashlib.md5(pathlib.Path(zip_path).read_bytes()).hexdigest(),
    "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
})
manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")
