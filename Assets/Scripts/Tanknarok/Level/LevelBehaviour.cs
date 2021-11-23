using UnityEngine;
using Fusion;
using FusionExamples.FusionHelpers;

namespace FusionExamples.Tanknarok
{
	public class LevelBehaviour : MonoBehaviour
	{
		// Class for storing the lighting settings of a level
		[System.Serializable]
		public struct LevelLighting
		{
			public Color ambientColor;
			public Color fogColor;
			public bool fog;
		}

		[SerializeField] private GameObject _levelParent;
		[SerializeField] private LevelLighting _levelLighting;

		private NetworkedLevelObject[] _networkedObjects;
		private SpawnPoint[] _playerSpawnPoints;
		private NetworkRunner _runner;

		public bool IsActive => _levelParent.activeInHierarchy;

		private void Awake()
		{
			_playerSpawnPoints = GetComponentsInChildren<SpawnPoint>(true);
		}

		// Set level active and get player spawnpoints
		public void Activate()
		{
			_levelParent.SetActive(true);
			SetLevelLighting();
		}

		// A variation that also spawns any networkobjects present
		public void Activate(NetworkRunner runner)
		{
			_runner = runner;

			if (GameManager.instance.Object.HasStateAuthority)
			{
				_networkedObjects = _levelParent.GetComponentsInChildren<NetworkedLevelObject>();
				for (int i = 0; i < _networkedObjects.Length; i++)
				{
					_networkedObjects[i].SpawnPrefab(runner);
				}
			}

			_levelParent.SetActive(true);
			SetLevelLighting();
		}

		// Despawn networkobjects if any, and set level inactive
		public void Deactivate()
		{
			if (_networkedObjects != null && _networkedObjects.Length > 0)
			{
				for (int i = 0; i < _networkedObjects.Length; i++)
				{
					_networkedObjects[i].DespawnPrefab(_runner);
				}
				_networkedObjects = null;
			}

			_levelParent.SetActive(false);
		}

		private void SetLevelLighting()
		{
			RenderSettings.ambientLight = _levelLighting.ambientColor;
			RenderSettings.fogColor = _levelLighting.fogColor;
			RenderSettings.fog = _levelLighting.fog;
		}

		public SpawnPoint GetPlayerSpawnPoint(int id)
		{
			return _playerSpawnPoints[id].GetComponent<SpawnPoint>();
		}
	}
}