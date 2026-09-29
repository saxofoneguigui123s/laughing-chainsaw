using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// Editor script to automatically fix Unturned SDK shader errors for Unity 6 / Unity 2021+
/// Fixes:
/// - HLSLSupport.cginc not found
/// - webgpu pragma warnings
/// - Ensures shim file exists
/// </summary>
public class UnturnedShaderFixer : AssetPostprocessor
{
    // Create shim file on editor load
    [InitializeOnLoadMethod]
    static void CreateShimOnLoad()
    {
        string[] shimPaths = new string[]
        {
            "Assets/CGIncludes/HLSLSupport.cginc",
            "Assets/Game/Sources/Shaders/HLSLSupport.cginc",
            "Assets/HLSLSupport.cginc",
            "HLSLSupport.cginc"
        };

        string shimContent = @"// HLSLSupport.cginc - Compatibility shim for Unity 6
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
#endif
";

        foreach (var path in shimPaths)
        {
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(fullPath))
            {
                File.WriteAllText(fullPath, shimContent);
                Debug.Log($"[UnturnedFix] Created shim: {path}");
            }
        }
    }

    // Fix shaders when they are imported
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string assetPath in importedAssets)
        {
            if (assetPath.EndsWith(".shader"))
            {
                string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
                if (File.Exists(fullPath))
                {
                    string content = File.ReadAllText(fullPath);
                    string original = content;

                    // Fix HLSLSupport include
                    content = Regex.Replace(content, @"#include\s+""HLSLSupport\.cginc""", "// HLSLSupport removed for Unity 6 compatibility\n// #include \"HLSLSupport.cginc\"", RegexOptions.IgnoreCase);

                    // Fix webgpu pragma
                    content = Regex.Replace(content, @"#pragma\s+(exclude_renderers|only_renderers)\s*:[^\n]*webgpu[^\n]*", (match) =>
                    {
                        string line = match.Value;
                        string fixedLine = Regex.Replace(line, @"\bwebgpu\b\s*,?\s*", "", RegexOptions.IgnoreCase);
                        fixedLine = Regex.Replace(fixedLine, @",\s*,", ",");
                        fixedLine = Regex.Replace(fixedLine, @":\s*,", ": ");
                        fixedLine = Regex.Replace(fixedLine, @",\s*$", "");
                        if (Regex.IsMatch(fixedLine.Trim(), @"#pragma\s+(exclude_renderers|only_renderers)\s*:\s*$", RegexOptions.IgnoreCase))
                        {
                            return $"// {line.Trim()} // removed webgpu";
                        }
                        return fixedLine;
                    }, RegexOptions.IgnoreCase);

                    if (content != original)
                    {
                        File.WriteAllText(fullPath, content);
                        Debug.Log($"[UnturnedFix] Fixed shader: {assetPath}");
                    }
                }
            }
        }
    }

    [MenuItem("Unturned/Fix All Shaders (Unity 6 Compatibility)")]
    public static void FixAllShadersMenu()
    {
        int fixedCount = 0;
        string[] shaderFiles = Directory.GetFiles(Application.dataPath, "*.shader", SearchOption.AllDirectories);

        foreach (var shaderPath in shaderFiles)
        {
            string content = File.ReadAllText(shaderPath);
            string original = content;

            content = Regex.Replace(content, @"#include\s+""HLSLSupport\.cginc""", "// HLSLSupport removed for Unity 6 compatibility\n// #include \"HLSLSupport.cginc\"", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, @"#pragma\s+(exclude_renderers|only_renderers)\s*:[^\n]*webgpu[^\n]*", (match) =>
            {
                string line = match.Value;
                string fixedLine = Regex.Replace(line, @"\bwebgpu\b\s*,?\s*", "", RegexOptions.IgnoreCase);
                fixedLine = Regex.Replace(fixedLine, @",\s*,", ",");
                fixedLine = Regex.Replace(fixedLine, @":\s*,", ": ");
                fixedLine = Regex.Replace(fixedLine, @",\s*$", "");
                if (Regex.IsMatch(fixedLine.Trim(), @"#pragma\s+(exclude_renderers|only_renderers)\s*:\s*$", RegexOptions.IgnoreCase))
                {
                    return $"// {line.Trim()} // removed webgpu";
                }
                return fixedLine;
            }, RegexOptions.IgnoreCase);

            if (content != original)
            {
                File.WriteAllText(shaderPath, content);
                fixedCount++;
            }
        }

        // Also fix postprocessing package cache
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string[] ppPaths = new string[]
        {
            Path.Combine(projectRoot, "Packages/com.unity.postprocessing"),
            Path.Combine(projectRoot, "Library/PackageCache")
        };

        foreach (var ppRoot in ppPaths)
        {
            if (Directory.Exists(ppRoot))
            {
                var ppShaders = Directory.GetFiles(ppRoot, "*.shader", SearchOption.AllDirectories);
                foreach (var shaderPath in ppShaders)
                {
                    string content = File.ReadAllText(shaderPath);
                    string original = content;

                    content = Regex.Replace(content, @"#include\s+""HLSLSupport\.cginc""", "// HLSLSupport removed\n// #include \"HLSLSupport.cginc\"", RegexOptions.IgnoreCase);
                    content = Regex.Replace(content, @"#pragma\s+(exclude_renderers|only_renderers)\s*:[^\n]*webgpu[^\n]*", (match) =>
                    {
                        string line = match.Value;
                        string fixedLine = Regex.Replace(line, @"\bwebgpu\b\s*,?\s*", "", RegexOptions.IgnoreCase);
                        fixedLine = Regex.Replace(fixedLine, @",\s*,", ",");
                        fixedLine = Regex.Replace(fixedLine, @":\s*,", ": ");
                        fixedLine = Regex.Replace(fixedLine, @",\s*$", "");
                        if (Regex.IsMatch(fixedLine.Trim(), @"#pragma\s+(exclude_renderers|only_renderers)\s*:\s*$", RegexOptions.IgnoreCase))
                        {
                            return $"// {line.Trim()} // removed webgpu";
                        }
                        return fixedLine;
                    }, RegexOptions.IgnoreCase);

                    if (content != original)
                    {
                        File.WriteAllText(shaderPath, content);
                        fixedCount++;
                    }
                }
            }
        }

        Debug.Log($"[UnturnedFix] Fixed {fixedCount} shader files. Please reimport all or delete Library folder.");
        EditorUtility.DisplayDialog("Unturned Shader Fix", $"Fixed {fixedCount} shaders!\n\n1. Delete Library folder\n2. Reopen project\n3. Build again", "OK");

        AssetDatabase.Refresh();
    }

    [MenuItem("Unturned/Fix Assets.cs Warning CS0162")]
    public static void FixAssetsCS()
    {
        string[] assetsFiles = Directory.GetFiles(Application.dataPath, "Assets.cs", SearchOption.AllDirectories);
        foreach (var filePath in assetsFiles)
        {
            string content = File.ReadAllText(filePath);
            if (!content.Contains("#pragma warning disable 0162"))
            {
                string pragma = "#pragma warning disable 0162, 0649, 0414 // Fix unreachable code warning\n";
                content = pragma + content;
                File.WriteAllText(filePath, content);
                Debug.Log($"[UnturnedFix] Fixed {filePath}");
            }
        }
        AssetDatabase.Refresh();
    }
}
