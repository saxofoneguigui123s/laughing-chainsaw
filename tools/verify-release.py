#!/usr/bin/env python3
"""Compare a candidate release archive against the previously published one.

Guards the republished download against accidental content loss: the candidate
must contain exactly the same entries as the baseline, its only content changes
must be the files patched by the build fix, and the fix itself must be present.

Exit status is 0 when every check passes, 1 otherwise.
"""

import argparse
import sys
import zipfile

# Files intentionally modified by the CS1069 build fix.
EXPECTED_CHANGES = (
    "U3-SDK/Packages/manifest.json",
    "U3-SDK/Packages/packages-lock.json",
    "U3-SDK/NIGHTFALL_UPGRADE.md",
)

# The fix: Unity only compiles UnityEngine.AI when the built-in module is declared.
REQUIRED_TEXT = {
    "U3-SDK/Packages/manifest.json": '"com.unity.modules.ai"',
    "U3-SDK/Packages/packages-lock.json": '"com.unity.modules.ai"',
}


def read_entries(path):
    """Map entry name -> uncompressed size, and entry name -> bytes for text probes."""
    sizes, blobs = {}, {}
    with zipfile.ZipFile(path) as archive:
        for info in archive.infolist():
            sizes[info.filename] = info.file_size
            if info.filename in REQUIRED_TEXT:
                blobs[info.filename] = archive.read(info.filename)
    return sizes, blobs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", required=True, help="previously published archive")
    parser.add_argument("--candidate", required=True, help="newly built archive")
    args = parser.parse_args()

    base_sizes, _ = read_entries(args.baseline)
    cand_sizes, cand_blobs = read_entries(args.candidate)

    print(f"baseline : {args.baseline}  ({len(base_sizes)} entries)")
    print(f"candidate: {args.candidate}  ({len(cand_sizes)} entries)")

    problems = []

    added = sorted(set(cand_sizes) - set(base_sizes))
    removed = sorted(set(base_sizes) - set(cand_sizes))
    if added:
        problems.append(f"{len(added)} entries added: {added[:10]}")
    if removed:
        problems.append(f"{len(removed)} entries removed: {removed[:10]}")

    changed = sorted(
        name
        for name in set(base_sizes) & set(cand_sizes)
        if base_sizes[name] != cand_sizes[name]
    )
    unexpected = [name for name in changed if name not in EXPECTED_CHANGES]
    if unexpected:
        problems.append(f"{len(unexpected)} unexpected content changes: {unexpected[:10]}")

    unchanged = [name for name in EXPECTED_CHANGES if name not in changed]
    if unchanged:
        problems.append(f"expected fix missing from these files: {unchanged}")

    for name, needle in REQUIRED_TEXT.items():
        blob = cand_blobs.get(name)
        if blob is None:
            problems.append(f"{name}: entry missing from candidate")
        elif needle not in blob.decode("utf-8", errors="replace"):
            problems.append(f"{name}: {needle} not found, build fix is absent")

    print(f"\nidentical entries : {len(set(base_sizes) & set(cand_sizes)) - len(changed)}")
    print(f"changed entries   : {len(changed)}")
    for name in changed:
        print(f"  {name}  {base_sizes[name]} -> {cand_sizes[name]} bytes")

    if problems:
        print("\nVERIFICATION FAILED")
        for problem in problems:
            print(f"  - {problem}")
        return 1

    print("\nVERIFICATION PASSED: same entries as baseline, only the build fix changed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
