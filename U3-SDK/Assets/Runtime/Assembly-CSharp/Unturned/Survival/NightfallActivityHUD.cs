////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using UnityEngine;

namespace SDG.Unturned
{
	/// <summary>
	/// Compact Nightfall and survival-systems board. The stock life/inventory UI is
	/// left untouched; this companion panel can be toggled in play.
	/// </summary>
	public sealed class NightfallActivityHUD : MonoBehaviour
	{
		private Player player;
		private bool isBoardOpen;
		private int nearbyThreats;
		private float nextThreatScan;
		private GUIStyle titleStyle;
		private GUIStyle bodyStyle;
		private GUIStyle mutedStyle;

		internal void Initialize(Player owner)
		{
			player = owner;
		}

		private void Update()
		{
			if (player == null || player.channel == null || !player.channel.IsLocalPlayer)
			{
				return;
			}

			if (Time.unscaledTime >= nextThreatScan)
			{
				nextThreatScan = Time.unscaledTime + 0.75f;
				ScanNearbyThreats();
			}
		}

		private void ScanNearbyThreats()
		{
			nearbyThreats = 0;
			List<Zombie> zombies = ZombieManager.AllZombies;
			if (zombies == null || player == null)
			{
				return;
			}

			float scanRadius = SurvivalExpansion.HasLocalSafehouseNearby(player) && SurvivalExpansion.GetLocalSafehouseUpgrade(ESafehouseUpgrade.Floodlights) > 0 ? 42f : 28f;
			Vector3 position = player.transform.position;
			for (int index = 0; index < zombies.Count; ++index)
			{
				Zombie zombie = zombies[index];
				if (zombie != null && !zombie.isDead && (zombie.transform.position - position).sqrMagnitude < scanRadius * scanRadius)
				{
					++nearbyThreats;
				}
			}
		}

		private void OnGUI()
		{
			if (player == null || player.channel == null || !player.channel.IsLocalPlayer || player.life == null || Dedicator.IsDedicatedServer)
			{
				return;
			}

			EnsureStyles();
			bool isNight = LightingManager.isNighttime;
			int target = NightfallActivity.CurrentTarget;
			float width = 330f;
			float height = 190f;
			Rect panel = new Rect(18f, 18f, width, height);
			GUI.Box(panel, GUIContent.none);
			GUI.Label(new Rect(panel.x + 14f, panel.y + 10f, 210f, 24f), isNight ? (NightfallActivity.IsFullMoon ? "FULL MOON — BOUNTY" : "NIGHTFALL — BOUNTY") : "DAYBREAK — REGROUP", titleStyle);
			GUI.Label(new Rect(panel.x + 14f, panel.y + 39f, width - 28f, 22f), isNight
				? $"Bounty {NightfallActivity.LocalCompletedBounties + 1}   •   Kills {NightfallActivity.LocalKills}/{target}"
				: "Restock, craft, and prepare before night.", bodyStyle);
			GUI.Label(new Rect(panel.x + 14f, panel.y + 65f, width - 28f, 20f), $"HP {player.life.health}   Food {player.life.food}   Water {player.life.water}   Infection {player.life.virus}", bodyStyle);
			GUI.Label(new Rect(panel.x + 14f, panel.y + 89f, width - 28f, 20f), $"Nearby zombies: {nearbyThreats}   •   Nights survived: {NightfallActivity.LocalNightsSurvived}", mutedStyle);

			string ecology = SurvivalExpansion.IsPlayerInHotspot(player)
				? "Infested sector: listen for fast footfalls and scraping; clear 16 infected to calm it."
				: "Noise carries: gunfire, engines, and generators can draw a sector horde.";
			GUI.Label(new Rect(panel.x + 14f, panel.y + 112f, width - 28f, 32f), ecology, mutedStyle);

			string fieldContract = NightfallActivity.LocalDayContractCompleted
				? $"Field contract: COMPLETE (+{NightfallActivity.DayContractReward} XP)"
				: $"Field contract ({(isNight ? "next day" : "today")}): {NightfallActivity.DayContractTitle}  {NightfallActivity.LocalDayContractCount}/{NightfallActivity.DayContractTarget}";
			GUI.Label(new Rect(panel.x + 14f, panel.y + 150f, width - 28f, 22f), fieldContract, mutedStyle);

			Rect button = new Rect(panel.x + width - 95f, panel.y + 8f, 82f, 25f);
			if (GUI.Button(button, isBoardOpen ? "CLOSE" : "BOARD"))
			{
				isBoardOpen = !isBoardOpen;
			}

			if (isBoardOpen)
			{
				DrawBoard(new Rect(18f, panel.yMax + 10f, 470f, 430f), target, isNight);
			}
		}

		private void DrawBoard(Rect panel, int target, bool isNight)
		{
			GUI.Box(panel, GUIContent.none);
			float x = panel.x + 14f;
			float width = panel.width - 28f;
			float y = panel.y + 10f;
			GUI.Label(new Rect(x, y, width, 24f), "EXPEDITION & SURVIVAL SYSTEMS", titleStyle);
			y += 29f;
			GUI.Label(new Rect(x, y, width, 20f), isNight
				? $"Night contract: clear {target} bounty points before dawn."
				: "Daybreak: prepare, research, and reinforce before night.", bodyStyle);
			y += 23f;
			GUI.Label(new Rect(x, y, width, 20f), $"Medical research: {SurvivalExpansion.GetLocalMedicalStageText()} ({SurvivalExpansion.LocalMedicalCount}/{SurvivalExpansion.LocalMedicalTarget})   •   Protocols {SurvivalExpansion.LocalMedicalRank}", bodyStyle);
			y += 23f;
			GUI.Label(new Rect(x, y, width, 20f), $"Salvage credits: {SurvivalExpansion.LocalSalvage}   (resource finds earn credits; safehouse upgrades cost {SurvivalExpansion.SafehouseUpgradeCost})", bodyStyle);
			y += 26f;

			DrawMasteryRow(x, y, width, "RANGED", SurvivalExpansion.LocalRangedKills, SurvivalExpansion.LocalRangedChoice, 0);
			y += 38f;
			DrawMasteryRow(x, y, width, "MELEE", SurvivalExpansion.LocalMeleeKills, SurvivalExpansion.LocalMeleeChoice, 1);
			y += 42f;

			bool nearBed = SurvivalExpansion.HasLocalSafehouseNearby(player);
			GUI.Label(new Rect(x, y, width, 20f), nearBed ? "SAFEHOUSE UPGRADES — claimed bed nearby" : "SAFEHOUSE — claim a bed, then stand within 10 m to upgrade", bodyStyle);
			y += 23f;
			DrawUpgradeRow(x, y, "Water purifier", "+3 water / 55 s; refills emit noise", ESafehouseUpgrade.Purifier, 0, nearBed);
			y += 27f;
			DrawUpgradeRow(x, y, "Medical station", "+4 immunity / 60 s", ESafehouseUpgrade.MedicalStation, 1, nearBed);
			y += 27f;
			DrawUpgradeRow(x, y, "Floodlights", "42 m threat scan; periodic noise", ESafehouseUpgrade.Floodlights, 2, nearBed);
			y += 27f;
			DrawUpgradeRow(x, y, "Reinforced gate", "35% less structure/barricade damage", ESafehouseUpgrade.ReinforcedGate, 3, nearBed);
			y += 30f;

			GUI.Label(new Rect(x, y, width, 36f), SurvivalExpansion.GetLocalRescueStatus(player), bodyStyle);
			y += 37f;
			string moonHint = NightfallActivity.IsFullMoon ? " Full moon active." : string.Empty;
			GUI.Label(new Rect(x, y, width, 33f), $"Mutation ecology: two spawn sectors rotate daily; composition shifts, not health.{moonHint}", mutedStyle);
			y += 34f;
			GUI.Label(new Rect(x, y, width, 31f), "Zombies still need a physical wall/closed gate to block entry. Reinforcement reduces damage; it does not make buildables indestructible.", mutedStyle);
		}

		private void DrawMasteryRow(float x, float y, float width, string label, int kills, byte choice, byte family)
		{
			string selection = choice == 1 ? "Quiet" : (choice == 2 ? (family == 0 ? "Durable" : "Efficient") : "unselected");
			GUI.Label(new Rect(x, y, 138f, 24f), $"{label}: {kills}/{SurvivalExpansion.MasteryUnlockKills} — {selection}", bodyStyle);
			bool available = kills >= SurvivalExpansion.MasteryUnlockKills && choice == 0;
			GUI.enabled = available;
			string first = family == 0 ? "Quiet fire" : "Quiet strikes";
			string second = family == 0 ? "Less weapon wear" : "Less stamina";
			if (GUI.Button(new Rect(x + 146f, y, 130f, 25f), first))
			{
				player.RequestSurvivalMasteryChoice(family, 1);
			}
			if (GUI.Button(new Rect(x + 284f, y, 155f, 25f), second))
			{
				player.RequestSurvivalMasteryChoice(family, 2);
			}
			GUI.enabled = true;
		}

		private void DrawUpgradeRow(float x, float y, string label, string description, ESafehouseUpgrade upgrade, byte index, bool nearBed)
		{
			bool purchased = SurvivalExpansion.GetLocalSafehouseUpgrade(upgrade) > 0;
			GUI.Label(new Rect(x, y, 270f, 22f), $"{label}: {description}", mutedStyle);
			GUI.enabled = nearBed && !purchased && SurvivalExpansion.LocalSalvage >= SurvivalExpansion.SafehouseUpgradeCost;
			if (GUI.Button(new Rect(x + 286f, y - 1f, 154f, 24f), purchased ? "INSTALLED" : $"INSTALL — {SurvivalExpansion.SafehouseUpgradeCost}"))
			{
				player.RequestSafehouseUpgrade(index);
			}
			GUI.enabled = true;
		}

		private void EnsureStyles()
		{
			if (titleStyle != null)
			{
				return;
			}

			titleStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 15,
				fontStyle = FontStyle.Bold,
				wordWrap = false
			};
			bodyStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 12,
				wordWrap = true
			};
			mutedStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 11,
				wordWrap = true
			};
		}
	}
}
