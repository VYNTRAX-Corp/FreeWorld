using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace FreeWorld.Enemy
{
    public enum EnemyState { Idle, Patrol, Chase, Attack, TakingCover, Dead }

    /// <summary>
    /// Basic FPS enemy AI using Unity NavMesh.
    /// States: Idle → Patrol → Chase → Attack → Cover → Dead
    /// 
    /// Setup: Add NavMeshAgent + this script to enemy. Bake NavMesh in scene.
    ///        Assign patrol points and player layer in Inspector.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAI : MonoBehaviour
    {
        // ── Detection ─────────────────────────────────────────────────────────
        [Header("Detection")]
        [SerializeField] private float sightRange       = 25f;
        [SerializeField] private float attackRange      = 12f;
        [SerializeField] private float fieldOfView      = 110f;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private LayerMask obstacleMask;

        // ── Movement ──────────────────────────────────────────────────────────
        [Header("Movement Speeds")]
        [SerializeField] private float patrolSpeed      = 2f;
        [SerializeField] private float chaseSpeed       = 5.5f;
        [SerializeField] private float patrolWaitTime   = 2f;

        // ── Attack ────────────────────────────────────────────────────────────
        [Header("Attack")]
        [SerializeField] private float attackDamage     = 10f;
        [SerializeField] private float attackRate       = 1.5f;   // attacks/sec
        [SerializeField] private float attackSpread     = 3f;     // accuracy spread degrees

        // ── Patrol ────────────────────────────────────────────────────────────
        [Header("Patrol")]
        [SerializeField] private Transform[] patrolPoints;

        // ── Audio & VFX ───────────────────────────────────────────────────────
        [Header("Audio")]
        [SerializeField] private AudioClip alertSound;
        [SerializeField] private AudioClip shootSound;
        [SerializeField] private AudioClip deathSound;

        [Header("VFX")]
        [SerializeField] private ParticleSystem muzzleFlash;

        // ── Internal ──────────────────────────────────────────────────────────
        private NavMeshAgent _agent;
        private AudioSource  _audio;
        private Transform    _player;
        private EnemyHealth  _health;

        private EnemyState   _state = EnemyState.Idle;
        private int          _patrolIndex;
        private float        _attackTimer;
        private bool         _playerVisible;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _agent  = GetComponent<NavMeshAgent>();
            _audio  = GetComponent<AudioSource>();
            _health = GetComponent<EnemyHealth>();

            // Cache player transform — assumes single player (extend for multiplayer)
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _player = playerObj.transform;
        }

        private void Update()
        {
            if (_state == EnemyState.Dead) return;

            _playerVisible = CanSeePlayer();
            _attackTimer  += Time.deltaTime;

            switch (_state)
            {
                case EnemyState.Idle:    UpdateIdle();    break;
                case EnemyState.Patrol:  UpdatePatrol();  break;
                case EnemyState.Chase:   UpdateChase();   break;
                case EnemyState.Attack:  UpdateAttack();  break;
            }
        }

        // ── States ────────────────────────────────────────────────────────────
        private void UpdateIdle()
        {
            if (_playerVisible)
                TransitionTo(EnemyState.Chase);
            else if (patrolPoints.Length > 0)
                TransitionTo(EnemyState.Patrol);
        }

        private void UpdatePatrol()
        {
            if (_playerVisible) { TransitionTo(EnemyState.Chase); return; }

            if (!_agent.pathPending && _agent.remainingDistance < 0.5f)
                StartCoroutine(WaitAtPatrolPoint());
        }

        private void UpdateChase()
        {
            if (!_playerVisible && _state == EnemyState.Chase)
            {
                // Lost sight — search at last known position a bit then give up
                // (basic: just go back to patrol)
                TransitionTo(EnemyState.Patrol);
                return;
            }

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist <= attackRange)
                TransitionTo(EnemyState.Attack);
            else
                _agent.SetDestination(_player.position);
        }

        private void UpdateAttack()
        {
            float dist = Vector3.Distance(transform.position, _player.position);

            if (dist > attackRange * 1.3f)
            {
                TransitionTo(EnemyState.Chase);
                return;
            }

            // Face the player
            Vector3 dir = (_player.position - transform.position).normalized;
            dir.y = 0f;
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);

            if (_attackTimer >= 1f / attackRate)
            {
                _attackTimer = 0f;
                ShootAtPlayer();
            }
        }

        // ── Shooting ──────────────────────────────────────────────────────────
        private void ShootAtPlayer()
        {
            if (muzzleFlash != null) muzzleFlash.Play();
            PlaySound(shootSound);

            // Apply accuracy spread
            Vector3 spread = new Vector3(
                UnityEngine.Random.Range(-attackSpread, attackSpread),
                UnityEngine.Random.Range(-attackSpread, attackSpread),
                0f);

            Vector3 dir = Quaternion.Euler(spread) *
                          (_player.position + Vector3.up * 1f - transform.position).normalized;

            Ray ray = new Ray(transform.position + Vector3.up * 1.5f, dir);
            if (Physics.Raycast(ray, out RaycastHit hit, attackRange * 1.5f))
            {
                IDamageable dmg = hit.collider.GetComponentInParent<IDamageable>();
                dmg?.TakeDamage(attackDamage, hit.point, dir);
            }
        }

        // ── Sight check ───────────────────────────────────────────────────────
        private bool CanSeePlayer()
        {
            if (_player == null) return false;

            Vector3 eyePos   = transform.position + Vector3.up * 1.6f;
            Vector3 toPlayer = _player.position   + Vector3.up * 1f - eyePos;
            float   dist     = toPlayer.magnitude;

            if (dist > sightRange) return false;

            float angle = Vector3.Angle(transform.forward, toPlayer);
            if (angle > fieldOfView * 0.5f) return false;

            // Line-of-sight raycast
            if (Physics.Raycast(eyePos, toPlayer.normalized, dist, obstacleMask))
                return false;

            return true;
        }

        // ── Transitions ───────────────────────────────────────────────────────
        private void TransitionTo(EnemyState newState)
        {
            if (_state == newState) return;
            _state = newState;

            _agent.isStopped = false;
            switch (newState)
            {
                case EnemyState.Patrol:
                    _agent.speed = patrolSpeed;
                    SetNextPatrolPoint();
                    break;
                case EnemyState.Chase:
                    _agent.speed = chaseSpeed;
                    PlaySound(alertSound);
                    break;
                case EnemyState.Attack:
                    _agent.isStopped = true;
                    break;
            }
        }

        public void OnDeath()
        {
            _state            = EnemyState.Dead;
            _agent.isStopped  = true;
            _agent.enabled    = false;
            PlaySound(deathSound);
        }

        // ── Patrol helpers ────────────────────────────────────────────────────
        private void SetNextPatrolPoint()
        {
            if (patrolPoints.Length == 0) return;
            _agent.SetDestination(patrolPoints[_patrolIndex].position);
            _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
        }

        private IEnumerator WaitAtPatrolPoint()
        {
            _agent.isStopped = true;
            yield return new WaitForSeconds(patrolWaitTime);
            _agent.isStopped = false;
            SetNextPatrolPoint();
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audio != null)
                _audio.PlayOneShot(clip);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, sightRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
