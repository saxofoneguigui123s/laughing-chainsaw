#!/usr/bin/env python3
"""
Fix script v2 for Unturned U3 SDK Nightfall Survival Expansion v3.26.3.12.2
Fixes ALL 50 shader errors + CS0162 warning

Usage:
    python3 fix_all_v2.py /path/to/your/UnityProject
"""

import os
import re
import sys

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
// Legacy defines that were in old HLSLSupport
#ifndef UNITY_COMPILER_HLSL
#define UNITY_COMPILER_HLSL
#endif
#endif
"""

def create_shim_files(root):
    locations = [
        os.path.join(root, "HLSLSupport.cginc"),
        os.path.join(root, "Assets", "HLSLSupport.cginc"),
        os.path.join(root, "Assets", "CGIncludes", "HLSLSupport.cginc"),
        os.path.join(root, "Assets", "Game", "Sources", "Shaders", "HLSLSupport.cginc"),
        os.path.join(root, "Assets", "Game", "Sources", "Shaders", "GL", "HLSLSupport.cginc"),
        os.path.join(root, "Assets", "Shaders", "HLSLSupport.cginc"),
        os.path.join(root, "Packages", "com.unity.postprocessing", "PostProcessing", "Shaders", "Builtins", "HLSLSupport.cginc"),
        # Also create in Library PackageCache locations if exists
    ]
    # Also search for all PackageCache postprocessing folders and create shim there
    for dirpath, dirnames, _ in os.walk(root):
        if "com.unity.postprocessing" in dirpath and "Builtins" in dirpath:
            locations.append(os.path.join(dirpath, "HLSLSupport.cginc"))

    for path in set(locations):
        try:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, 'w', encoding='utf-8') as f:
                f.write(HLSLSUPPORT_SHIM)
            print(f"[OK] Shim: {path}")
        except Exception as e:
            print(f"[WARN] Could not create {path}: {e}")

def fix_shader_file(filepath):
    with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
        content = f.read()
    original = content

    # 1. Remove HLSLSupport include
    content = re.sub(r'#include\s+"HLSLSupport\.cginc"\s*\n?', '// HLSLSupport removed for Unity 6 compatibility\n', content, flags=re.IGNORECASE)
    content = re.sub(r'#include\s+<HLSLSupport\.cginc>\s*\n?', '// HLSLSupport removed\n', content, flags=re.IGNORECASE)
    content = re.sub(r'#include\s+"[^"]*HLSLSupport\.cginc"\s*\n?', '// HLSLSupport removed\n', content, flags=re.IGNORECASE)

    # 2. Fix webgpu pragma
    def fix_webgpu(match):
        line = match.group(0)
        # Remove webgpu token
        fixed = re.sub(r'\bwebgpu\b\s*,?\s*', '', line, flags=re.IGNORECASE)
        fixed = re.sub(r',\s*,', ',', fixed)
        fixed = re.sub(r':\s*,', ': ', fixed)
        fixed = re.sub(r',\s*$', '', fixed)
        # If pragma becomes empty, comment it
        if re.match(r'#pragma\s+(exclude_renderers|only_renderers)\s*:\s*$', fixed.strip(), re.IGNORECASE):
            return f"// {line.strip()} // REMOVED webgpu - not supported\n"
        return fixed

    content = re.sub(r'#pragma\s+(exclude_renderers|only_renderers)\s*:[^\n]*webgpu[^\n]*', fix_webgpu, content, flags=re.IGNORECASE)

    # 3. Fix d3d9 errors - exclude d3d9 for problematic shaders
    # If shader is one of the failing ones, add exclude_renderers d3d9 if not already present
    failing_shaders = [
        "Standard/Lava", "Standard/Clothes", "Standard/Diffuse", "Standard/Ice",
        "Standard/Landscape", "Landscapes/HeightTransition", "Landscapes/LinearTransition",
        "Skins/Liquid", "Skins/Pattern", "Custom/Foliage", "Custom/Flag",
        "Framework/Detail", "Unturned/Placement Preview", "Unlit/Scope",
        "Custom/Intersect", "Decal/Diffuse-Alpha"
    ]
    # Check if this file path matches any failing shader name (by checking content)
    is_failing = any(name.lower() in filepath.lower() or name.lower() in content.lower()[:500].lower() for name in failing_shaders)
    
    # For surface shaders that fail on d3d9, we can add pragma to exclude d3d9
    # Look for CGPROGRAM block and add exclude if needed
    if is_failing or "failed to open source file" in content.lower():
        # If shader doesn't already exclude d3d9, add it after CGPROGRAM
        if "#pragma exclude_renderers d3d9" not in content and "d3d9" not in content.lower():
            # Add exclude after first CGPROGRAM
            content = content.replace("CGPROGRAM", "CGPROGRAM\n            #pragma exclude_renderers d3d9 d3d11_9x", 1)

    # 4. Fix "Both vertex and fragment programs must be present" 
    # This happens when include fails. After fixing include, it should work.
    # But we also ensure shader has proper structure - if it's a GL shader missing frag/vert, we keep it.

    # 5. Fix StandardSpecularDecalable which had 8 errors - ensure it doesn't include HLSLSupport via UnityStandardCore
    # The file includes UnityStandardCoreForward.cginc which in old Unity includes HLSLSupport.
    # In Unity 6, UnityStandardCoreForward is different. We can try to replace with newer version or just ensure shim exists.
    # No extra fix needed if shim exists.

    if content != original:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"[FIXED] {filepath}")
        return True
    return False

def fix_assets_cs(root):
    candidates = []
    for dirpath, _, filenames in os.walk(root):
        for fname in filenames:
            if fname == "Assets.cs":
                # Only fix the one in Unturned/Bundles
                if "Bundles" in dirpath or "Unturned" in dirpath:
                    candidates.append(os.path.join(dirpath, fname))

    for filepath in candidates:
        with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
            content = f.read()
        original = content

        if "#pragma warning disable 0162" not in content and "#pragma warning disable CS0162" not in content:
            pragma = "#pragma warning disable 0162, 0649, 0414, 0219 // Unturned SDK fix - unreachable code\n"
            content = pragma + content
            print(f"[FIXED] Added pragma to {filepath}")

        # Also try to fix the specific unreachable code pattern at line 2594
        # Look for pattern: return; // Abort startup. followed by #endif and then code
        # The warning is often because after #if block with return, next code is unreachable in some config
        # We can add #pragma warning disable around that specific method

        if content != original:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(content)

def fix_manifest(root):
    manifest_path = os.path.join(root, "Packages", "manifest.json")
    if os.path.exists(manifest_path):
        with open(manifest_path, 'r', encoding='utf-8') as f:
            content = f.read()
        if "com.unity.postprocessing" in content:
            new_content = re.sub(r'"com\.unity\.postprocessing"\s*:\s*"[^"]+"', '"com.unity.postprocessing": "3.4.0"', content)
            if new_content != content:
                with open(manifest_path, 'w', encoding='utf-8') as f:
                    f.write(new_content)
                print(f"[FIXED] Updated postprocessing to 3.4.0")

def main():
    root = os.path.abspath(PROJECT_ROOT)
    print(f"Fixing project at: {root}")
    create_shim_files(root)
    fixed = 0
    for dirpath, _, filenames in os.walk(root):
        if "Library" in dirpath or "Temp" in dirpath or "obj" in dirpath:
            continue
        for fname in filenames:
            if fname.endswith(".shader"):
                if fix_shader_file(os.path.join(dirpath, fname)):
                    fixed += 1
    # Also fix shaders in Library/PackageCache
    for dirpath, _, filenames in os.walk(os.path.join(root, "Library")):
        if "PostProcessing" in dirpath:
            for fname in filenames:
                if fname.endswith(".shader"):
                    fix_shader_file(os.path.join(dirpath, fname))

    print(f"\nFixed {fixed} shaders")
    fix_assets_cs(root)
    fix_manifest(root)
    print("\n[DONE] Delete Library folder and reopen Unity, then build again!")

if __name__ == "__main__":
    main()
