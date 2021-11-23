using System.Collections;
using Fusion;
using FusionExamples.FusionHelpers;
using FusionExamples.UIHelpers;
using Tanknarok.UI;
using TMPro;
using UnityEngine;

namespace FusionExamples.Tanknarok
{
	/// <summary>
	/// App entry point and main UI flow management.
	/// </summary>
	public class GameLauncher : MonoBehaviour
	{
		[SerializeField] private GameManager _gameManagerPrefab;
		[SerializeField] private Player _playerPrefab;
		[SerializeField] private TMP_InputField _room;
		[SerializeField] private TextMeshProUGUI _progress;
		[SerializeField] private Panel _uiCurtain;
		[SerializeField] private Panel _uiStart;
		[SerializeField] private Panel _uiProgress;
		[SerializeField] private Panel _uiRoom;
		[SerializeField] private GameObject _uiGame;

		private FusionLauncher.ConnectionStatus _status = FusionLauncher.ConnectionStatus.Disconnected;
		private GameMode _gameMode;
		
		private void Awake()
		{
			DontDestroyOnLoad(this);
		}

		private void Start()
		{
			OnConnectionStatusUpdate(null, FusionLauncher.ConnectionStatus.Disconnected, "");
		}

		private void Update()
		{
			if (_uiProgress.isShowing)
			{
				if (Input.GetKeyUp(KeyCode.Escape))
				{
					FusionLauncher launcher = FindObjectOfType<FusionLauncher>();
					if (launcher != null)
						launcher.Shutdown();
				}					
				UpdateUI();
			}
		}

		// What mode to play - Called from the start menu
		public void OnHostOptions()
		{
			SetGameMode(GameMode.Host);
		}

		public void OnJoinOptions()
		{
			SetGameMode(GameMode.Client);
		}

		public void OnSharedOptions()
		{
			SetGameMode(GameMode.Shared);
		}

		private void SetGameMode(GameMode gamemode)
		{
			_gameMode = gamemode;
			if (GateUI(_uiStart))
				_uiRoom.SetVisible(true);
		}

		public void OnEnterRoom()
		{
			if (GateUI(_uiRoom))
			{
		    FusionLauncher launcher = FindObjectOfType<FusionLauncher>();
				if (launcher == null)
					launcher = new GameObject("Launcher").AddComponent<FusionLauncher>();
				launcher.Launch(_gameMode, _room.text, OnConnectionStatusUpdate, OnSpawnWorld, OnSpawnPlayer, OnDespawnPlayer);
			}
		}

		/// <summary>
		/// Call this method from button events to close the current UI panel and check the return value to decide
		/// if it's ok to proceed with handling the button events. Prevents double-actions and makes sure UI panels are closed. 
		/// </summary>
		/// <param name="ui">Currently visible UI that should be closed</param>
		/// <returns>True if UI is in fact visible and action should proceed</returns>
		private bool GateUI(Panel ui)
		{
			if (!ui.isShowing)
				return false;
			ui.SetVisible(false);
			return true;
		}

		private void OnConnectionStatusUpdate(NetworkRunner runner, FusionLauncher.ConnectionStatus status, string reason)
		{
			if (!this)
				return;

			Debug.Log(status);

			if (status != _status)
			{
				switch (status)
				{
					case FusionLauncher.ConnectionStatus.Disconnected:
						ErrorBox.Show("Disconnected!", reason, () => { });
						if (!runner.IsShutdown)
							runner.Shutdown();
						break;
					case FusionLauncher.ConnectionStatus.Failed:
						ErrorBox.Show("Error!", reason, () => { });
						if (!runner.IsShutdown)
							runner.Shutdown();
						break;
				}
			}

			_status = status;
			UpdateUI();
		}

		private void OnSpawnWorld(NetworkRunner runner)
		{
			Debug.Log("Spawning GameManager");
			runner.Spawn(_gameManagerPrefab, Vector3.zero, Quaternion.identity, null, InitializeWorld);
			void InitializeWorld(NetworkRunner runner, NetworkObject world)
			{
				world.transform.parent = transform;
			}
		}

		private void OnSpawnPlayer(NetworkRunner runner, PlayerRef playerref)
		{
			if (GameManager.instance!=null && GameManager.instance.playState != GameManager.PlayState.LOBBY)
			{
				Debug.Log("Not Spawning Player - game has already started");
				return;
			}
			Debug.Log("Spawning Player");
			runner.Spawn(_playerPrefab, Vector3.zero, Quaternion.identity, playerref, InitializePlayer);
			void InitializePlayer(NetworkRunner runner, NetworkObject networkObject)
			{
				Player player = networkObject.gameObject.GetComponent<Player>();

				Debug.Log($"Initializing player {player.playerID}");
				player.state = Player.State.New;
				player.lives = GameManager.MAX_LIVES;
				player.life = Player.MAX_HEALTH;
				player.score = 0;
			}
		}

		private void OnDespawnPlayer(NetworkRunner runner, PlayerRef playerref)
		{
			Debug.Log("Despawning Player");
			Player player = PlayerManager.Get(playerref);
			player.TriggerDespawn();
		}

		private void UpdateUI()
		{
			bool intro = false;
			bool progress = false;
			bool running = false;

			switch (_status)
			{
				case FusionLauncher.ConnectionStatus.Disconnected:
					_progress.text = "Disconnected!";
					intro = true;
					break;
				case FusionLauncher.ConnectionStatus.Failed:
					_progress.text = "Failed!";
					intro = true;
					break;
				case FusionLauncher.ConnectionStatus.Connecting:
					_progress.text = "Connecting";
					progress = true;
					break;
				case FusionLauncher.ConnectionStatus.Connected:
					running = true;
					break;
			}

			_uiCurtain.SetVisible(!running);
			_uiStart.SetVisible(intro);
			_uiProgress.SetVisible(progress);
			_uiGame.SetActive(running);
			
			if(intro)
				MusicPlayer.instance.SetLowPassTranstionDirection( -1f);
		}
	}
}