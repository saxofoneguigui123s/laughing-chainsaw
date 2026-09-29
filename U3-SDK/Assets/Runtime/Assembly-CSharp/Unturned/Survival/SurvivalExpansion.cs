////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using Steamworks;
using System.Collections.Generic;
using UnityEngine;

namespace SDG.Unturned
{
	internal enum EMasteryFamily : byte
	{
		Ranged,
		Melee
	}

	internal enum ESafehouseUpgrade : byte
	{
		Purifier,
		MedicalStation,
		Floodlights,
		ReinforcedGate
	}

	/// <summary>
	/// Server-authoritative systems layered onto the base survival loop. Session
	/// progression deliberately reuses existing stats, items, nav regions, beds,
	/// zombie pools, and networking rather than requiring new item IDs.
	/// </summary>
	internal static class SurvivalExpansion
	{
		private sealed class PlayerState
		{
			public int salvage;
			public int[] weaponKills = new int[2];
			public byte[] weaponChoices = new byte[2];
			public int medicalStage;
			public int medicalCount;
			public int medicalResearchRank;
			public byte[] safehouseUpgrades = new byte[4];
			public float nextPurifierTick;
			public float nextMedicalTick;
			public float nextFloodlightNoise;
		}

		private sealed class NoiseCell
		{
			public float intensity;
			public float lastUpdate;
			public float cooldownUntil;
			public float pendingHordeAt;
			public bool pendingHorde;
			public Vector3 lastPosition;
		}

		private static readonly Dictionary<CSteamID, PlayerState> serverPlayers = new Dictionary<CSteamID, PlayerState>();
		private static readonly Dictionary<byte, NoiseCell> noiseByRegion = new Dictionary<byte, NoiseCell>();
		private static readonly Dictionary<byte, int> hotspotKills = new Dictionary<byte, int>();
		private static readonly HashSet<byte> clearedHotspots = new HashSet<byte>();
		private static readonly List<byte> validHotspotRegions = new List<byte>();
		private static InteractableBed[] bedCache = new InteractableBed[0];
		private static SurvivalRescueAnchor[] rescueAnchorCache = new SurvivalRescueAnchor[0];
		private static float nextBedScan;
		private static float nextRescueScan;
		private static float nextServerTick;
		private static float nextPersistentNoiseScan;
		private static float nextLocalSafehouseScan;
		private static bool localHasSafehouseNearby;
		private static CSteamID localSafehouseCheckOwner;
		private static long hotspotDate = long.MinValue;
		private static long rescueAnnouncementDate = long.MinValue;

		private static int localSalvage;
		private static int[] localWeaponKills = new int[2];
		private static byte[] localWeaponChoices = new byte[2];
		private static int localMedicalStage;
		private static int localMedicalCount;
		private static int localMedicalResearchRank;
		private static byte[] localSafehouseUpgrades = new byte[4];
		private static readonly Dictionary<byte, int> localHotspotProgress = new Dictionary<byte, int>();
		private static long localHotspotDate = long.MinValue;

		internal static int LocalSalvage => localSalvage;
		internal static int LocalMedicalStage => localMedicalStage;
		internal static int LocalMedicalCount => localMedicalCount;
		internal static int LocalMedicalRank => localMedicalResearchRank;
		internal static int LocalMedicalTarget => localMedicalStage == 0 ? 3 : (localMedicalStage == 1 ? 2 : 1);
		internal static int LocalRangedKills => localWeaponKills[(int) EMasteryFamily.Ranged];
		internal static int LocalMeleeKills => localWeaponKills[(int) EMasteryFamily.Melee];
		internal static byte LocalRangedChoice => localWeaponChoices[(int) EMasteryFamily.Ranged];
		internal static byte LocalMeleeChoice => localWeaponChoices[(int) EMasteryFamily.Melee];
		internal static int MasteryUnlockKills => 8;
		internal static int SafehouseUpgradeCost => 3;
		private static bool IsSurvivalLevel => Level.isLoaded && Level.info != null && Level.info.type == ELevelType.SURVIVAL;

		internal static int GetLocalSafehouseUpgrade(ESafehouseUpgrade upgrade)
		{
			return localSafehouseUpgrades[(int) upgrade];
		}

		internal static bool HasLocalSafehouseNearby(Player player)
		{
			if (player?.channel?.owner == null)
			{
				return false;
			}

			CSteamID owner = player.channel.owner.playerID.steamID;
			if (owner != localSafehouseCheckOwner || Time.unscaledTime >= nextLocalSafehouseScan)
			{
				localSafehouseCheckOwner = owner;
				nextLocalSafehouseScan = Time.unscaledTime + 1f;
				localHasSafehouseNearby = FindNearestOwnedBed(player, 10f) != null;
			}

			return localHasSafehouseNearby;
		}

		internal static string GetLocalMedicalStageText()
		{
			switch (localMedicalStage)
			{
				case 0: return "Collect 3 samples";
				case 1: return "Craft 2 supplies";
			default: return "Use a disinfectant";
			}
		}

		internal static bool IsRescueWindow
		{
			get
			{
				return IsSurvivalLevel && LightingManager.isDaytime && LightingManager.day >= 0.35f && LightingManager.day <= 0.82f && LightingManager.DateCounter % 3 == 1;
			}
		}

		internal static SurvivalRescueAnchor GetLocalRescueAnchor()
		{
			RefreshRescueAnchors();
			if (!IsRescueWindow || rescueAnchorCache.Length == 0)
			{
				return null;
			}

			int index = PositiveModulo(LightingManager.DateCounter, rescueAnchorCache.Length);
			return rescueAnchorCache[index];
		}

		internal static bool IsSelectedRescueAnchor(SurvivalRescueAnchor anchor)
		{
			return anchor != null && anchor == GetLocalRescueAnchor();
		}

		internal static string GetLocalRescueStatus(Player player)
		{
			SurvivalRescueAnchor anchor = GetLocalRescueAnchor();
			if (anchor == null)
			{
				return IsRescueWindow ? "No rescue anchor is configured on this map." : "Radio call: check the board every third day.";
			}

			float distance = player == null ? 0f : Vector3.Distance(player.transform.position, anchor.transform.position);
			return $"SOS: {anchor.CallSign} — {distance:0} m away";
		}

		private static PlayerState GetServerState(CSteamID steamID)
		{
			if (!serverPlayers.TryGetValue(steamID, out PlayerState state))
			{
				state = new PlayerState();
				serverPlayers.Add(steamID, state);
			}

			return state;
		}

		internal static void ReportServerStat(Player player, EPlayerStat stat)
		{
			if (!Provider.isServer || !IsSurvivalLevel || player?.life == null || player.life.isDead || player.channel?.owner == null)
			{
				return;
			}

			PlayerState state = GetServerState(player.channel.owner.playerID.steamID);
			if (stat == EPlayerStat.FOUND_RESOURCES)
			{
				state.salvage = Mathf.Min(state.salvage + 1, 99);
			}

			if (stat == EPlayerStat.FOUND_ITEMS && state.medicalStage == 0)
			{
				AdvanceMedicalResearch(state);
			}
			else if (stat == EPlayerStat.FOUND_CRAFTS && state.medicalStage == 1)
			{
				AdvanceMedicalResearch(state);
			}
		}

		internal static void ReportLocalStat(Player player, EPlayerStat stat)
		{
			if (Dedicator.IsDedicatedServer || !IsSurvivalLevel || player?.channel == null || !player.channel.IsLocalPlayer)
			{
				return;
			}

			if (stat == EPlayerStat.FOUND_RESOURCES)
			{
				localSalvage = Mathf.Min(localSalvage + 1, 99);
			}

			if (stat == EPlayerStat.FOUND_ITEMS && localMedicalStage == 0)
			{
				AdvanceLocalMedicalResearch();
			}
			else if (stat == EPlayerStat.FOUND_CRAFTS && localMedicalStage == 1)
			{
				AdvanceLocalMedicalResearch();
			}

			if (stat == EPlayerStat.KILLS_ZOMBIES_NORMAL || stat == EPlayerStat.KILLS_ZOMBIES_MEGA)
			{
				ItemAsset equippedAsset = player.equipment?.asset;
				if (equippedAsset != null && equippedAsset.type == EItemType.GUN)
				{
					localWeaponKills[(int) EMasteryFamily.Ranged]++;
				}
				else if (equippedAsset != null && equippedAsset.type == EItemType.MELEE)
				{
					localWeaponKills[(int) EMasteryFamily.Melee]++;
				}
			}
		}

		private static void AdvanceMedicalResearch(PlayerState state)
		{
			int target = state.medicalStage == 0 ? 3 : (state.medicalStage == 1 ? 2 : 1);
			++state.medicalCount;
			if (state.medicalCount < target)
			{
				return;
			}

			state.medicalCount = 0;
			++state.medicalStage;
			if (state.medicalStage >= 2)
			{
				// Stage two waits for a disinfectant-bearing medical item, not another stat.
				state.medicalStage = 2;
			}
		}

		private static void AdvanceLocalMedicalResearch()
		{
			int target = localMedicalStage == 0 ? 3 : 2;
			++localMedicalCount;
			if (localMedicalCount >= target)
			{
				localMedicalCount = 0;
				++localMedicalStage;
			}
		}

		internal static void ReportServerMedicalUse(Player player, ItemConsumeableAsset asset)
		{
			if (!Provider.isServer || !IsSurvivalLevel || player?.life == null || player.life.isDead || player.channel?.owner == null || asset == null || asset.disinfectant == 0)
			{
				return;
			}

			PlayerState state = GetServerState(player.channel.owner.playerID.steamID);
			if (state.medicalStage != 2)
			{
				return;
			}

			state.medicalStage = 0;
			state.medicalCount = 0;
			++state.medicalResearchRank;
			player.life.serverModifyVirus(8f);
			player.skills?.ServerModifyExperience(90);
			ChatManager.serverSendMessage("Research complete: your immunity protocol improved.", new Color(0.45f, 0.9f, 0.65f), toPlayer: player.channel.owner);
		}

		internal static void ReportLocalMedicalUse(Player player, ItemConsumeableAsset asset)
		{
			if (Dedicator.IsDedicatedServer || !IsSurvivalLevel || player?.channel == null || !player.channel.IsLocalPlayer || asset == null || asset.disinfectant == 0 || localMedicalStage != 2)
			{
				return;
			}

			localMedicalStage = 0;
			localMedicalCount = 0;
			++localMedicalResearchRank;
			PlayerUI.message(EPlayerMessage.EXPERIENCE, "Research complete: your immunity protocol improved.", 4f);
		}

		internal static void ReportServerWeaponKill(Player player, ItemAsset weapon)
		{
			if (!Provider.isServer || !IsSurvivalLevel || player?.channel?.owner == null || weapon == null)
			{
				return;
			}

			int family = weapon.type == EItemType.GUN ? (int) EMasteryFamily.Ranged : (weapon.type == EItemType.MELEE ? (int) EMasteryFamily.Melee : -1);
			if (family < 0)
			{
				return;
			}

			PlayerState state = GetServerState(player.channel.owner.playerID.steamID);
			++state.weaponKills[family];
			if (state.weaponKills[family] == MasteryUnlockKills)
			{
				ChatManager.serverSendMessage("Weapon mastery unlocked. Open the activity board and choose a sidegrade.", new Color(0.55f, 0.8f, 1f), toPlayer: player.channel.owner);
			}
		}

		internal static void TryChooseWeaponMastery(Player player, byte family, byte choice)
		{
			if (!Provider.isServer || !IsSurvivalLevel || family > 1 || choice < 1 || choice > 2 || player?.channel?.owner == null)
			{
				return;
			}

			PlayerState state = GetServerState(player.channel.owner.playerID.steamID);
			if (state.weaponKills[family] < MasteryUnlockKills || state.weaponChoices[family] != 0)
			{
				return;
			}

			state.weaponChoices[family] = choice;
			if (player.channel.IsLocalPlayer)
			{
				SetLocalMasteryState(family, choice);
			}
			else
			{
				player.SendSurvivalMasteryState(family, choice);
			}
		}

		internal static void SetLocalMasteryState(byte family, byte choice)
		{
			if (family < localWeaponChoices.Length)
			{
				localWeaponChoices[family] = choice;
			}
		}

		internal static void TryPurchaseSafehouseUpgrade(Player player, byte upgrade)
		{
			if (!Provider.isServer || !IsSurvivalLevel || upgrade >= 4 || player?.channel?.owner == null || player.life == null || player.life.isDead)
			{
				return;
			}

			InteractableBed bed = FindNearestOwnedBed(player, 10f);
			if (bed == null)
			{
				return;
			}

			CSteamID steamID = player.channel.owner.playerID.steamID;
			PlayerState state = GetServerState(steamID);
			if (state.salvage < SafehouseUpgradeCost || state.safehouseUpgrades[upgrade] != 0)
			{
				return;
			}

			state.salvage -= SafehouseUpgradeCost;
			state.safehouseUpgrades[upgrade] = 1;
			if (player.channel.IsLocalPlayer)
			{
				SetLocalSafehouseState(upgrade, 1, state.salvage);
			}
			else
			{
				player.SendSafehouseUpgradeState(upgrade, 1, (byte) state.salvage);
			}
		}

		internal static void SetLocalSafehouseState(byte upgrade, byte level, int salvage)
		{
			if (upgrade < localSafehouseUpgrades.Length)
			{
				localSafehouseUpgrades[upgrade] = level;
				localSalvage = salvage;
			}
		}

		private static InteractableBed FindNearestOwnedBed(Player player, float radius)
		{
			RefreshBeds();
			if (player?.channel?.owner == null)
			{
				return null;
			}

			CSteamID steamID = player.channel.owner.playerID.steamID;
			float bestSqrDistance = radius * radius;
			InteractableBed best = null;
			for (int index = 0; index < bedCache.Length; ++index)
			{
				InteractableBed bed = bedCache[index];
				if (bed == null || !bed.isClaimed || bed.owner != steamID)
				{
					continue;
				}

				float sqrDistance = (bed.transform.position - player.transform.position).sqrMagnitude;
				if (sqrDistance < bestSqrDistance)
				{
					bestSqrDistance = sqrDistance;
					best = bed;
				}
			}

			return best;
		}

		private static void RefreshBeds()
		{
			if (Time.unscaledTime < nextBedScan)
			{
				return;
			}

			nextBedScan = Time.unscaledTime + 5f;
			bedCache = Object.FindObjectsOfType<InteractableBed>();
		}

		private static bool IsZombieDamageOrigin(EDamageOrigin origin)
		{
			switch (origin)
			{
				case EDamageOrigin.Mega_Zombie_Boulder:
				case EDamageOrigin.Zombie_Swipe:
				case EDamageOrigin.Radioactive_Zombie_Explosion:
				case EDamageOrigin.Flamable_Zombie_Explosion:
				case EDamageOrigin.Zombie_Electric_Shock:
				case EDamageOrigin.Zombie_Stomp:
				case EDamageOrigin.Zombie_Fire_Breath:
					return true;
				default:
					return false;
			}
		}

		internal static float GetSafehouseDamageMultiplier(Vector3 position, EDamageOrigin origin)
		{
			if (!Provider.isServer || !IsSurvivalLevel || !IsZombieDamageOrigin(origin))
			{
				return 1f;
			}

			RefreshBeds();
			for (int index = 0; index < bedCache.Length; ++index)
			{
				InteractableBed bed = bedCache[index];
				if (bed == null || !bed.isClaimed)
				{
					continue;
				}

				if ((bed.transform.position - position).sqrMagnitude > 18f * 18f)
				{
					continue;
				}

				if (serverPlayers.TryGetValue(bed.owner, out PlayerState state) && state.safehouseUpgrades[(int) ESafehouseUpgrade.ReinforcedGate] > 0)
				{
					return 0.65f; // Durable, not invulnerable: zombies can still break the buildable.
				}
			}

			return 1f;
		}

		internal static bool IsHotspot(byte nav)
		{
			if (!Level.isLoaded || Level.info == null || Level.info.type != ELevelType.SURVIVAL)
			{
				return false;
			}

			EnsureHotspotDay();
			if (clearedHotspots.Contains(nav))
			{
				return false;
			}

			BuildValidHotspotRegions();
			if (validHotspotRegions.Count == 0)
			{
				return false;
			}

			int first = PositiveModulo(LightingManager.DateCounter, validHotspotRegions.Count);
			if (validHotspotRegions[first] == nav)
			{
				return true;
			}

			if (validHotspotRegions.Count > 1)
			{
				int second = (first + Mathf.Max(1, validHotspotRegions.Count / 2)) % validHotspotRegions.Count;
				return validHotspotRegions[second] == nav;
			}

			return false;
		}

		internal static EZombieSpeciality MutateSpeciality(byte nav, EZombieSpeciality current)
		{
			if (!IsHotspot(nav) || (current != EZombieSpeciality.NORMAL && current != EZombieSpeciality.NONE) || Random.value > 0.38f)
			{
				return current;
			}

			long cycle = LightingManager.DateCounter % 3;
			if (cycle == 0)
			{
				return EZombieSpeciality.SPRINTER;
			}
			if (cycle == 1)
			{
				return EZombieSpeciality.CRAWLER;
			}

			return Random.value < 0.5f ? EZombieSpeciality.SPRINTER : EZombieSpeciality.CRAWLER;
		}

		private static void BuildValidHotspotRegions()
		{
			if (validHotspotRegions.Count > 0 || LevelZombies.zombies == null)
			{
				return;
			}

			for (int index = 0; index < LevelZombies.zombies.Length && index <= byte.MaxValue; ++index)
			{
				if (LevelZombies.zombies[index] != null && LevelZombies.zombies[index].Count > 0)
				{
					validHotspotRegions.Add((byte) index);
				}
			}
		}

		private static void EnsureHotspotDay()
		{
			if (hotspotDate == LightingManager.DateCounter)
			{
				return;
			}

			hotspotDate = LightingManager.DateCounter;
			hotspotKills.Clear();
			clearedHotspots.Clear();
		}

		internal static void ReportServerZombieKill(Player player, Zombie zombie)
		{
			if (!Provider.isServer || !IsSurvivalLevel || zombie == null)
			{
				return;
			}

			if (IsHotspot(zombie.bound))
			{
				hotspotKills.TryGetValue(zombie.bound, out int count);
				count++;
				hotspotKills[zombie.bound] = count;
				if (count >= 16 && clearedHotspots.Add(zombie.bound))
				{
					BroadcastToRegion(zombie.bound, "Infestation cleared. Mutant spawns and horde pressure will ease until tomorrow.", new Color(0.65f, 1f, 0.65f));
				}
			}

			if (player?.channel?.owner == null || player.equipment == null)
			{
				return;
			}

			ReportServerWeaponKill(player, player.equipment.asset);
		}

		internal static void ReportLocalZombieDeath(Zombie zombie)
		{
			if (Dedicator.IsDedicatedServer || zombie == null)
			{
				return;
			}

			if (localHotspotDate != LightingManager.DateCounter)
			{
				localHotspotDate = LightingManager.DateCounter;
				localHotspotProgress.Clear();
			}

			if (!IsHotspot(zombie.bound) || clearedHotspots.Contains(zombie.bound))
			{
				return;
			}

			localHotspotProgress.TryGetValue(zombie.bound, out int count);
			count++;
			localHotspotProgress[zombie.bound] = count;
			if (count == 16)
			{
				clearedHotspots.Add(zombie.bound);
				PlayerUI.message(EPlayerMessage.EXPERIENCE, "Sector cleared. Mutations have moved on.", 4f);
			}
		}

		internal static void RecordNoise(Vector3 position, float radius)
		{
			if (!Provider.isServer || !IsSurvivalLevel || radius < 4f || ZombieManager.regions == null)
			{
				return;
			}

			if (!LevelNavigation.tryGetNavigation(position, out byte nav) || nav >= ZombieManager.regions.Length)
			{
				return;
			}

			if (!noiseByRegion.TryGetValue(nav, out NoiseCell cell))
			{
				cell = new NoiseCell { lastUpdate = Time.time };
				noiseByRegion.Add(nav, cell);
			}

			float elapsed = Mathf.Max(0f, Time.time - cell.lastUpdate);
			cell.intensity = Mathf.Max(0f, cell.intensity - elapsed * 1.8f);
			cell.lastUpdate = Time.time;
			cell.lastPosition = position;
			float threatMultiplier = clearedHotspots.Contains(nav) ? 0.55f : 1f;
			cell.intensity += Mathf.Clamp(radius, 4f, 80f) * 0.85f * threatMultiplier;

			if (cell.intensity >= 150f && Time.time >= cell.cooldownUntil && !cell.pendingHorde)
			{
				cell.intensity = 0f;
				cell.cooldownUntil = Time.time + 75f;
				cell.pendingHorde = true;
				cell.pendingHordeAt = Time.time + 10f;
				BroadcastToRegion(nav, "Horde warning: repeated noise has drawn infected toward this sector. You have 10 seconds to hide, flee, or defend.", new Color(1f, 0.65f, 0.25f));
			}
		}

		private static void TriggerHorde(byte nav, Vector3 position)
		{
			List<Zombie> zombies = new List<Zombie>();
			ZombieManager.getZombiesInRadius(position, 95f * 95f, zombies);
			int alerted = 0;
			for (int index = 0; index < zombies.Count && alerted < 24; ++index)
			{
				Zombie zombie = zombies[index];
				if (zombie == null || zombie.isDead)
				{
					continue;
				}

				zombie.alert(position, true);
				++alerted;
			}

			BroadcastToRegion(nav, "The horde is converging on the last loud-noise location.", new Color(1f, 0.45f, 0.2f));
		}

		private static void BroadcastToRegion(byte nav, string text, Color color)
		{
			if (!Provider.isServer || Provider.clients == null)
			{
				return;
			}

			for (int index = 0; index < Provider.clients.Count; ++index)
			{
				SteamPlayer client = Provider.clients[index];
				if (client?.player == null || client.player.life == null || client.player.life.isDead || client.player.movement.nav != nav)
				{
					continue;
				}

				ChatManager.serverSendMessage(text, color, toPlayer: client);
			}
		}

		internal static float GetWeaponNoiseMultiplier(Player player)
		{
			ItemAsset asset = player?.equipment?.asset;
			if (asset == null)
			{
				return 1f;
			}

			return GetWeaponChoice(player, asset.type) == 1 ? 0.7f : 1f;
		}

		internal static float GetWeaponWearChanceMultiplier(Player player)
		{
			ItemAsset asset = player?.equipment?.asset;
			if (asset == null || GetWeaponChoice(player, asset.type) != 2)
			{
				return 1f;
			}

			return 0.7f;
		}

		internal static float GetMeleeStaminaMultiplier(Player player)
		{
			return GetWeaponChoice(player, EItemType.MELEE) == 2 ? 0.82f : 1f;
		}

		private static byte GetWeaponChoice(Player player, EItemType type)
		{
			if (player?.channel?.owner == null)
			{
				return 0;
			}

			int family = type == EItemType.GUN ? 0 : (type == EItemType.MELEE ? 1 : -1);
			if (family < 0)
			{
				return 0;
			}

			if (Provider.isServer)
			{
				return GetServerState(player.channel.owner.playerID.steamID).weaponChoices[family];
			}

			return localWeaponChoices[family];
		}

		private static void TickPersistentNoise()
		{
			if (Time.time < nextPersistentNoiseScan)
			{
				return;
			}

			nextPersistentNoiseScan = Time.time + 8f;
			InteractableGenerator[] generators = Object.FindObjectsOfType<InteractableGenerator>();
			for (int index = 0; index < generators.Length; ++index)
			{
				InteractableGenerator generator = generators[index];
				if (generator != null && generator.isPowered && generator.fuel > 0)
				{
					RecordNoise(generator.transform.position, 50f);
				}
			}

			InteractableVehicle[] vehicles = Object.FindObjectsOfType<InteractableVehicle>();
			for (int index = 0; index < vehicles.Length; ++index)
			{
				InteractableVehicle vehicle = vehicles[index];
				if (vehicle != null && vehicle.isEngineOn)
				{
					RecordNoise(vehicle.transform.position, 40f);
				}
			}
		}

		internal static void TickServer()
		{
			if (!Provider.isServer || !Level.isLoaded || Level.info == null || Level.info.type != ELevelType.SURVIVAL || Time.unscaledTime < nextServerTick)
			{
				return;
			}

			nextServerTick = Time.unscaledTime + 2f;
			EnsureHotspotDay();
			foreach (KeyValuePair<byte, NoiseCell> entry in noiseByRegion)
			{
				NoiseCell cell = entry.Value;
				float elapsed = Mathf.Max(0f, Time.time - cell.lastUpdate);
				cell.intensity = Mathf.Max(0f, cell.intensity - elapsed * 1.8f);
				cell.lastUpdate = Time.time;
				if (cell.pendingHorde && Time.time >= cell.pendingHordeAt)
				{
					cell.pendingHorde = false;
					TriggerHorde(entry.Key, cell.lastPosition);
				}
			}

			TickPersistentNoise();
			if (Provider.clients == null)
			{
				return;
			}

			for (int index = 0; index < Provider.clients.Count; ++index)
			{
				SteamPlayer client = Provider.clients[index];
				Player player = client?.player;
				if (player?.life == null || player.life.isDead || player.channel?.owner == null)
				{
					continue;
				}

				InteractableBed bed = FindNearestOwnedBed(player, 18f);
				if (bed == null)
				{
					continue;
				}

				PlayerState state = GetServerState(player.channel.owner.playerID.steamID);
				if (state.safehouseUpgrades[(int) ESafehouseUpgrade.Purifier] > 0 && Time.time >= state.nextPurifierTick)
				{
					state.nextPurifierTick = Time.time + 55f;
					if (player.life.water < 90)
					{
						player.life.serverModifyWater(3f);
						RecordNoise(bed.transform.position, 14f);
					}
				}

				if (state.safehouseUpgrades[(int) ESafehouseUpgrade.MedicalStation] > 0 && Time.time >= state.nextMedicalTick)
				{
					state.nextMedicalTick = Time.time + 60f;
					player.life.serverModifyVirus(4f);
				}

				if (state.safehouseUpgrades[(int) ESafehouseUpgrade.Floodlights] > 0 && Time.time >= state.nextFloodlightNoise)
				{
					state.nextFloodlightNoise = Time.time + 45f;
					RecordNoise(bed.transform.position, 22f);
				}
			}
		}

		internal static bool IsPlayerInHotspot(Player player)
		{
			return player?.movement != null && IsHotspot(player.movement.nav);
		}

		private static int PositiveModulo(long value, int modulus)
		{
			if (modulus <= 0)
			{
				return 0;
			}

			int result = (int) (value % modulus);
			return result < 0 ? result + modulus : result;
		}

		internal static void ResetForLevel()
		{
			// Keep player progression and choices for the server session; only world-bound caches reset.
			noiseByRegion.Clear();
			hotspotKills.Clear();
			clearedHotspots.Clear();
			validHotspotRegions.Clear();
			bedCache = new InteractableBed[0];
			rescueAnchorCache = new SurvivalRescueAnchor[0];
			nextBedScan = 0f;
			nextRescueScan = 0f;
			nextPersistentNoiseScan = 0f;
			nextLocalSafehouseScan = 0f;
			localHasSafehouseNearby = false;
			localSafehouseCheckOwner = CSteamID.Nil;
			hotspotDate = long.MinValue;
			rescueAnnouncementDate = long.MinValue;
			localHotspotDate = long.MinValue;
			localHotspotProgress.Clear();
		}

		private static void RefreshRescueAnchors()
		{
			if (Time.unscaledTime < nextRescueScan)
			{
				return;
			}

			nextRescueScan = Time.unscaledTime + 10f;
			rescueAnchorCache = Object.FindObjectsOfType<SurvivalRescueAnchor>();
			System.Array.Sort(rescueAnchorCache, (left, right) =>
			{
				if (left == null) return right == null ? 0 : 1;
				if (right == null) return -1;
				int xCompare = left.transform.position.x.CompareTo(right.transform.position.x);
				if (xCompare != 0) return xCompare;
				int zCompare = left.transform.position.z.CompareTo(right.transform.position.z);
				if (zCompare != 0) return zCompare;
				int yCompare = left.transform.position.y.CompareTo(right.transform.position.y);
				if (yCompare != 0) return yCompare;
				int callSignCompare = string.CompareOrdinal(left.CallSign, right.CallSign);
				return callSignCompare != 0 ? callSignCompare : string.CompareOrdinal(left.gameObject.name, right.gameObject.name);
			});
		}

		internal static void AnnounceRescueCall(SurvivalRescueAnchor anchor)
		{
			if (!Provider.isServer || anchor == null || rescueAnnouncementDate == LightingManager.DateCounter)
			{
				return;
			}

			rescueAnnouncementDate = LightingManager.DateCounter;
			string message = $"Radio rescue call: {anchor.CallSign} at {anchor.transform.position.x:0}, {anchor.transform.position.z:0}. Reach the survivor and defend the pickup.";
			ChatManager.serverSendMessage(message, new Color(0.6f, 0.85f, 1f));
		}

		internal static void SpawnRescueWave(Vector3 center, int wave)
		{
			if (!Provider.isServer || !IsSurvivalLevel || !LevelNavigation.tryGetNavigation(center, out byte bound) || ZombieManager.regions == null || bound >= ZombieManager.regions.Length)
			{
				return;
			}

			if (LevelZombies.zombies == null || bound >= LevelZombies.zombies.Length || LevelZombies.zombies[bound] == null)
			{
				return;
			}

			List<ZombieSpawnpoint> spawnpoints = LevelZombies.zombies[bound];
			if (spawnpoints.Count == 0)
			{
				return;
			}

			ZombieRegion region = ZombieManager.regions[bound];
			int spawned = 0;
			int desired = Mathf.Clamp(2 + wave, 2, 5);
			for (int zombieIndex = 0; zombieIndex < region.zombies.Count && spawned < desired; ++zombieIndex)
			{
				Zombie zombie = region.zombies[zombieIndex];
				if (zombie == null || !zombie.isDead || Time.realtimeSinceStartup - zombie.lastDead < 12f)
				{
					continue;
				}

				for (int pointIndex = 0; pointIndex < spawnpoints.Count; ++pointIndex)
				{
					ZombieSpawnpoint spawn = spawnpoints[(pointIndex + zombieIndex + wave) % spawnpoints.Count];
					float distanceSqr = (spawn.point - center).sqrMagnitude;
					if (distanceSqr < 14f * 14f || distanceSqr > 65f * 65f || !SafezoneManager.checkPointValid(spawn.point))
					{
						continue;
					}

					EZombieSpeciality speciality = wave == 2 ? EZombieSpeciality.SPRINTER : EZombieSpeciality.NORMAL;
					Vector3 position = spawn.point + Vector3.up * 0.5f;
					zombie.sendRevive(spawn.type, (byte) speciality, zombie.shirt, zombie.pants, zombie.hat, zombie.gear, position, Random.Range(0f, 360f));
					++spawned;
					break;
				}
			}

			// If the map pool has no eligible dead entries, use nearby living infected as a lighter wave.
			if (spawned < desired)
			{
				List<Zombie> nearby = new List<Zombie>();
				ZombieManager.getZombiesInRadius(center, 75f * 75f, nearby);
				for (int index = 0; index < nearby.Count && spawned < desired; ++index)
				{
					Zombie zombie = nearby[index];
					if (zombie != null && !zombie.isDead)
					{
						zombie.alert(center, true);
						++spawned;
					}
				}
			}
		}
	}
}
