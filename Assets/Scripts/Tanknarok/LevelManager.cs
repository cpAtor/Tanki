using Fusion;
using UnityEngine;

namespace FusionExamples.Tanknarok
{
	/// <summary>
	/// The LevelManager controls the map - keeps track of spawn points for players and powerups, and spawns powerups at regular intervals.
	/// </summary>
	public class LevelManager : MonoBehaviour
	{
		[SerializeField] private LevelBehaviour _lobby;
		[SerializeField] private LevelBehaviour[] _levels;
		[SerializeField] private LevelBehaviour _currentLevel;
		[SerializeField] private CameraScreenFXBehaviour _transitionEffect;
		[SerializeField] private AudioEmitter _audioEmitter;

		public delegate void Callback();

		public void Reset()
		{
			_currentLevel = _lobby;
			_currentLevel.Activate();

			for (int i = 0; i < _levels.Length; i++)
			{
				_levels[i].Deactivate();
			}
		}

		public void ReturnToLobby()
		{
			_currentLevel.Deactivate();

			_currentLevel = _lobby;
			_currentLevel.Activate();
		}

		// Get a random level
		public int GetRandomLevelIndex()
		{
			return UnityEngine.Random.Range(0, _levels.Length);
		}

		// Transition to level
		public void LevelTransition(NetworkRunner runner, int levelIndex, Callback playersDespawned, Callback screenEffectOn, Callback onTransitionOver)
		{
			Debug.Log("Level Manager transitioning to level");
			StartCoroutine(TransitionSequence( runner, levelIndex, playersDespawned, screenEffectOn, onTransitionOver));
		}

		// Transition to lobby
		public void LobbyTransition(Callback playersDespawned, Callback screenEffectOn, Callback onTransitionOver)
		{
			Debug.Log("Level Manager transitioning to lobby");
			StartCoroutine(TransitionSequence( null, -1, playersDespawned, screenEffectOn,onTransitionOver));
		}
		
		private void UnloadLastLevel()
		{
			if (_currentLevel.IsActive)
				_currentLevel.Deactivate();
		}

		// Despawns players, toggles the glitch effect, changes the current level and respawns players
		private System.Collections.IEnumerator TransitionSequence(NetworkRunner runner, int nextLevelIndex, Callback playersDespawned, Callback screenEffectOn, Callback onLevelTransitionOver)
		{
			yield return new WaitForSeconds(1.0f);

			// Despawn players with a small delay between each one
			for (int i = 0; i < PlayerManager.allPlayers.Count; i++)
			{
				PlayerManager.allPlayers[i].DespawnTank();
				yield return new WaitForSeconds(0.1f);
			}

			playersDespawned?.Invoke();

			// Delay here to give the gamemanager some time to show the score
			yield return new WaitForSeconds(1.5f);

			_transitionEffect.ToggleGlitch(true);
			_audioEmitter.Play();

			yield return new WaitForSeconds(0.3f);
			screenEffectOn?.Invoke();

			UnloadLastLevel();

			// Load the level while the screen effect is up
			// Check if we are transitioning to a new level or the lobby
			if( runner!=null && nextLevelIndex>=0 )
			{
				// Activate the next level
				_currentLevel = _levels[nextLevelIndex];
				_currentLevel.Activate(runner);
				MusicPlayer.instance.SetLowPassTranstionDirection( 1f);
			}
			else
			{
				_currentLevel = _lobby;
				_currentLevel.Activate();
				MusicPlayer.instance.SetLowPassTranstionDirection( -1f);
			}

			_transitionEffect.ToggleGlitch(false);
			_audioEmitter.Stop();

			yield return new WaitForSeconds(0.3f);

			// Respawn with slight delay between each player
			for (int i = 0; i < PlayerManager.allPlayers.Count; i++)
			{
				Player player = PlayerManager.allPlayers[i];
				player.Respawn(0);
				yield return new WaitForSeconds(0.3f);
			}

			if (onLevelTransitionOver!=null)
				onLevelTransitionOver();
		}

		/// <summary>
		/// Find a spawnpoint based on the players ID
		/// </summary>
		/// <param name="playerID">ID of the player</param>
		/// <returns>Player spawnpoint location</returns>
		public SpawnPoint GetPlayerSpawnPoint(int playerID)
		{
			if (_currentLevel.IsActive)
				return _currentLevel.GetPlayerSpawnPoint(playerID);
			return _lobby.GetPlayerSpawnPoint(playerID);
		}
	}
}