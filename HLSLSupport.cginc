// HLSLSupport.cginc - Compatibility shim for Unity 6 / Unity 2021+
// Original file was removed in Unity 2021.2+. It was automatically included for CGPROGRAM shaders.
// This shim provides empty compatibility layer so old shaders that explicitly #include it can compile.

#ifndef HLSLSUPPORT_INCLUDED
#define HLSLSUPPORT_INCLUDED

// Unity 6 already defines all necessary macros internally.
// We keep this file empty for compatibility, but define some legacy macros if missing.

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

#endif // HLSLSUPPORT_INCLUDED
