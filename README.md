# laughing-chainsaw

Working repository for the **Unturned U3 SDK** source tree plus the **Nightfall + Survival Expansion** code pass.

## Layout

| Path | What it is |
| --- | --- |
| `U3-SDK/` | Unturned U3 SDK `v3.26.3.12` source (Unity `2022.3.62f3`), imported from the `unturned` release asset `U3-SDK-Nightfall-Survival-Expansion-v3.26.3.12.1.zip`. |
| `U3-SDK/Assets/Runtime/Assembly-CSharp/Unturned/Survival/` | The expansion: `NightfallActivity`, `NightfallActivityHUD`, `SurvivalExpansion`, `SurvivalExpansionRuntime`, `SurvivalRescueAnchor`. |
| `U3-SDK/NIGHTFALL_UPGRADE.md` | System-by-system implementation notes, tuning values, map setup, test plan, and known limitations. |
| `U3-SDK/UPGRADE_IDEAS.md` | Status of the implemented pass and the backlog for future content work. |

## Opening the project

1. Install Unity **2022.3.62f3**.
2. Open `U3-SDK/` with Unity Hub.
3. Open the `Assets/GameStartup.unity` scene and press Play (Steam and Unturned must be installed and running).

## Build fix applied

`Packages/manifest.json` was missing the built-in **AI** module, so `SurvivalRescueAnchor.cs` failed to compile with `CS1069` on `UnityEngine.AI.NavMeshAgent`. `com.unity.modules.ai` is now declared in `Packages/manifest.json`, with the matching builtin entry in `Packages/packages-lock.json`. See the "Build requirement" section of `U3-SDK/NIGHTFALL_UPGRADE.md` for details and the runtime caveat about navmesh-based escort.

## Licensing

`U3-SDK/` remains under the U3 SDK License Agreement (`U3-SDK/LICENSE.txt`), copyright Smartly Dressed Games Ltd.
