using System.Collections.Generic;
using UnityEngine;
using Fusion;

namespace FusionExamples.Tanknarok
{
	public interface ISpawnableNetworkLevelObject
	{
		void Initialize();
	}

	// This script is used for placing networkobjects in a level
	// Once the prefab is assigned it will be drawn as a "preview" in the scene
	// When loading the level in-game the networkobject will be spawned based on this gameobjects transform
	// It also checks for "ISpawnableNetworkLevelObject" in case the networkobject needs to initialize something before spwning

	[ExecuteInEditMode]
	public class NetworkedLevelObject : MonoBehaviour
	{
		[SerializeField] private NetworkObject _networkObjectPrefab;

		private NetworkObject _spawnedNetworkObject;

		// Spawns the assigned networkobject prefab
		public void SpawnPrefab(NetworkRunner runner)
		{
			_spawnedNetworkObject = runner.Spawn(_networkObjectPrefab, transform.position, transform.rotation,null, InitObject);
		}

		// Checks if the networkobject needs to initialize before spawning
		private void InitObject(NetworkRunner runner, NetworkObject obj)
		{
			ISpawnableNetworkLevelObject spawnable = obj.GetComponent<ISpawnableNetworkLevelObject>();
			if (spawnable != null)
				spawnable.Initialize();
		}

		// Despawns the existing networkobject
		public void DespawnPrefab(NetworkRunner runner)
		{
			if (_spawnedNetworkObject != null && runner != null)
			{
				runner.Despawn(_spawnedNetworkObject);
				_spawnedNetworkObject = null;
			}
		}

		// Gets the meshes and materials of the prefab and draws it in the scene to preview how it will look in-game
#if UNITY_EDITOR
		class NetworkedObjectApperance
		{
			public NetworkedObjectApperance(Material[] mats, MeshFilter newMesh)
			{
				materials = mats;
				mesh = newMesh;
			}

			public Material[] materials;
			public MeshFilter mesh;
		}

		private List<NetworkedObjectApperance> _objectApperances = new List<NetworkedObjectApperance>();

		private void OnEnable()
		{
			InitApperance();

			Camera.onPreCull -= DrawWithCamera;
			Camera.onPreCull += DrawWithCamera;
		}

		private void OnDisable()
		{
			Camera.onPreCull -= DrawWithCamera;
		}

		private void DrawWithCamera(Camera cam)
		{
			if (cam && _objectApperances.Count > 0)
			{
				Draw(cam);
			}
		}

		private void Draw(Camera camera)
		{
			if (Application.isPlaying || UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
				return;

			if (_networkObjectPrefab == null && _objectApperances.Count > 0)
			{
				_objectApperances.Clear();
				return;
			}

			for (int i = 0; i < _objectApperances.Count; i++)
			{
				Matrix4x4 posMatrix = transform.localToWorldMatrix;
				posMatrix.SetTRS(_objectApperances[i].mesh.transform.localPosition, _objectApperances[i].mesh.transform.localRotation, _objectApperances[i].mesh.transform.localScale);
				Matrix4x4 matrix = transform.localToWorldMatrix * posMatrix;

				for (int m = 0; m < _objectApperances[i].materials.Length; m++)
				{
					Graphics.DrawMesh(_objectApperances[i].mesh.sharedMesh,
						matrix,
						_objectApperances[i].materials[m],
						gameObject.layer,
						camera,
						m);
				}
			}
		}

		private void InitApperance()
		{
			if (_networkObjectPrefab == null)
				return;

			_objectApperances.Clear();

			MeshRenderer[] renderers = _networkObjectPrefab.GetComponentsInChildren<MeshRenderer>();
			for (int i = 0; i < renderers.Length; i++)
			{
				NetworkedObjectApperance apperance = new NetworkedObjectApperance(renderers[i].sharedMaterials, renderers[i].GetComponent<MeshFilter>());
				_objectApperances.Add(apperance);
			}
		}
#endif
	}
}