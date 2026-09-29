////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned
{
	/// <summary>Bootstraps the server-side survival systems without scene-specific objects.</summary>
	public sealed class SurvivalExpansionRuntime : MonoBehaviour
	{
		private static SurvivalExpansionRuntime instance;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Install()
		{
			if (instance != null)
			{
				return;
			}

			GameObject runtimeObject = new GameObject("Survival Expansion Runtime");
			DontDestroyOnLoad(runtimeObject);
			instance = runtimeObject.AddComponent<SurvivalExpansionRuntime>();
		}

		private void Awake()
		{
			if (instance != null && instance != this)
			{
				Destroy(gameObject);
				return;
			}

			instance = this;
			Level.onLevelLoaded += OnLevelLoaded;
		}

		private void OnLevelLoaded(int level)
		{
			SurvivalExpansion.ResetForLevel();
		}

		private void Update()
		{
			SurvivalExpansion.TickServer();
		}

		private void OnDestroy()
		{
			Level.onLevelLoaded -= OnLevelLoaded;
			if (instance == this)
			{
				instance = null;
			}
		}
	}
}
