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
        [SerializeField] private float roundEndDelay   = 5f;
        [SerializeField] private int   enemiesToKill   = 10;

        [Header("Spawn Settings")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private float       respawnDelay  = 3f;

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<GameState>    OnStateChanged;
        public event Action<int>          OnScoreChanged;    // total score
        public event Action<int, int>     OnRoundChanged;    // (current, total)
        public event Action<float>        OnTimerTick;

        // ── Properties ────────────────────────────────────────────────────────
        public GameState CurrentState  { get; private set; }
        public int        Score         { get; private set; }
        public int        CurrentRound  { get; private set; } = 1;
        public int        KillCount     { get; private set; }

        private float _roundTimer;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // DontDestroyOnLoad requires a root GameObject — detach from parent if needed
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
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
            Score      = 0;
            KillCount  = 0;
            CurrentRound = 1;
            SetState(GameState.Playing);
            StartRound();
        }

        private void StartRound()
        {
            _roundTimer = roundDuration;
            KillCount   = 0;
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

        // ── Score ─────────────────────────────────────────────────────────────
        public void AddScore(int amount)
        {
            Score     += amount;
            KillCount++;
            OnScoreChanged?.Invoke(Score);

            if (KillCount >= enemiesToKill)
                EndRound(true);
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
            if (roundDuration <= 0f) return;    // unlimited

            _roundTimer -= Time.deltaTime;
            OnTimerTick?.Invoke(_roundTimer);

            if (_roundTimer <= 0f)
                EndRound(false);    // time ran out = player loses round
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
