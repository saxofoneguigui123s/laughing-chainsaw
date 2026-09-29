////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using Steamworks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SDG.Unturned
{
	/// <summary>
	/// Optional map component placed on a survivor NPC or rescue location. Each map may
	/// reuse its own NPC prefab and evacuation point; no map name or item GUID is assumed.
	/// If Survivor and EvacuationPoint are assigned, the NPC is escorted before extraction defense.
	/// </summary>
	public sealed class SurvivalRescueAnchor : MonoBehaviour
	{
		[Tooltip("Short radio call sign displayed to players.")]
		public string CallSign = "Distress Signal";

		[Tooltip("Optional NPC transform. Assign a networked survivor with a NavMeshAgent for escort movement.")]
		public Transform Survivor;

		[Tooltip("Safe evacuation destination. Required for the escort leg.")]
		public Transform EvacuationPoint;

		[Min(5f)]
		public float ActivationRadius = 18f;

		private readonly HashSet<CSteamID> participants = new HashSet<CSteamID>();
		private long eventDate = long.MinValue;
		private bool eventStarted;
		private bool eventComplete;
		private bool escortStarted;
		private int wavesSent;
		private float nextWaveTime;
		private NavMeshAgent survivorAgent;

		public bool IsEventActive => eventStarted && !eventComplete;
		public bool IsEscortActive => escortStarted && !eventComplete;
		public string PhaseText
		{
			get
			{
				if (eventComplete) return "Rescue complete";
				if (escortStarted) return "Escort survivor to evacuation point";
				if (eventStarted) return $"Defend extraction — wave {Mathf.Min(wavesSent + 1, 3)} of 3";
				return "Radio call pending";
			}
		}

		private void Update()
		{
			if (!Provider.isServer || !Level.isLoaded || !SurvivalExpansion.IsSelectedRescueAnchor(this))
			{
				return;
			}

			if (eventDate != LightingManager.DateCounter)
			{
				ResetEventForToday();
			}

			SurvivalExpansion.AnnounceRescueCall(this);
			if (Provider.clients == null)
			{
				return;
			}

			float now = Time.time;
			bool anyoneAtCall = false;
			for (int index = 0; index < Provider.clients.Count; ++index)
			{
				SteamPlayer client = Provider.clients[index];
				Player player = client?.player;
				if (player?.life == null || player.life.isDead || player.channel?.owner == null)
				{
					continue;
				}

				float anchorDistance = Vector3.Distance(player.transform.position, transform.position);
				if (anchorDistance <= ActivationRadius)
				{
					anyoneAtCall = true;
					participants.Add(client.playerID.steamID);
				}
				else if (escortStarted && Survivor != null && Vector3.Distance(player.transform.position, Survivor.position) <= 24f)
				{
					participants.Add(client.playerID.steamID);
				}
			}

			if (!eventStarted && anyoneAtCall)
			{
				eventStarted = true;
				BeginEscortOrStartDefense();
			}

			if (!eventStarted || eventComplete)
			{
				return;
			}

			if (escortStarted)
			{
				TickEscort();
				return;
			}

			if (wavesSent < 3 && now >= nextWaveTime)
			{
				Vector3 wavePosition = EvacuationPoint != null && Survivor != null ? EvacuationPoint.position : transform.position;
				SurvivalExpansion.SpawnRescueWave(wavePosition, wavesSent);
				++wavesSent;
				nextWaveTime = now + 12f;
				Broadcast($"Extraction defense: wave {wavesSent} of 3.");
			}

			if (wavesSent >= 3 && now >= nextWaveTime)
			{
				CompleteRescue();
			}
		}

		private void ResetEventForToday()
		{
			eventDate = LightingManager.DateCounter;
			eventStarted = false;
			eventComplete = false;
			escortStarted = false;
			wavesSent = 0;
			participants.Clear();
			nextWaveTime = 0f;
			survivorAgent = null;
		}

		private void BeginEscortOrStartDefense()
		{
			if (Survivor != null && EvacuationPoint != null)
			{
				survivorAgent = Survivor.GetComponent<NavMeshAgent>();
				if (survivorAgent != null && survivorAgent.enabled && survivorAgent.isOnNavMesh && survivorAgent.SetDestination(EvacuationPoint.position))
				{
					escortStarted = true;
					survivorAgent.isStopped = false;
					nextWaveTime = Time.time + 8f;
					Broadcast("Survivor located. Escort them to the marked evacuation point; stay within 24 m.");
					return;
				}

				Broadcast("The survivor has no usable NavMesh path. This call will use the radio-beacon extraction variant.");
			}
			else
			{
				Broadcast("Rescue beacon located. No survivor/evacuation transforms are assigned; defend this pickup point.");
			}

			StartDefense();
		}

		private void StartDefense()
		{
			escortStarted = false;
			wavesSent = 0;
			nextWaveTime = Time.time + 4f;
			Broadcast("Extraction is live. Defend the survivor through three short waves.");
		}

		private void TickEscort()
		{
			if (survivorAgent == null || !survivorAgent.enabled || EvacuationPoint == null)
			{
				StartDefense();
				return;
			}

			bool escorted = false;
			for (int index = 0; index < Provider.clients.Count; ++index)
			{
				SteamPlayer client = Provider.clients[index];
				if (client?.player == null || !participants.Contains(client.playerID.steamID) || client.player.life == null || client.player.life.isDead)
				{
					continue;
				}

				if (Vector3.Distance(client.player.transform.position, survivorAgent.transform.position) <= 24f)
				{
					escorted = true;
					break;
				}
			}

			survivorAgent.isStopped = !escorted;
			if (!escorted && Time.time >= nextWaveTime)
			{
				nextWaveTime = Time.time + 8f;
				Broadcast("The survivor has stopped. Stay within 24 m to escort them.");
			}

			if (!survivorAgent.pathPending && survivorAgent.remainingDistance <= Mathf.Max(2f, survivorAgent.stoppingDistance + 0.5f))
			{
				escortStarted = false;
				Broadcast("Survivor reached extraction. Defend the pickup through three short waves.");
				StartDefense();
			}
		}

		private void CompleteRescue()
		{
			if (eventComplete)
			{
				return;
			}

			eventComplete = true;
			if (Provider.clients != null)
			{
				for (int index = 0; index < Provider.clients.Count; ++index)
				{
					SteamPlayer client = Provider.clients[index];
					Player player = client?.player;
					if (player?.life == null || player.life.isDead || !participants.Contains(client.playerID.steamID))
					{
						continue;
					}

					Vector3 rewardPoint = EvacuationPoint != null ? EvacuationPoint.position : transform.position;
					float distance = Vector3.Distance(player.transform.position, rewardPoint);
					if (distance > 45f)
					{
						continue;
					}

					player.skills.ServerModifyExperience(120);
					player.life.serverModifyVirus(4f);
					ChatManager.serverSendMessage("Rescue complete: +120 experience and immunity support.", new Color(0.55f, 1f, 0.7f), toPlayer: client);
				}
			}

			Broadcast("Rescue complete. The radio signal will return on a later cycle.");
		}

		private void Broadcast(string message)
		{
			if (!Provider.isServer || Provider.clients == null)
			{
				return;
			}

			for (int index = 0; index < Provider.clients.Count; ++index)
			{
				SteamPlayer client = Provider.clients[index];
				if (client?.player == null || Vector3.Distance(client.player.transform.position, transform.position) > 500f)
				{
					continue;
				}

				ChatManager.serverSendMessage(message, new Color(0.6f, 0.85f, 1f), toPlayer: client);
			}
		}
	}
}
