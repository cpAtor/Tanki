using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using FusionExamples.Tanknarok;

namespace FusionExamples.FusionHelpers
{
	/// <summary>
	/// Small helper that provides a simple world/player pattern for launching Fusion
	/// </summary>
	public class FusionLauncher : MonoBehaviour, INetworkRunnerCallbacks
	{
		private NetworkRunner _runner;
		private Action<NetworkRunner, ConnectionStatus, string> _connectionCallback;
		private ConnectionStatus _status;
		private FusionObjectPoolRoot _pool;
		private Action<NetworkRunner> _spawnWorldCallback;
		private Action<NetworkRunner,PlayerRef> _spawnPlayerCallback;
		private Action<NetworkRunner,PlayerRef> _despawnPlayerCallback;

		public enum ConnectionStatus
		{
			Disconnected,
			Connecting,
			Failed,
			Connected
		}

		public async void Launch(GameMode mode, string room, 
			Action<NetworkRunner, ConnectionStatus, string> onConnect, 
			Action<NetworkRunner> onSpawnWorld, 
			Action<NetworkRunner, PlayerRef> onSpawnPlayer,
			Action<NetworkRunner, PlayerRef> onDespawnPlayer)
		{
			_connectionCallback = onConnect;
			_spawnWorldCallback = onSpawnWorld;
			_spawnPlayerCallback = onSpawnPlayer;
			_despawnPlayerCallback = onDespawnPlayer;

			SetConnectionStatus(ConnectionStatus.Connecting, "");

			DontDestroyOnLoad(gameObject);
			
			_runner = gameObject.AddComponent<NetworkRunner>();
			_runner.name = name;
			_runner.ProvideInput = mode != GameMode.Server;

			if(_pool==null)
				_pool = gameObject.AddComponent<FusionObjectPoolRoot>();

			await _runner.StartGame(new StartGameArgs() {GameMode = mode, SessionName = room, ObjectPool = _pool});

			if (mode != GameMode.Client && TryGetSceneRef(out SceneRef scene))
			{
				_runner.SetActiveScene(scene);
			}
		}

		private bool TryGetSceneRef(out SceneRef sceneRef)
		{
			var scenePath = SceneManager.GetActiveScene().path;
			var config = NetworkProjectConfig.Global;

			if (config.TryGetSceneRef(scenePath, out sceneRef) == false)
			{
				// Failed to find scene by full path, try with just name
				if (config.TryGetSceneRef(SceneManager.GetActiveScene().name, out sceneRef) == false)
				{
					Debug.LogError($"Could not find scene reference to scene {scenePath}, make sure it's added to {nameof(NetworkProjectConfig)}.");
					return false;
				}
			}

			return true;
		}

		private void SetConnectionStatus(ConnectionStatus status, string message)
		{
			_status = status;
			if (_connectionCallback != null)
				_connectionCallback(_runner, status, message);
		}

		public void OnInput(NetworkRunner runner, NetworkInput input)
		{
		}

		public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
		{
		}
		
		public void OnConnectedToServer(NetworkRunner runner)
		{
			Debug.Log("Connected to server");
			if (runner.GameMode == GameMode.Shared)
				InstantiatePlayer(runner, runner.LocalPlayer);
			SetConnectionStatus(ConnectionStatus.Connected, "");
		}

		public void OnDisconnectedFromServer(NetworkRunner runner)
		{
			Debug.Log("Disconnected from server");
			_runner.Shutdown();
			SetConnectionStatus(ConnectionStatus.Disconnected, "");
		}

		public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
		{
			request.Accept();
		}

		public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
		{
			Debug.Log($"Connect failed {reason}");
			SetConnectionStatus(ConnectionStatus.Failed, reason.ToString());
		}

		// Called on host when new player joins
		public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
		{
			Debug.Log("Player Joined - spawning");
			InstantiatePlayer(runner, player);
			SetConnectionStatus(ConnectionStatus.Connected, "");
		}

		private void InstantiatePlayer(NetworkRunner runner, PlayerRef playerref)
		{
			if (_spawnWorldCallback!=null && (runner.IsServer || runner.IsSharedModeMasterClient) )
			{
				_spawnWorldCallback(runner);
				_spawnWorldCallback = null;
			}

			_spawnPlayerCallback(runner, playerref);
		}

		public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
		{
			Debug.Log("Player Left");
			_despawnPlayerCallback(runner, player);

			SetConnectionStatus(_status, "Player Left");
		}

		public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
		{
		}

		public void OnObjectWordsChanged(NetworkRunner runner, NetworkObject obj, HashSet<int> changedWords, NetworkObjectMemoryPtr oldMemory)
		{
		}

		public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
		{
		}

		public void OnSceneLoadDone(NetworkRunner runner)
		{
		}

		public void OnSceneLoadStart(NetworkRunner runner)
		{
		}

		public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
		{		
			Debug.Log("OnShutdown");
			string message = "";
			switch (shutdownReason)
			{
				case GameManager.ShutdownReason_GameAlreadyRunning:
					message = "Game in this room already started!";
					break;
				case ShutdownReason.IncompatibleConfiguration:
					message = "This room already exist in a different game mode!";
					break;
				case ShutdownReason.Ok:
					message = "User terminated network session!"; 
					break;
				case ShutdownReason.Error:
					message = "Unknown network error!";
					break;
				case ShutdownReason.ServerInRoom:
					message = "There is already a server/host in this room";
					break;
				case ShutdownReason.DisconnectedByPluginLogic:
					message = "The Photon server plugin terminated the network session!";
					break;
				default:
					message = shutdownReason.ToString();
					break;
			}
			SetConnectionStatus(ConnectionStatus.Disconnected, message);

			// TODO: This cleanup should be handled by the ClearPools call below, but currently Fusion is not returning pooled objects on shutdown, so...
			// Destroy all NOs
			NetworkObject[] nos = FindObjectsOfType<NetworkObject>();
			for (int i = 0; i < nos.Length; i++)
				Destroy(nos[i].gameObject);
			
			// Clear all the player registries
			// TODO: This does not belong in here
			PlayerManager.ResetPlayerManager();

			// Reset the object pools
			_pool.ClearPools();
            
      if(_runner!=null && _runner.gameObject != null)
	    	Destroy(_runner.gameObject);
		}

		public void Shutdown()
		{
			if(_runner!=null)
				_runner.Shutdown();
		}
	}
}