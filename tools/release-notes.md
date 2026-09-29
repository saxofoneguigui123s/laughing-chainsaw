Nightfall + Survival Expansion on the Unturned **U3 SDK v3.26.3.12** source tree.

**Download:** `U3-SDK-Nightfall-Survival-Expansion-v@@VERSION@@.zip`
**sha256:** `@@SHA256@@` (also published as the `.zip.sha256` asset)

## Fixed: CS1069 — `NavMeshAgent` could not be found in `UnityEngine.AI`

`Assets/Runtime/Assembly-CSharp/Unturned/Survival/SurvivalRescueAnchor.cs` is the only script in the SDK that uses `UnityEngine.AI`. Unity compiles a built-in module only when the module is declared in `Packages/manifest.json`, and the AI module was missing, so `Assembly-CSharp` failed to compile:

```
SurvivalRescueAnchor.cs(38,11): error CS1069: The type name 'NavMeshAgent' could not be found
in the namespace 'UnityEngine.AI'. This type has been forwarded to assembly 'UnityEngine.AIModule'
```

Changes in this build:

- `Packages/manifest.json` — added `"com.unity.modules.ai": "1.0.0"`.
- `Packages/packages-lock.json` — matching builtin entry.
- `NIGHTFALL_UPGRADE.md` — documents the requirement and the runtime caveat below.

Everything else is byte-identical to the previous archive: both contain the same 5,578 entries, and only those three files differ.

## Runtime caveat

No prefab, scene, or object shipped in this SDK has a `NavMeshAgent` component, and Unturned's zombie/NPC movement does not use Unity navmesh. `Survivor.GetComponent<NavMeshAgent>()` therefore returns null on unmodified content and the rescue event runs its documented radio-beacon fallback. The escort leg only activates on maps that add a `NavMeshAgent` to the survivor object and bake a navmesh over that area.

## Previous builds

`U3-SDK-Nightfall-Survival-Expansion-v3.26.3.12.1.zip` is left in place for reference; it does not compile until the AI module is enabled in the Package Manager.
