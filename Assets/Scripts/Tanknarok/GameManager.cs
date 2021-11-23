using System.Collections;
using UnityEngine;
using Fusion;

namespace FusionExamples.Tanknarok
{
	public class GameManager : NetworkBehaviour
	{
		public enum PlayState
		{
			LOBBY,
			LEVEL,
			TRANSITION
		}

		[Networked(OnChanged = nameof(OnStateChangedCallback))]
		public PlayState playState { get; set; }

		public const byte MAX_LIVES = 3;
		public const byte MAX_SCORE = 3;

		private ScoreManager _scoreManager;
		private ReadyupManager _readyupManager;
		private LevelManager _levelManager;
		private CountdownManager _countdownManager;

		private static PlayState _lastState;
		private bool _restart;

		public static GameManager instance { get; private set; }

		public override void Spawned()
		{
			// We only want one GameManager
			if (instance)
				Runner.Despawn(Object); // TODO: I've never seen this happen - do we really need this check?
			else
			{
				instance = this;

				// Find managers and UI
				_levelManager = FindObjectOfType<LevelManager>(true);
				_scoreManager = FindObjectOfType<ScoreManager>(true);
				_readyupManager = FindObjectOfType<ReadyupManager>(true);
				_countdownManager = FindObjectOfType<CountdownManager>(true);

				_levelManager.Reset();
				_scoreManager.HideLobbyScore();
				_readyupManager.ShowUI(this);
				_countdownManager.Reset();

				InputController.fetchInput = true;

				if(Object.HasStateAuthority)
					playState = PlayState.LOBBY;
				else if(playState!=PlayState.LOBBY)
				{
					Debug.Log("Rejecting Player, game is already running!");
					_restart = true;
				}
			}
		}

		public void OnTankDeath()
		{
			if (Object.HasStateAuthority)
			{
				if (playState != PlayState.LOBBY)
				{
					int playersleft = PlayerManager.PlayersAlive();
					Debug.Log($"Someone died - {playersleft} left");
					if (playersleft<=1)
					{
						Player lastPlayerStanding = playersleft == 0 ? null : PlayerManager.GetFirstAlivePlayer();
						// if there is only one player, who died from a laser (e.g.) we don't award scores. 
						// normally, the game should not be started with only a single player...
                        if (lastPlayerStanding != null)
                        {
                            RPC_ScoreAndLoad(lastPlayerStanding.playerID, (byte)(lastPlayerStanding.score+1), _levelManager.GetRandomLevelIndex());
                        }
					}
				}
			}
		}

		public void Restart(ShutdownReason shutdownReason)
		{
			Runner.Shutdown(true,null, shutdownReason);
			if (_levelManager != null)
			{
				_levelManager.ReturnToLobby();
			}
			instance = null;
			_restart = false;
		}

		public static void OnStateChangedCallback(Changed<NetworkBehaviour> changed)
		{
			// Get the last playstate
			PlayState newState = ((GameManager) changed.Behaviour).playState;

			changed.LoadOld();
			_lastState = ((GameManager) changed.Behaviour).playState;

			Debug.Log($"State changed from {_lastState} to {newState}");
		}

		public const ShutdownReason ShutdownReason_GameAlreadyRunning = (ShutdownReason)100;

		private void Update()
		{
			if (_restart || Input.GetKeyDown(KeyCode.Escape))
			{
				Restart( _restart ? ShutdownReason_GameAlreadyRunning : ShutdownReason.Ok);
				return;
			}
			PlayerManager.HandleNewPlayers();
		}

		private IEnumerator OnTankWon(Player player)
		{
			// Add a small delay (This is just for game-feel)
			yield return new WaitForSeconds(0.5f);

			// Check if there is a winner or if it is a draw
			int winningPlayerIndex = (player == null) ? -1 : player.playerID;

			if(Object.HasStateAuthority)
				RPC_LobbyTransition(winningPlayerIndex);
		}

		private void ResetStats()
		{
			for (int i = 0; i < PlayerManager.allPlayers.Count; i++)
			{
				Debug.Log($"Resetting player {i} stats to lives={MAX_LIVES}");
				PlayerManager.allPlayers[i].lives = MAX_LIVES;
				PlayerManager.allPlayers[i].score = 0;
			}
		}

		private void ResetLives()
		{
			for (int i = 0; i < PlayerManager.allPlayers.Count; i++)
			{
				Debug.Log($"Resetting player {i} lives to {MAX_LIVES}");
				PlayerManager.allPlayers[i].lives = MAX_LIVES;
			}
		}

		// Transition from lobby to level
		public void OnAllPlayersReady()
		{
			Debug.Log("All players are ready");
			if (playState!=PlayState.LOBBY)
				return;

			// Reset stats and transition to level.
			ResetStats();

			// close and hide the session from matchmaking / lists. this demo does not allow late join.
            Runner.SessionInfo.IsOpen = false;
            Runner.SessionInfo.IsVisible = false;

			if (Object.HasStateAuthority)
				RPC_ScoreAndLoad(-1,0, _levelManager.GetRandomLevelIndex());
		}
		
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All, InvokeLocal = true, Channel = RpcChannel.Reliable)]
		private void RPC_ScoreAndLoad(int winningPlayerIndex, byte winningPlayerScore, int nextLevelIndex)
		{
			playState = PlayState.TRANSITION;

			// This is a little quirky, but because we also want this code to work in Shared mode, we can't set
			// the winning players score from the GameManager because we probably don't have authority over it.
			// However, since this RPC is called on all clients, it'll also be called on the one that does have authority.
			// For all other clients we use the score value calculated by the GameManager authority.
			// It might have been cleaner to store the player score on the GameManager, but that would mean managing another list.
			if (winningPlayerIndex >= 0)
			{
				Player winner = PlayerManager.GetPlayerFromID(winningPlayerIndex);
				if (winner.Object.HasStateAuthority)
					winner.score = winningPlayerScore;
				if (winningPlayerScore >= MAX_SCORE)
				{
					StartCoroutine(OnTankWon(winner));
					return;
				}
			}
			// Reset lives and transition to level
			ResetLives();

			// Start transition
			_levelManager.LevelTransition( Runner, nextLevelIndex, () =>
			{
				// Players have despawned - Disable input
				InputController.fetchInput = false;

				// Show the score
				if (winningPlayerIndex >=0 )
					_scoreManager.UpdateScore(winningPlayerIndex, winningPlayerScore);
			},
			() =>
			{
				// Screen Effect is active
				_scoreManager.HideUiScoreAndReset(false);
				if (_lastState == PlayState.LOBBY)
				{
					_readyupManager.HideUI();
					_scoreManager.HideLobbyScore();
				}
			}, () =>
			{
				if (this)
				{
					StartCoroutine(_countdownManager.Countdown(() =>
					{
						// Set state to playing level
						playState = PlayState.LEVEL;
						// Enable inputs after countdow finishes
						InputController.fetchInput = true;
					}));
				}
			});
		}
		
		[Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All, Channel = RpcChannel.Reliable)]
		public void RPC_LobbyTransition(int winningPlayerIndex)
		{
			// Set state to lobby
			playState = PlayState.TRANSITION;

			// Reset players ready state so we don't launch immediately
			for (int i = 0; i < PlayerManager.allPlayers.Count; i++)
			{
				PlayerManager.allPlayers[i].ResetReady();
			}

			_levelManager.LobbyTransition( () =>
				{
					// Players have despawned Update score
					if (winningPlayerIndex != -1)
						_scoreManager.UpdateScore(winningPlayerIndex, PlayerManager.GetPlayerFromID(winningPlayerIndex).score);
				},
				() =>
				{
					// Show lobby scores and reset the score ui.
					_scoreManager.ShowLobbyScore(winningPlayerIndex);
					_scoreManager.HideUiScoreAndReset(true);
					_readyupManager.ShowUI(this);
				}, () =>
				{
					// Set state to playing level
					playState = PlayState.LOBBY;
				});
		}
	}
}