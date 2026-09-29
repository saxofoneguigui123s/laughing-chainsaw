# Nightfall + Survival Expansion — implementation notes

This update layers the existing Daybreak/Nightfall activities and a generic survival-systems pass onto U3 SDK `v3.26.3.12`. Core gameplay state and rewards are validated on the server. No named map, new item GUID, or custom loot table is assumed.

## Implemented systems

### Weapon mastery sidegrades
- Server counts player-credited zombie kills by the equipped weapon family.
- At 8 kills, the activity board offers one permanent choice per family for the current server session:
  - **Ranged:** quieter gunfire (30% smaller alert radius) or 30% lower weapon-wear chance.
  - **Melee:** quieter strikes or 18% lower stamina cost.
- Choices are mutually exclusive within their family and do not increase damage.

### Medical research and infection
- Research stages: collect 3 item pickups as samples, craft 2 items as treatment supplies, then consume an item with a non-zero disinfectant value.
- Completing the chain grants 90 XP and increases immunity through the server's existing virus API.
- The chain is generic because the SDK has no project-specific sample/treatment item IDs. The first two steps therefore count ordinary pickup/craft stats; only the final step requires a disinfectant-bearing medical item.

### Mutation ecology and infestation hotspots
- Each in-game day, two populated zombie navigation sectors are selected and rotate with the world's day counter.
- In active sectors, a portion of ordinary zombies spawn as existing **sprinter** or **crawler** specialities. Zombie health is not raised.
- Active hotspots receive a small extra loot quantity/odds bonus. Killing 16 credited infected in a sector clears its infestation for the rest of that in-game day; mutation/loot bonuses stop and its noise contribution is reduced.
- The board calls out active sector clues, while the changed infected mix is the in-world cue.

### Claimed-bed safehouses and upgrade tradeoffs
- A claimed bed anchors the safehouse. The owner can spend 3 salvage credits per upgrade while within 10 m; resource finds earn credits.
- **Water purifier:** +3 water about every 55 seconds while the owner is within 18 m; refills make noise.
- **Medical station:** +4 immunity about every 60 seconds while the owner is nearby.
- **Floodlights:** widens the local threat scan from 28 m to 42 m, but emits periodic noise. This is a gameplay representation only; no visual lamp prefab is created.
- **Reinforced gate:** infected-origin damage to structures and barricades within 18 m is reduced by 35%. It is not invulnerable and does not reduce player-origin damage.
- The upgrade does not create walls or close doors. Use solid buildables and keep doors/gates closed; the base game's physical collision blocks infected, and existing zombie attacks can break the barrier. An open doorway is still an entrance.

### Noise-driven hordes
- Existing world-alert calls (including gunfire/melee alerts, powered generators, and running vehicle engines) add noise to their navigation sector.
- At the threshold, nearby players receive a **10-second warning** to hide, flee, or defend. The server then redirects up to 24 nearby living zombies toward the last loud location. A 75-second regional cooldown prevents constant repeats.
- Noise values decay over time; this uses the map's existing infected pool rather than creating unlimited zombies.

### Rescue radio calls
- Every third in-game day, during the daytime call window, the board announces one configured rescue anchor and its coordinates.
- Reaching the anchor starts an escort when an NPC and evacuation point are configured; the survivor pauses unless a participant stays within 24 m. At the evacuation point, three short defense waves begin. The server reuses eligible dead zombies from that region's existing pool; if it cannot, it redirects nearby living infected as a lighter wave. Without a configured NPC, the radio-beacon fallback begins the defense waves at the anchor.
- Participants still near the extraction receive 120 XP and immunity support.
- **Map setup required:** add `SurvivalRescueAnchor` to an existing survivor/NPC or rescue location in each map, set its `CallSign`, assign `Survivor` and `EvacuationPoint`, and ensure the survivor agent/navigation and the map's networking replicate movement. If no survivor/evacuation references are assigned, the event falls back to a radio-beacon holdout; if no anchor component exists, no rescue call is generated on that map. This avoids spawning an unnetworked placeholder NPC or assuming map-specific assets.

## Existing Daybreak / Nightfall features

- Night bounties, full-moon variant, consecutive-night XP, and rotating daytime contracts remain active.
- The board combines those objectives with mastery, research, safehouse, hotspot, and rescue status.
- XP, infection/immunity changes, upgrades, mutation selection, loot, and horde behavior are server-authoritative. HUD counters are presentation state only.

## Integration points

- `SurvivalExpansion.cs`, `SurvivalExpansionRuntime.cs`: system state, server hooks, noise/hotspot rotation, and passive safehouse effects.
- `SurvivalRescueAnchor.cs`: optional per-map NPC/extraction component.
- `NightfallActivityHUD.cs`: combined activity and systems board.
- Hooks in `Player.cs`, `DamageTool.cs`, `AlertTool.cs`, `ZombieManager.cs`, `UseableGun.cs`, `UseableMelee.cs`, `UseableConsumeable.cs`, `BarricadeManager.cs`, and `StructureManager.cs` connect existing gameplay events to the systems.

Progression is currently held in server memory for the running session; it is not serialized to player saves and resets when the server process restarts. This avoids introducing an unreviewed persistence format into the SDK.

## Test plan

1. Open the project in its documented Unity editor and enter a survival map with zombie spawn regions.
2. Verify normal kills advance the correct mastery family; choose each sidegrade and confirm no damage increase.
3. Pick up 3 items, craft 2 items, then use a disinfectant-bearing medical item. Confirm the server awards the research reward once.
4. Check rotating sectors over in-game days; verify sprinters/crawlers appear without health inflation, hotspot loot increases, and 16 credited kills clear a sector.
5. Claim a bed, collect 3 resource credits, buy each upgrade, and verify the range/cooldown effects. Test zombie damage on closed barriers and confirm they remain destructible.
6. Fire repeatedly or run a generator/engine until the warning appears; confirm the 10-second escape/defense window and that the horde is delayed.
7. Add a rescue anchor to a test NPC. Verify call timing, three waves, optional escort path, and server-side reward on a dedicated server with a second client.

Unity, Steam, and Unturned runtime dependencies are unavailable in this workspace, so no Unity compile or in-game multiplayer validation could be performed here. The source changes need that test pass before production use.

## Scope / known limitations

- “Generic any map” means the code discovers existing zombie navigation regions and claimed beds instead of using hard-coded map names. Rescue escort still needs a map-authored anchor and survivor/evacuation references.
- Rescue waves reuse the map's existing zombie pool. They may be smaller if the region has too few eligible zombies.
- Research uses ordinary pickup/craft stats for its first two generic steps; a project-specific content pack can later narrow those to curated sample and treatment recipes.
- Floodlights currently improve the threat scanner and attract noise; no scene light mesh or lamp asset is generated.
- Safehouses rely on the base game's collision and zombie attack logic; the code does not auto-build an enclosed perimeter or make open doors safe.
- All new progression is session-only, not save-persistent.
