using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeWorld.Managers
{
    public enum GameState { MainMenu, Playing, Paused, RoundEnd, GameOver }

    /// <summary>
    /// Central game state machine. Singleton pattern.
    /// Controls rounds, score, pause, win/loss conditions.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ── Settings ──────────────────────────────────────────────────────────
        [Header("Round Settings")]
        [SerializeField] private int   totalRounds     = 15;
        [SerializeField] private float roundDuration   = 120f;   // seconds (0 = unlimited)
        [SerializeField] private float roundEndDelay   = 3f;

        [Header("Spawn Settings")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private float       respawnDelay  = 3f;

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<GameState>    OnStateChanged;
        public event Action<int>          OnScoreChanged;    // total score
        public event Action<int, int>     OnRoundChanged;    // (current, total)
        public event Action<float>        OnTimerTick;
        public event Action<string, int>  OnEnemyKilled;     // (enemyTypeName, scoreAwarded)
        public event Action<int>          OnEnemiesChanged;  // enemies remaining this wave

        // ── Properties ─────────────────────────────────────────────────────
        public GameState CurrentState  { get; private set; }
        public int        Score         { get; private set; }
        public int        CurrentRound  { get; private set; } = 1;
        public int        KillCount     { get; private set; }
        public int   EnemiesRemaining { get; private set; }
        public float RoundTimer        => _roundTimer;

        private float _roundTimer;
        private bool  _waveSpawnComplete;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            // Always destroy any stale instance surviving from a previous Play session.
            if (Instance != null && Instance != this)
                Destroy(Instance.gameObject);
            Instance = this;

            // Ensure serialised fields have sane values even if Inspector shows 0
            if (roundDuration < 10f)  roundDuration  = 120f;
            if (roundEndDelay < 0.5f) roundEndDelay  = 3f;
        }

        private void Start()
        {
            // Subscribe to player death for auto-respawn
            var ph = FindObjectOfType<Player.PlayerHealth>();
            if (ph != null)
                ph.OnDeath += HandlePlayerDeath;

            StartGame();
        }

        private void HandlePlayerDeath()
        {
            StartCoroutine(RespawnPlayer());
        }

        private IEnumerator RespawnPlayer()
        {
            yield return new WaitForSecondsRealtime(respawnDelay);
            if (CurrentState == GameState.Playing || CurrentState == GameState.Paused)
            {
                var ph = FindObjectOfType<Player.PlayerHealth>();
                ph?.Respawn(GetRandomSpawnPoint());
                if (CurrentState == GameState.Playing)
                    FindObjectOfType<Player.PlayerCamera>()?.SetCursorLock(true);
            }
        }

        private void Update()
        {
            // Escape is handled by UIManager to avoid event ordering issues
            if (CurrentState == GameState.Playing)
                TickRoundTimer();
        }

        // ── Game Flow ─────────────────────────────────────────────────────────
        public void StartGame()
        {
            Time.timeScale = 1f;   // ensure not stuck from a previous pause/session
            Score        = 0;
            KillCount    = 0;
            CurrentRound = 1;
            SetState(GameState.Playing);
            StartRound();
        }

        private void StartRound()
        {
            _roundTimer        = roundDuration;
            KillCount          = 0;
            EnemiesRemaining   = 0;
            _waveSpawnComplete = false;
            OnRoundChanged?.Invoke(CurrentRound, totalRounds);
        }

        private void EndRound(bool playerWon)
        {
            SetState(GameState.RoundEnd);

            if (CurrentRound >= totalRounds || !playerWon)
                StartCoroutine(TransitionToGameOver());
            else
                StartCoroutine(StartNextRound());
        }

        private IEnumerator StartNextRound()
        {
            yield return new WaitForSeconds(roundEndDelay);
            CurrentRound++;
            SetState(GameState.Playing);
            StartRound();
        }

        private IEnumerator TransitionToGameOver()
        {
            yield return new WaitForSeconds(roundEndDelay);
            SetState(GameState.GameOver);
        }

        // ── Score / Kills ─────────────────────────────────────────────────────
        /// <summary>Call when an enemy dies to award score and fire kill events.</summary>
        public void EnemyKilled(string typeName, int scoreValue)
        {
            Score     += scoreValue;
            KillCount ++;
            EnemiesRemaining = Mathf.Max(0, EnemiesRemaining - 1);
            OnScoreChanged?.Invoke(Score);
            OnEnemyKilled?.Invoke(typeName, scoreValue);
            OnEnemiesChanged?.Invoke(EnemiesRemaining);
            FreeWorld.Enemy.EnemyAdaptiveSystem.Instance?.NotifyPlayerKill();

            CheckWaveComplete();
        }

        /// <summary>Called by EnemySpawner once all enemies in the wave have been instantiated.</summary>
        public void NotifyWaveSpawnComplete()
        {
            _waveSpawnComplete = true;
            CheckWaveComplete();
        }

        private void CheckWaveComplete()
        {
            if (_waveSpawnComplete && EnemiesRemaining <= 0 && KillCount > 0)
                EndRound(true);
        }

        /// <summary>Call from EnemySpawner each time an enemy is spawned.</summary>
        public void RegisterEnemySpawn()
        {
            EnemiesRemaining++;
            OnEnemiesChanged?.Invoke(EnemiesRemaining);
        }

        // ── Pause ─────────────────────────────────────────────────────────────
        private void HandlePauseInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                TogglePause();
        }

        public void TogglePause()
        {
            if (CurrentState == GameState.Playing)
            {
                SetState(GameState.Paused);
                Time.timeScale = 0f;
                // UIManager sets cursor directly; this is a belt-and-suspenders fallback
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
            else if (CurrentState == GameState.Paused)
            {
                SetState(GameState.Playing);
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void TickRoundTimer()
        {
            _roundTimer -= Time.deltaTime;
            OnTimerTick?.Invoke(_roundTimer);
            if (_roundTimer <= 0f)
                EndRound(false);   // time ran out
        }

        private void SetState(GameState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        // ── Player respawn ────────────────────────────────────────────────────
        public Vector3 GetRandomSpawnPoint()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return Vector3.zero;
            return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)].position;
        }
    }
}
