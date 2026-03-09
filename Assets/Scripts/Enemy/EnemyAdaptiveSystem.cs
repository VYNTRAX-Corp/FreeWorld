using UnityEngine;
using System.Collections.Generic;

namespace FreeWorld.Enemy
{
    /// <summary>
    /// Adaptive AI difficulty manager. Watches how quickly the player kills enemies
    /// and adjusts three parameters globally across all active enemies:
    ///
    ///   ReactionDelay  — seconds before an enemy fires its first shot after entering Attack
    ///   FlankInterval  — how often enemy tries to circle the player (lower = more flanking)
    ///   ChaseSpeedMult — movement speed multiplier applied on top of variant base speed
    ///
    /// No machine learning — simple rolling-window kill tracker that shifts difficulty.
    /// Hook-up is automatic: EnemyAI reads this singleton; GameManager calls NotifyPlayerKill.
    /// </summary>
    public class EnemyAdaptiveSystem : MonoBehaviour
    {
        public static EnemyAdaptiveSystem Instance { get; private set; }

        // ── Inspector tunables ────────────────────────────────────────────────
        [Header("Difficulty Bounds")]
        [SerializeField] private float minReactionDelay = 0.10f;  // fastest enemy reacts
        [SerializeField] private float maxReactionDelay = 0.90f;  // slowest enemy reacts
        [SerializeField] private float minFlankInterval = 4.0f;   // most aggressive flanking
        [SerializeField] private float maxFlankInterval = 16.0f;  // least aggressive flanking
        [SerializeField] private float minChaseSpeedMult = 1.00f;
        [SerializeField] private float maxChaseSpeedMult = 1.40f;

        [Header("Adaptation Rate")]
        [Tooltip("How much each kill inside the tracking window shifts difficulty (0-1).")]
        [SerializeField] private float adaptRatePerKill  = 0.07f;
        [Tooltip("How much difficulty eases back per second when player isn't killing.")]
        [SerializeField] private float idleDecayPerSec   = 0.005f;
        [Tooltip("Rolling time window in seconds to count player kills.")]
        [SerializeField] private float trackingWindow    = 25f;
        [Tooltip("Kills inside the window before ramping toward max difficulty.")]
        [SerializeField] private int   killsToMaxRamp    = 5;

        // ── Public reads (used by EnemyAI every frame) ────────────────────────
        public float ReactionDelay   { get; private set; }
        public float FlankInterval   { get; private set; }
        public float ChaseSpeedMult  { get; private set; }

        // ── Internal ──────────────────────────────────────────────────────────
        private float _diffLevel = 0.25f;   // 0 = easy, 1 = max difficulty; starts slightly above trivial
        private readonly Queue<float> _killTimes = new Queue<float>();

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            // Not DontDestroyOnLoad — this is a per-session singleton living in the game scene
            ApplyDifficulty();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Slow idle decay — if the player isn't killing, difficulty drifts down
            _diffLevel = Mathf.Clamp01(_diffLevel - idleDecayPerSec * Time.deltaTime);
            ApplyDifficulty();
        }

        // ── Notification API ──────────────────────────────────────────────────
        /// <summary>Call every time the player kills an enemy (done via GameManager.EnemyKilled).</summary>
        public void NotifyPlayerKill()
        {
            float now = Time.time;
            _killTimes.Enqueue(now);

            // Flush kills older than the tracking window
            while (_killTimes.Count > 0 && now - _killTimes.Peek() > trackingWindow)
                _killTimes.Dequeue();

            // More kills in the window → harder difficulty
            float ratio = Mathf.Clamp01((float)_killTimes.Count / killsToMaxRamp);
            _diffLevel  = Mathf.Clamp01(_diffLevel + adaptRatePerKill * ratio);
            ApplyDifficulty();
        }

        /// <summary>Call when the player dies — easens off so it doesn't feel unfair.</summary>
        public void NotifyPlayerDeath()
        {
            _diffLevel = Mathf.Clamp01(_diffLevel - adaptRatePerKill * 3f);
            _killTimes.Clear();
            ApplyDifficulty();
        }

        // ── Debug ─────────────────────────────────────────────────────────────
        /// <summary>Difficulty level 0-1 — readable in the Inspector at runtime for testing.</summary>
        public float DifficultyLevel => _diffLevel;

        // ── Internal ─────────────────────────────────────────────────────────
        private void ApplyDifficulty()
        {
            ReactionDelay  = Mathf.Lerp(maxReactionDelay, minReactionDelay, _diffLevel);
            FlankInterval  = Mathf.Lerp(maxFlankInterval, minFlankInterval, _diffLevel);
            ChaseSpeedMult = Mathf.Lerp(minChaseSpeedMult, maxChaseSpeedMult, _diffLevel);
        }
    }
}
