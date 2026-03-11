using UnityEngine;
using FreeWorld.Enemy;

namespace FreeWorld.Utilities
{
    /// <summary>
    /// Spawner that creates waves of enemies from a list of spawn points.
    /// Hook up to GameManager round events to trigger new waves.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private GameObject   enemyPrefab;
        [SerializeField] private Transform[]  spawnPoints;
        [SerializeField] private int          baseEnemiesPerWave = 5;
        [SerializeField] private int          extraPerRound      = 2;   // scales with rounds
        [SerializeField] private float        spawnInterval      = 1f;  // seconds between spawns

        // IEnumerator Start ensures GameManager.Start() has finished before we run.
        private System.Collections.IEnumerator Start()
        {
            yield return null; // frame 1
            yield return null; // frame 2
            yield return null; // frame 3 — belt-and-suspenders

            // Auto-discover spawn points from scene if not wired in Inspector
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                var found = new System.Collections.Generic.List<Transform>();
                foreach (var go in FindObjectsOfType<GameObject>())
                    if (go.name.StartsWith("SpawnPoint")) found.Add(go.transform);
                spawnPoints = found.ToArray();
                Debug.Log($"[EnemySpawner] Auto-found {spawnPoints.Length} spawn points.");
            }

            // Auto-find enemy prefab from scene if not wired in Inspector
            if (enemyPrefab == null)
            {
                var guids = UnityEngine.Object.FindObjectsOfType<Enemy.EnemyAI>();
                if (guids.Length > 0)
                    Debug.LogWarning("[EnemySpawner] enemyPrefab is NOT assigned! " +
                        "Assign 'Assets/Prefabs/Enemy_Grunt.prefab' in the Inspector.");
            }

            Managers.GameManager gm = Managers.GameManager.Instance;
            if (gm == null) { Debug.LogError("[EnemySpawner] GameManager not found!"); yield break; }

            // Subscribe for future rounds
            gm.OnRoundChanged += (current, _) => StartWave(current);

            // Start whichever round is currently active
            Debug.Log($"[EnemySpawner] Starting wave {gm.CurrentRound}, state={gm.CurrentState}");
            if (gm.CurrentState == Managers.GameState.Playing)
                StartWave(gm.CurrentRound);
        }

        public void StartWave(int roundNumber)
        {
            // Always cancel any in-progress wave so new rounds always start
            StopAllCoroutines();
            int count = baseEnemiesPerWave + (roundNumber - 1) * extraPerRound;
            StartCoroutine(SpawnWave(count, roundNumber));
        }

        private System.Collections.IEnumerator SpawnWave(int count, int round)
        {
            // Build a shuffled index list so no spawn point is reused until all are exhausted
            var indices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < count; i++)
                indices.Add(i % spawnPoints.Length);
            // Fisher-Yates shuffle
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j   = Random.Range(0, i + 1);
                int tmp = indices[i]; indices[i] = indices[j]; indices[j] = tmp;
            }

            for (int i = 0; i < count; i++)
            {
                SpawnEnemy(round, indices[i]);
                yield return new WaitForSeconds(spawnInterval);
            }
            // Notify GameManager that all enemies for this wave have been spawned
            Managers.GameManager.Instance?.NotifyWaveSpawnComplete();
        }

        private EnemyVariant ChooseVariant(int round)
        {
            float r = Random.value;
            if (round == 3)
                return EnemyVariant.OriginalSkin;  // full SWAT skin, no tint
            if (round >= 7)
            {
                // 50% Grunt, 25% Heavy, 25% Scout
                if (r < 0.50f) return EnemyVariant.Grunt;
                if (r < 0.75f) return EnemyVariant.Heavy;
                return EnemyVariant.Scout;
            }
            if (round >= 4)
            {
                // 65% Grunt, 35% Heavy
                return r < 0.65f ? EnemyVariant.Grunt : EnemyVariant.Heavy;
            }
            return EnemyVariant.Grunt;
        }

        private void SpawnEnemy(int round, int spawnIndex = -1)
        {
            if (enemyPrefab == null)
            {
                Debug.LogError("[EnemySpawner] enemyPrefab is null — assign it in the Inspector!");
                Managers.GameManager.Instance?.RegisterEnemySpawn();
                return;
            }
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError("[EnemySpawner] No spawn points!");
                return;
            }
            int idx = (spawnIndex >= 0 && spawnIndex < spawnPoints.Length)
                ? spawnIndex
                : Random.Range(0, spawnPoints.Length);
            Transform sp = spawnPoints[idx];
            // Small random offset so enemies don't stack
            Vector3 offset = new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));
            Vector3 spawnPos = sp.position + offset;

            // Snap to nearest NavMesh point so the agent is always valid on spawn
            if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out var hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
                spawnPos = hit.position;

            var go = Instantiate(enemyPrefab, spawnPos, sp.rotation);
            go.GetComponent<Enemy.EnemyAI>()?.SetVariant(ChooseVariant(round));
            Managers.GameManager.Instance?.RegisterEnemySpawn();
            Debug.Log($"[EnemySpawner] Spawned enemy round={round} spawnPoint={idx} variant={ChooseVariant(round)}");
        }
    }
}
