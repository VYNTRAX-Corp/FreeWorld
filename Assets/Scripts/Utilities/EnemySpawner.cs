using UnityEngine;

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

        private bool _spawning;

        private void Start()
        {
            Managers.GameManager gm = Managers.GameManager.Instance;
            if (gm != null)
            {
                gm.OnRoundChanged += (current, _) => StartWave(current);
                // GameManager.StartGame() fires OnRoundChanged(1) in its own Start().
                // If that ran before us, we missed it — kick off round 1 manually.
                if (gm.CurrentState == Managers.GameState.Playing && gm.CurrentRound == 1)
                    StartWave(1);
            }
        }

        public void StartWave(int roundNumber)
        {
            if (_spawning) return;
            int count = baseEnemiesPerWave + (roundNumber - 1) * extraPerRound;
            StartCoroutine(SpawnWave(count));
        }

        private System.Collections.IEnumerator SpawnWave(int count)
        {
            _spawning = true;
            for (int i = 0; i < count; i++)
            {
                SpawnEnemy();
                yield return new WaitForSeconds(spawnInterval);
            }
            _spawning = false;
        }

        private void SpawnEnemy()
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return;
            Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Instantiate(enemyPrefab, sp.position, sp.rotation);
        }
    }
}
