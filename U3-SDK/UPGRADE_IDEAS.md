# Survival expansion status

## Implemented in this source pass

The first server-authoritative pass now connects the requested systems to existing Unturned hooks and avoids map names or custom item GUIDs:

1. **Choice-based weapon mastery:** 8 credited zombie kills per ranged/melee family unlock one of two non-damage sidegrades. Ranged: quieter fire or reduced wear. Melee: quieter strikes or reduced stamina use.
2. **Medical research chain:** three pickup samples, two crafts, then a disinfectant-bearing medical item. Completion increases immunity and awards XP on the server.
3. **Mutation ecology / infestations:** two populated zombie sectors rotate daily, changing ordinary infected composition (sprinters/crawlers, not increased health). A 16-kill clear ends the daily mutation/loot bonus and reduces noise pressure.
4. **Safehouse upgrades:** claimed beds act as anchors. Water, medical, floodlight/threat-scan, and reinforced-gate upgrades spend salvage credits and have their documented cadence/noise tradeoffs.
5. **Rescue radio events:** configured anchors call players to locate/escort an NPC with a networked `NavMeshAgent` to an evacuation point, then defend extraction in three waves. A beacon holdout fallback is used if no survivor transform is assigned.
6. **Noise-driven hordes:** alerts, generators, and engines add regional noise. A 10-second warning precedes the server redirecting nearby zombies.

See `NIGHTFALL_UPGRADE.md` for exact tuning, server integration, map setup, test steps, and limitations. The rescue anchor requires placement on each map; the other systems discover existing zombie regions and claimed beds. New progression is session-only and still requires Unity/dedicated-server testing.

## Future content work

- Replace broad pickup/craft research counters with curated sample items and treatment blueprints once project-specific item GUIDs are chosen.
- Add an art-authored floodlight prefab/effect and replicate its installed state to nearby players.
- Add persistent save data after the progression schema and migration policy are agreed.
- Expand safehouse construction into a server-owned perimeter/door state if the project requires protecting open doors rather than relying on solid buildable collision.
- Add map-specific survivor animation, voice lines, and extraction markers to rescue anchors.
- Tune horde thresholds and rescue spawn counts after multiplayer playtests.
