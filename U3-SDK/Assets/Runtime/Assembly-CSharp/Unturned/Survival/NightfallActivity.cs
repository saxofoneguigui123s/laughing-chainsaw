////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using Steamworks;
using System.Collections.Generic;
using UnityEngine;

namespace SDG.Unturned
{
	/// <summary>
	/// Small vanilla-plus activity loop: rotating daytime contracts, night bounties,
	/// a full-moon event, and a survival-streak reward. Progress/rewards are server-side.
	/// The local HUD only presents the same activity to the player.
	/// </summary>
	internal static class NightfallActivity
	{
		private sealed class PlayerProgress
		{
			public int kills;
			public int completedBounties;
			public int nightsSurvived;
			public bool wasAliveAtDusk;
		}

		private sealed class DayContractProgress
		{
			public long date;
			public int count;
			public bool completed;
		}

		private struct DayContract
		{
			public EPlayerStat stat;
			public int target;
			public int reward;
			public string title;
		}

		private static readonly Dictionary<CSteamID, PlayerProgress> serverProgress = new Dictionary<CSteamID, PlayerProgress>();
		private static readonly Dictionary<CSteamID, DayContractProgress> serverDayProgress = new Dictionary<CSteamID, DayContractProgress>();
		private static bool hasDayNightState;
		private static bool wasDaytime;

		private static int localKills;
		private static int localCompletedBounties;
		private static int localNightsSurvived;
		private static bool localWasAliveAtDusk;
		private static long localDayProgressDate = long.MinValue;
		private static int localDayContractCount;
		private static bool localDayContractCompleted;

		internal static int LocalKills => localKills;
		internal static int LocalCompletedBounties => localCompletedBounties;
		internal static int LocalNightsSurvived => localNightsSurvived;
		internal static int CurrentTarget => LightingManager.isFullMoon ? 8 : 5;
		internal static bool IsFullMoon => LightingManager.isFullMoon;
		internal static int LocalDayContractCount { get { EnsureLocalDayContract(); return localDayContractCount; } }
		internal static bool LocalDayContractCompleted { get { EnsureLocalDayContract(); return localDayContractCompleted; } }
		internal static int DayContractTarget => GetDayContract().target;
		internal static int DayContractReward => GetDayContract().reward;
		internal static string DayContractTitle => GetDayContract().title;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Install()
		{
			Player.onPlayerCreated += OnPlayerCreated;
			Level.onLevelLoaded += OnLevelLoaded;
		}

		private static void OnLevelLoaded(int level)
		{
			if (level <= Level.BUILD_INDEX_SETUP)
			{
				return;
			}

			serverProgress.Clear();
			serverDayProgress.Clear();
			localKills = 0;
			localCompletedBounties = 0;
			localNightsSurvived = 0;
			localWasAliveAtDusk = false;
			localDayProgressDate = long.MinValue;
			localDayContractCount = 0;
			localDayContractCompleted = false;

			// LightingManager clears its static delegates immediately before loading a
			// level, so subscribe after that reset, once the level is ready.
			LightingManager.onDayNightUpdated -= OnDayNightUpdated;
			LightingManager.onDayNightUpdated += OnDayNightUpdated;
			hasDayNightState = false;
			OnDayNightUpdated(LightingManager.isDaytime);
		}

		private static void OnPlayerCreated(Player player)
		{
			if (player == null)
			{
				return;
			}

			if (!Dedicator.IsDedicatedServer && player.GetComponent<NightfallActivityHUD>() == null)
			{
				NightfallActivityHUD hud = player.gameObject.AddComponent<NightfallActivityHUD>();
				hud.Initialize(player);
			}

			if (LightingManager.isNighttime && player.life != null && !player.life.isDead)
			{
				if (Provider.isServer && player.channel?.owner != null)
				{
					GetOrCreateProgress(player.channel.owner.playerID.steamID).wasAliveAtDusk = true;
				}

				if (!Dedicator.IsDedicatedServer && player.channel != null && player.channel.IsLocalPlayer)
				{
					localWasAliveAtDusk = true;
				}
			}
		}

		private static PlayerProgress GetOrCreateProgress(CSteamID steamID)
		{
			if (!serverProgress.TryGetValue(steamID, out PlayerProgress progress))
			{
				progress = new PlayerProgress();
				serverProgress.Add(steamID, progress);
			}

			return progress;
		}

		private static void OnDayNightUpdated(bool isDaytime)
		{
			if (!hasDayNightState)
			{
				hasDayNightState = true;
				wasDaytime = isDaytime;
				if (!isDaytime)
				{
					BeginNight();
				}
				return;
			}

			if (wasDaytime == isDaytime)
			{
				return;
			}

			wasDaytime = isDaytime;
			if (isDaytime)
			{
				FinishNight();
			}
			else
			{
				BeginNight();
			}
		}

		private static void BeginNight()
		{
			localKills = 0;
			localCompletedBounties = 0;
			Player localPlayer = GetLocalPlayerSafely();
			localWasAliveAtDusk = localPlayer != null && localPlayer.life != null && !localPlayer.life.isDead;

			if (!Provider.isServer || Provider.clients == null)
			{
				return;
			}

			foreach (SteamPlayer client in Provider.clients)
			{
				Player player = client?.player;
				if (player?.life == null)
				{
					continue;
				}

				GetOrCreateProgress(client.playerID.steamID).wasAliveAtDusk = !player.life.isDead;
			}
		}

		private static void FinishNight()
		{
			localKills = 0;
			localCompletedBounties = 0;
			Player localPlayer = GetLocalPlayerSafely();
			if (localWasAliveAtDusk && localPlayer?.life != null && !localPlayer.life.isDead)
			{
				++localNightsSurvived;
			}
			localWasAliveAtDusk = false;

			if (!Provider.isServer || Provider.clients == null)
			{
				return;
			}

			foreach (SteamPlayer client in Provider.clients)
			{
				Player player = client?.player;
				if (player?.life == null)
				{
					continue;
				}

				PlayerProgress progress = GetOrCreateProgress(client.playerID.steamID);
				if (progress.wasAliveAtDusk && !player.life.isDead)
				{
					++progress.nightsSurvived;
					int reward = 35 + Mathf.Min(progress.nightsSurvived * 5, 50);
					player.skills?.ServerModifyExperience(reward);
				}

				progress.wasAliveAtDusk = false;
				progress.kills = 0;
				progress.completedBounties = 0;
			}
		}

		/// <summary>
		/// Called by the authoritative server for each player already credited by the
		/// game's nearby-zombie quest rules. Special and boss zombies count twice.
		/// </summary>
		internal static void ReportServerZombieKill(Player player, CSteamID steamID, Zombie zombie)
		{
			if (!Provider.isServer || !LightingManager.isNighttime || player?.life == null || player.life.isDead || zombie == null)
			{
				return;
			}

			PlayerProgress progress = GetOrCreateProgress(steamID);
			progress.wasAliveAtDusk = true;
			progress.kills += GetKillValue(zombie);

			int target = CurrentTarget;
			while (progress.kills >= target)
			{
				progress.kills -= target;
				++progress.completedBounties;
				int reward = (LightingManager.isFullMoon ? 120 : 60) + Mathf.Min((progress.completedBounties - 1) * 20, 100);
				player.skills?.ServerModifyExperience(reward);
			}
		}

		/// <summary>
		/// Client-side presentation only. The server's separate progress is the sole
		/// authority for XP rewards, so packet timing cannot mint extra experience.
		/// </summary>
		internal static void ReportLocalZombieKilled(Zombie zombie)
		{
			if (Dedicator.IsDedicatedServer || !LightingManager.isNighttime || zombie == null)
			{
				return;
			}

			Player player = GetLocalPlayerSafely();
			if (player?.life == null || player.life.isDead || (player.transform.position - zombie.transform.position).sqrMagnitude > 140f * 140f)
			{
				return;
			}

			localKills += GetKillValue(zombie);
			int target = CurrentTarget;
			while (localKills >= target)
			{
				localKills -= target;
				++localCompletedBounties;
				PlayerUI.message(EPlayerMessage.EXPERIENCE, "Night bounty complete! XP reward delivered.", 4.0f);
			}
		}

		/// <summary>
		/// Counts a gameplay stat on the server. The active contract is selected from
		/// the world's replicated, saved day counter, so every player shares the same
		/// rotation without adding a new network message.
		/// </summary>
		internal static void ReportServerDayStat(Player player, EPlayerStat stat)
		{
			if (!Provider.isServer || !LightingManager.isDaytime || player?.life == null || player.life.isDead || player.channel?.owner == null)
			{
				return;
			}

			DayContract contract = GetDayContract();
			if (stat != contract.stat)
			{
				return;
			}

			CSteamID steamID = player.channel.owner.playerID.steamID;
			if (!serverDayProgress.TryGetValue(steamID, out DayContractProgress progress))
			{
				progress = new DayContractProgress { date = LightingManager.DateCounter };
				serverDayProgress.Add(steamID, progress);
			}
			else if (progress.date != LightingManager.DateCounter)
			{
				progress.date = LightingManager.DateCounter;
				progress.count = 0;
				progress.completed = false;
			}

			if (progress.completed)
			{
				return;
			}

			++progress.count;
			if (progress.count >= contract.target)
			{
				progress.count = contract.target;
				progress.completed = true;
				player.skills?.ServerModifyExperience(contract.reward);
			}
		}

		/// <summary>
		/// Client-side board progress. The client receives the game's normal stat
		/// update; only ReportServerDayStat can grant the reward.
		/// </summary>
		internal static void ReportLocalDayStat(Player player, EPlayerStat stat)
		{
			if (Dedicator.IsDedicatedServer || !LightingManager.isDaytime || player?.channel == null || !player.channel.IsLocalPlayer)
			{
				return;
			}

			EnsureLocalDayContract();
			DayContract contract = GetDayContract();
			if (localDayContractCompleted || stat != contract.stat)
			{
				return;
			}

			++localDayContractCount;
			if (localDayContractCount >= contract.target)
			{
				localDayContractCount = contract.target;
				localDayContractCompleted = true;
				PlayerUI.message(EPlayerMessage.EXPERIENCE, "Daybreak contract complete! XP reward delivered.", 4.0f);
			}
		}

		private static void EnsureLocalDayContract()
		{
			if (localDayProgressDate == LightingManager.DateCounter)
			{
				return;
			}

			localDayProgressDate = LightingManager.DateCounter;
			localDayContractCount = 0;
			localDayContractCompleted = false;
		}

		private static DayContract GetDayContract()
		{
			int rotation = (int) (LightingManager.DateCounter % 3);
			if (rotation < 0)
			{
				rotation += 3;
			}

			switch (rotation)
			{
				case 0:
					return new DayContract { stat = EPlayerStat.FOUND_ITEMS, target = 4, reward = 70, title = "Scavenge" };
				case 1:
					return new DayContract { stat = EPlayerStat.FOUND_RESOURCES, target = 3, reward = 80, title = "Harvest" };
				default:
					return new DayContract { stat = EPlayerStat.FOUND_CRAFTS, target = 2, reward = 90, title = "Craft" };
			}
		}

		private static int GetKillValue(Zombie zombie)
		{
			return zombie.isBoss || (zombie.speciality != EZombieSpeciality.NONE && zombie.speciality != EZombieSpeciality.NORMAL) ? 2 : 1;
		}

		private static Player GetLocalPlayerSafely()
		{
			if (Dedicator.IsDedicatedServer)
			{
				return null;
			}

			return Player.LocalPlayer;
		}
	}

}
