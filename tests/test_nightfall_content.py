#!/usr/bin/env python3
"""Lightweight regression checks for the generated Nightfall content pack."""
from __future__ import annotations

import json
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import build_nightfall_content as build  # noqa: E402


def fail(message: str) -> None:
    raise AssertionError(message)


def main() -> int:
    catalog = build.build_catalog()
    build.validate(catalog)
    content = ROOT / "NightfallSurvivalExpansion"
    committed_catalog = json.loads((content / "catalog.json").read_text(encoding="utf-8"))
    if committed_catalog != catalog:
        fail("catalog.json is stale; run python3 tools/build_nightfall_content.py")

    entries = catalog["entries"] + catalog["magazines"]
    for entry in entries:
        category = {"weapon": "Items/Weapons", "armor": "Items/Armor", "vehicle": "Vehicles", "magazine": "Items/Magazines"}[entry["kind"]]
        directory = content / category / f"{entry['legacy_id']}_{entry['slug']}"
        dat = directory / f"{entry['slug']}.dat"
        local = directory / "English.dat"
        requirements = directory / "AssetRequirements.md"
        for path in (dat, local, requirements):
            if not path.is_file():
                fail(f"Missing generated asset file: {path.relative_to(ROOT)}")
        text = dat.read_text(encoding="utf-8")
        if f"GUID {entry['guid']}" not in text:
            fail(f"Wrong GUID in {dat}")
        if f"ID {entry['legacy_id']}" not in text:
            fail(f"Wrong legacy ID in {dat}")
        if entry["name"] not in local.read_text(encoding="utf-8"):
            fail(f"Missing localized name in {local}")

    archive = ROOT / "release" / f"Nightfall-Survival-Expansion-content-v{build.VERSION}.zip"
    if not archive.is_file():
        fail(f"Missing release archive: {archive}")
    with zipfile.ZipFile(archive) as zf:
        names = set(zf.namelist())
        if "NightfallSurvivalExpansion/catalog.json" not in names:
            fail("Release archive missing catalog.json")
        if len(names) < 480:
            fail("Release archive unexpectedly incomplete")

    stats = catalog["statistics"]
    print(
        "PASS: "
        f"{stats['modern_weapons']} modern weapons, {stats['ancient_weapons']} ancient weapons, "
        f"{stats['armor']} armor items, {stats['vehicles']} vehicles, {stats['magazines']} magazines."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
