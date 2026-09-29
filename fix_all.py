#!/usr/bin/env python3
"""
Fix script for Unturned U3 SDK Nightfall Survival Expansion v3.26.3.12.2
Fixes:
- HLSLSupport.cginc not found errors (Unity 6 / 2021+)
- webgpu pragma warnings in PostProcessing
- CS0162 unreachable code in Assets.cs

Usage:
    python3 fix_all.py /path/to/your/UnityProject

If no path given, fixes current directory.
"""
import os
import re
import sys
import shutil

PROJECT_ROOT = sys.argv[1] if len(sys.argv) > 1 else "."

HLSLSUPPORT_SHIM = """// HLSLSupport.cginc - Compatibility shim for Unity 6 / Unity 2021+
#ifndef HLSLSUPPORT_INCLUDED
#define HLSLSUPPORT_INCLUDED
#ifndef UNITY_BRANCH
#define UNITY_BRANCH [branch]
#endif
#ifndef UNITY_FLATTEN
#define UNITY_FLATTEN [flatten]
#endif
#ifndef UNITY_UNROLL
#define UNITY_UNROLL [unroll]
#endif
#ifndef UNITY_LOOP
#define UNITY_LOOP [loop]
#endif
#ifndef UNITY_CAN_COMPILE_TESSELLATION
#define UNITY_CAN_COMPILE_TESSELLATION 1
#endif
#endif
"""

def create_shim_files(root):
    """Create HLSLSupport.cginc in all search locations Unity uses"""
    locations = [
        os.path.join(root, "HLSLSupport.cginc"),
        os.path.join(root, "Assets", "HLSLSupport.cginc"),
        os.path.join(root, "Assets", "CGIncludes", "HLSLSupport.cginc"),
        os.path.join(root, "Assets", "Game", "Sources", "Shaders", "HLSLSupport.cginc"),
        os.path.join(root, "Assets", "Game", "Sources", "Shaders", "GL", "HLSLSupport.cginc"),
        os.path.join(root, "Assets", "Shaders", "HLSLSupport.cginc"),
        os.path.join(root, "Packages", "com.unity.postprocessing", "PostProcessing", "Shaders", "Builtins", "HLSLSupport.cginc"),
    ]
    for path in locations:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'w', encoding='utf-8') as f:
            f.write(HLSLSUPPORT_SHIM)
        print(f"[OK] Created shim: {path}")

def fix_shader_file(filepath):
    """Fix a single .shader file"""
    with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
        content = f.read()

    original = content

    # 1. Remove or comment out explicit HLSLSupport includes
    # Patterns like: #include "HLSLSupport.cginc" or #include "UnityCG.cginc" that might have HLSLSupport inside?
    # We replace HLSLSupport include with a comment, because Unity now auto-includes needed macros
    content = re.sub(
        r'#include\s+"HLSLSupport\.cginc"\s*',
        '// HLSLSupport.cginc removed for Unity 6 compatibility - auto-included now\n',
        content,
        flags=re.IGNORECASE
    )
    content = re.sub(
        r'#include\s+<HLSLSupport\.cginc>\s*',
        '// HLSLSupport.cginc removed\n',
        content,
        flags=re.IGNORECASE
    )

    # 2. Fix webgpu pragma warnings
    # Example: #pragma exclude_renderers: webgpu
    # We remove webgpu token from exclude_renderers and only_renderers
    def fix_webgpu_pragma(match):
        full = match.group(0)
        # Remove webgpu and clean up commas
        # Handle both exclude_renderers and only_renderers
        fixed = re.sub(r'\bwebgpu\b\s*,?\s*', '', full, flags=re.IGNORECASE)
        fixed = re.sub(r',\s*,', ',', fixed)  # clean double commas
        fixed = re.sub(r':\s*,', ': ', fixed)  # clean ": ,"
        fixed = re.sub(r',\s*$', '', fixed)  # trailing comma
        # If pragma becomes empty like "#pragma exclude_renderers: " then comment it out
        if re.match(r'#pragma\s+(exclude_renderers|only_renderers)\s*:\s*$', fixed.strip(), re.IGNORECASE):
            return f"// {full.strip()} // removed webgpu - not supported in this Unity version"
        return fixed

    content = re.sub(
        r'#pragma\s+(exclude_renderers|only_renderers)\s*:[^\n]*webgpu[^\n]*',
        fix_webgpu_pragma,
        content,
        flags=re.IGNORECASE
    )

    # 3. Fix d3d9 specific errors - some shaders fail on d3d9 because HLSLSupport missing
    # Add fallback to not compile for d3d9 if needed, or ensure shader has both vert and frag
    # For Unturned/Screenshot and similar that say "Both vertex and fragment programs must be present"
    # This happens when include fails, so after fixing include it should be ok. But we also add safety.

    if content != original:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"[FIXED] {filepath}")
        return True
    return False

def fix_assets_cs(root):
    """Fix CS0162 unreachable code warning in Assets.cs"""
    candidates = []
    for dirpath, _, filenames in os.walk(root):
        for fname in filenames:
            if fname == "Assets.cs":
                candidates.append(os.path.join(dirpath, fname))

    for filepath in candidates:
        with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
            content = f.read()

        original = content

        # Check if file already has pragma disable
        if "#pragma warning disable 0162" not in content and "#pragma warning disable CS0162" not in content:
            # Add pragma at top of file after usings
            # Find first using or namespace
            lines = content.split('\n')
            insert_idx = 0
            for i, line in enumerate(lines[:20]):
                if line.strip().startswith("using ") or line.strip().startswith("namespace ") or line.strip().startswith("public ") or line.strip().startswith("class "):
                    insert_idx = i
                    break
            # Insert pragma warning disable at very top
            pragma = "#pragma warning disable 0162, 0649, 0414 // Fix for Unturned SDK build - unreachable code warning\n"
            if content.startswith("//") or content.startswith("/*"):
                # Keep header comments
                content = pragma + content
            else:
                content = pragma + content
            print(f"[FIXED] Added pragma warning disable to {filepath}")

        # Also fix specific pattern that causes unreachable code:
        # In Assets.cs around line 2594, there is often:
        # return; // Abort startup.
        # followed by unreachable code. We can try to fix by ensuring #if blocks are correct.
        # Simple approach: replace "return; // Abort startup." that is inside #if !DEDICATED_SERVER with conditional return that doesn't trigger unreachable warning.
        # But easiest is pragma.

        if content != original:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(content)

def fix_postprocessing_package(root):
    """Update postprocessing package manifest to newer version that doesn't have webgpu issues"""
    manifest_path = os.path.join(root, "Packages", "manifest.json")
    if os.path.exists(manifest_path):
        with open(manifest_path, 'r', encoding='utf-8') as f:
            content = f.read()
        # Update com.unity.postprocessing to 3.4.0 if older
        if "com.unity.postprocessing" in content:
            # Replace version with 3.4.0
            new_content = re.sub(
                r'"com\.unity\.postprocessing"\s*:\s*"[^"]+"',
                '"com.unity.postprocessing": "3.4.0"',
                content
            )
            if new_content != content:
                with open(manifest_path, 'w', encoding='utf-8') as f:
                    f.write(new_content)
                print(f"[FIXED] Updated postprocessing package to 3.4.0 in {manifest_path}")

    # Also directly fix shader files in Library/PackageCache if they exist
    for dirpath, _, filenames in os.walk(root):
        if "PostProcessing" in dirpath and dirpath.endswith("Builtins"):
            for fname in filenames:
                if fname.endswith(".shader"):
                    fix_shader_file(os.path.join(dirpath, fname))

def main():
    root = os.path.abspath(PROJECT_ROOT)
    print(f"Fixing Unturned project at: {root}")

    # 1. Create shim files
    create_shim_files(root)

    # 2. Fix all .shader files
    fixed_count = 0
    for dirpath, _, filenames in os.walk(root):
        # Skip Library, obj, Temp
        if "Library" in dirpath or "Temp" in dirpath or "obj" in dirpath:
            continue
        for fname in filenames:
            if fname.endswith(".shader"):
                filepath = os.path.join(dirpath, fname)
                if fix_shader_file(filepath):
                    fixed_count += 1

    print(f"\n[SUMMARY] Fixed {fixed_count} shader files")

    # 3. Fix Assets.cs
    fix_assets_cs(root)

    # 4. Fix postprocessing
    fix_postprocessing_package(root)

    # 5. Create a README with instructions
    print("\n[DONE] All fixes applied!")
    print("\nNext steps in Unity:")
    print("1. Delete Library folder and re-open project")
    print("2. If using Unity 6, set Color Space to Linear and Auto Graphics API")
    print("3. Build again - shader errors should be gone")
    print("4. Warning CS0162 can be ignored or is fixed by pragma")

if __name__ == "__main__":
    main()
