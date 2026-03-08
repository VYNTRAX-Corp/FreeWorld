using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using FreeWorld.Utilities;

namespace FreeWorld.Enemy
{
    public enum EnemyState   { Idle, Patrol, Chase, Attack, TakingCover, Dead }
    public enum EnemyVariant  { Grunt, Heavy, Scout }

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

        // ── Variant ───────────────────────────────────────────────────────────────
        [Header("Variant")]
        [SerializeField] private EnemyVariant variant   = EnemyVariant.Grunt;
        [SerializeField] private Transform[] patrolPoints;

        // ── Audio & VFX ───────────────────────────────────────────────────────
        [Header("Audio")]
        [SerializeField] private AudioClip alertSound;
        [SerializeField] private AudioClip shootSound;
        [SerializeField] private AudioClip deathSound;

        // ── Internal ──────────────────────────────────────────────────────────
        private NavMeshAgent _agent;
        private AudioSource  _audio;
        private Transform    _player;
        private EnemyHealth  _health;

        private EnemyState           _state = EnemyState.Idle;
        private int                   _patrolIndex;
        private bool                  _playerVisible;
        private EnemyBodyParts        _bodyParts;
        private EnemyShootingModule   _shootModule;

        /// <summary>Read by EnemyProceduralAnimator to select the right animation.</summary>
        public EnemyState CurrentState => _state;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _agent  = GetComponent<NavMeshAgent>();
            _audio  = GetComponent<AudioSource>();
            _health = GetComponent<EnemyHealth>();

            // Cache player transform — assumes single player (extend for multiplayer)
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _player = playerObj.transform;

            // Build procedural humanoid body (before ApplyVariant so color can be set)
            _bodyParts = EnemyHumanoidBuilder.Build(transform, new Color(0.15f, 0.28f, 0.12f));
            GetComponent<EnemyProceduralAnimator>()?.Init(_bodyParts);

            // Build gun and wire shooting module
            var gunVis   = EnemyGunBuilder.Build(_bodyParts.RightForeArm);
            _shootModule = GetComponent<EnemyShootingModule>();
            if (_shootModule != null)
            {
                _shootModule.Init(gunVis?.MuzzlePoint);
                _shootModule.ShootAudioClip = shootSound;
            }

            // Apply variant stats (uses Inspector value by default; overridden by SetVariant())
            ApplyVariant(variant);
        }

        /// <summary>Called by EnemySpawner to override the default Grunt stats.</summary>
        public void SetVariant(EnemyVariant v)
        {
            variant = v;
            ApplyVariant(v);
        }

        private void ApplyVariant(EnemyVariant v)
        {
            switch (v)
            {
                case EnemyVariant.Heavy:
                    chaseSpeed = 3.0f;
                    _health?.Configure(280f, 250, "HEAVY");
                    SetRendererColor(new Color(0.18f, 0.28f, 0.85f));  // blue
                    // 3-round burst, hard-hitting, wide spread, short range
                    _shootModule?.Configure(28f, 0.7f, 8f, 3, 12, 3.0f, 10f);
                    break;

                case EnemyVariant.Scout:
                    chaseSpeed = 9.5f;
                    sightRange = 32f;
                    _health?.Configure(55f, 150, "SCOUT");
                    SetRendererColor(new Color(0.85f, 0.80f, 0.08f));  // yellow
                    // Fast semi-auto, precise, long range
                    _shootModule?.Configure(7f, 3.0f, 2f, 1, 30, 1.8f, 22f);
                    break;

                default: // Grunt — standard battle rifle
                    SetRendererColor(new Color(0.15f, 0.28f, 0.12f));
                    _shootModule?.Configure(10f, 1.2f, 4f, 1, 20, 2.2f, 14f);
                    break;
            }
            if (_agent != null) _agent.speed = chaseSpeed;
        }

        private void SetRendererColor(Color c)
        {
            EnemyHumanoidBuilder.SetClothingColor(_bodyParts, c);
        }

        private void Update()
        {
            if (_state == EnemyState.Dead) return;

            _playerVisible = CanSeePlayer();

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

            _shootModule?.TryShoot(_player);
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
