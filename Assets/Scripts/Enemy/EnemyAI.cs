using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using FreeWorld.Audio;
using FreeWorld.Utilities;

namespace FreeWorld.Enemy
{
    public enum EnemyState   { Idle, Patrol, Chase, Attack, TakingCover, Investigate, Flank, Dead }
    public enum EnemyVariant  { Grunt, Heavy, Scout, OriginalSkin }

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

        // ── Character model (optional — assign a CC0 humanoid FBX prefab) ────
        [Header("Character Model (CC0 — optional)")]
        [Tooltip("Assign a humanoid FBX prefab from Assets/Characters to replace the procedural body. " +
                 "Use FreeWorld → Setup → 5 - Set Up Character Models to import and configure a CC0 model.")]
        [SerializeField] private GameObject _characterModelPrefab;

        // ── Audio & VFX ───────────────────────────────────────────────────────
        [Header("Audio")]
        [SerializeField] private AudioClip alertSound;
        [SerializeField] private AudioClip shootSound;
        [SerializeField] private AudioClip deathSound;        [Tooltip("Shared audio bank (auto-found from Resources/WeaponAudioBank). Clips here override procedural synthesis.")]
        [SerializeField] private WeaponAudioBank audioBank;
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

        // ── Adaptive behaviour state ───────────────────────────────────────────
        private Vector3 _lastKnownPlayerPos;
        private float   _strafeDir    = 1f;   // +1 right, -1 left
        private float   _strafeTimer;
        private float   _flankTimer;
        private float   _searchTimer;
        private float   _reactionTimer;   // delay before first shot on entering Attack
        private float   _coverTimer;
        private float   _footstepTimer;

        /// <summary>Read by EnemyProceduralAnimator to select the right animation.</summary>
        public EnemyState CurrentState => _state;

        /// <summary>Intended movement velocity for animation. Always reflects the desired
        /// movement even when NavMesh clamps the actual displacement (e.g. strafe against wall).
        /// Updated every frame before EnemyModelAnimator.Update() reads it.</summary>
        public Vector3 AnimationVelocity { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _agent  = GetComponent<NavMeshAgent>();
            _audio  = GetComponent<AudioSource>();
            _health = GetComponent<EnemyHealth>();

            // 3D spatial audio — sounds come from the enemy's world position
            if (_audio != null)
            {
                _audio.spatialBlend = 1f;
                _audio.minDistance  = 1f;
                _audio.maxDistance  = 35f;
                _audio.rolloffMode  = AudioRolloffMode.Linear;
                _audio.playOnAwake  = false;
            }

            // Auto-load bank
            if (audioBank == null)
                audioBank = Resources.Load<WeaponAudioBank>("WeaponAudioBank");

            // Cache player transform — assumes single player (extend for multiplayer)
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _player = playerObj.transform;

            // Build humanoid body — real model path takes priority over procedural
            if (_characterModelPrefab != null)
            {
                // Instantiate CC0 model as a child of this root
                var modelGO = Object.Instantiate(_characterModelPrefab, transform);
                // Y = -1: capsule GO origin is at capsule centre; model root is at feet
                modelGO.transform.localPosition = new Vector3(0f, -1f, 0f);
                modelGO.transform.localRotation = Quaternion.identity;

                // Strip colliders from the model — root capsule handles all physics
                foreach (var col in modelGO.GetComponentsInChildren<Collider>())
                    Object.Destroy(col);

                var animator = modelGO.GetComponent<Animator>()
                            ?? modelGO.GetComponentInChildren<Animator>();

                if (animator != null && animator.isHuman)
                {
                    // Root motion fights NavMeshAgent — always disable it
                    animator.applyRootMotion = false;

                    _bodyParts = EnemyHumanoidBuilder.BuildFromAnimator(
                        transform, animator, new Color(0.15f, 0.28f, 0.12f));

                    // Add runtime animator wrapper; disable procedural animator
                    var modelAnim = gameObject.AddComponent<EnemyModelAnimator>();
                    modelAnim.Init(_bodyParts, animator);
                    var procAnim  = GetComponent<EnemyProceduralAnimator>();
                    if (procAnim != null) procAnim.enabled = false;

                    // Tighten NavMesh obstacle avoidance so it can't clip through walls
                    if (_agent != null)
                    {
                        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                        _agent.radius = 0.4f;
                    }
                }
                else
                {
                    Debug.LogWarning("[EnemyAI] Assigned model has no humanoid Animator — falling back to procedural humanoid.", gameObject);
                    _bodyParts = EnemyHumanoidBuilder.Build(transform, new Color(0.15f, 0.28f, 0.12f));
                    GetComponent<EnemyProceduralAnimator>()?.Init(_bodyParts);
                }
            }
            else
            {
                // Default procedural primitive body (no model assigned)
                _bodyParts = EnemyHumanoidBuilder.Build(transform, new Color(0.15f, 0.28f, 0.12f));
                GetComponent<EnemyProceduralAnimator>()?.Init(_bodyParts);
            }

            // Build gun and wire shooting module
            var gunVis   = EnemyGunBuilder.Build(_bodyParts.RightForeArm);
            _shootModule = GetComponent<EnemyShootingModule>();
            if (_shootModule != null)
            {
                _shootModule.Init(gunVis?.MuzzlePoint);
                // Prefer: Inspector field → bank clip → procedural (set in EnemyShootingModule)
                if (shootSound != null)
                    _shootModule.ShootAudioClip = shootSound;
                else if (audioBank == null)
                    audioBank = Resources.Load<WeaponAudioBank>("WeaponAudioBank");
            }

            if (_health != null)
                _health.OnDamaged += OnTookDamage;

            // Apply variant stats (uses Inspector value by default; overridden by SetVariant())
            ApplyVariant(variant);
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnDamaged -= OnTookDamage;
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

                case EnemyVariant.OriginalSkin:
                    _health?.Configure(100f, 200, "SWAT");
                    // No tint — original Mixamo textures unchanged
                    _shootModule?.Configure(12f, 1.5f, 3f, 1, 20, 2.0f, 16f);
                    break;

                default: // Grunt — standard battle rifle, original SWAT skin
                    // No tint — original Mixamo textures unchanged
                    _shootModule?.Configure(10f, 1.2f, 4f, 1, 20, 2.2f, 14f);
                    break;
            }
            if (_agent != null) _agent.speed = chaseSpeed;
        }

        private void SetRendererColor(Color c)
        {
            EnemyHumanoidBuilder.SetClothingColor(_bodyParts, c);
        }

        private void ClearRendererColorTint()
        {
            EnemyHumanoidBuilder.ClearClothingTint(_bodyParts);
        }

        private void Update()
        {
            if (_state == EnemyState.Dead) return;

            _playerVisible = CanSeePlayer();
            if (_playerVisible && _player != null)
                _lastKnownPlayerPos = _player.position;

            // Default: use NavMesh agent velocity (states that call SetDestination)
            AnimationVelocity = _agent != null ? _agent.velocity : Vector3.zero;

            switch (_state)
            {
                case EnemyState.Idle:        UpdateIdle();        break;
                case EnemyState.Patrol:      UpdatePatrol();      break;
                case EnemyState.Chase:       UpdateChase();       break;
                case EnemyState.Attack:      UpdateAttack();      break;
                case EnemyState.Investigate: UpdateInvestigate(); break;
                case EnemyState.Flank:       UpdateFlank();       break;
                case EnemyState.TakingCover: UpdateTakingCover(); break;
            }

            TickFootsteps();
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
            if (!_playerVisible)
            {
                TransitionTo(EnemyState.Investigate);
                return;
            }

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist <= attackRange)
            {
                TransitionTo(EnemyState.Attack);
                return;
            }

            // Periodically try to flank based on adaptive difficulty
            _flankTimer -= Time.deltaTime;
            if (_flankTimer <= 0f)
            {
                _strafeDir = Random.value > 0.5f ? 1f : -1f;
                TransitionTo(EnemyState.Flank);
                return;
            }

            _agent.SetDestination(_player.position);
        }

        private void UpdateAttack()
        {
            if (!_playerVisible)
            {
                TransitionTo(EnemyState.Investigate);
                return;
            }

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

            // Strafe sideways — changes direction every 1-2.5 s, makes enemy hard to hit
            _strafeTimer -= Time.deltaTime;
            if (_strafeTimer <= 0f)
            {
                _strafeDir   = Random.value > 0.5f ? 1f : -1f;
                _strafeTimer = Random.Range(1.0f, 2.5f);
            }
            // Override AnimationVelocity with the INTENDED strafe — bypasses NavMesh clamping
            AnimationVelocity = transform.right * (_strafeDir * 2.0f);
            _agent.Move(transform.right * (_strafeDir * 2.0f * Time.deltaTime));

            // Reaction delay before opening fire (shorter as difficulty rises)
            _reactionTimer -= Time.deltaTime;
            if (_reactionTimer <= 0f)
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
            var adapt = EnemyAdaptiveSystem.Instance;
            switch (newState)
            {
                case EnemyState.Patrol:
                    _agent.speed = patrolSpeed;
                    SetNextPatrolPoint();
                    break;
                case EnemyState.Chase:
                    _agent.speed = chaseSpeed * (adapt?.ChaseSpeedMult ?? 1f);
                    _flankTimer  = adapt?.FlankInterval ?? 12f;
                    PlayAlertSound();
                    break;
                case EnemyState.Attack:
                    _agent.ResetPath();          // stop pathing; Move() still works (isStopped=true blocks Move)
                    _strafeTimer     = Random.Range(0.4f, 1.2f);
                    _reactionTimer   = adapt?.ReactionDelay ?? 0.5f;
                    break;
                case EnemyState.Investigate:
                    _agent.speed = chaseSpeed * 0.75f;
                    _searchTimer = 3.0f;
                    _agent.SetDestination(_lastKnownPlayerPos);
                    break;
                case EnemyState.Flank:
                    _agent.speed = chaseSpeed * (adapt?.ChaseSpeedMult ?? 1f);
                    _agent.SetDestination(ComputeFlankPosition());
                    break;
                case EnemyState.TakingCover:
                    _agent.speed = chaseSpeed * 1.2f;
                    _coverTimer  = Random.Range(2.0f, 4.0f);
                    SeekCover();
                    break;
            }
        }

        public void OnDeath()
        {
            _state            = EnemyState.Dead;
            _agent.isStopped  = true;
            _agent.enabled    = false;

            if (_audio == null) return;
            if (deathSound != null) { _audio.PlayOneShot(deathSound); return; }
            var clip = WeaponAudioBank.Pick(audioBank?.EnemyDeath);
            if (clip != null) _audio.PlayOneShot(clip);
            else Utilities.ProceduralAudioLibrary.Play(_audio, Utilities.ProceduralAudioLibrary.ClipEnemyDeath, 0.9f);
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

        private void PlayAlertSound()
        {
            if (_audio == null) return;
            if (alertSound != null)
            {
                _audio.PlayOneShot(alertSound);
                return;
            }
            if (audioBank == null)
                audioBank = Resources.Load<WeaponAudioBank>("WeaponAudioBank");
            var clip = WeaponAudioBank.Pick(audioBank?.EnemyAlert);
            if (clip != null)
                _audio.PlayOneShot(clip);
            else
                Utilities.ProceduralAudioLibrary.Play(
                    _audio, Utilities.ProceduralAudioLibrary.ClipEnemyAlert, 0.65f);
        }

        private void TickFootsteps()
        {
            if (_audio == null) return;

            float speed = _agent.velocity.magnitude;
            if (speed < 0.4f) return;   // standing still — no footsteps

            // Interval: slower patrol cadence, faster chase cadence
            float interval = (_state == EnemyState.Patrol || _state == EnemyState.Investigate)
                             ? 0.52f : 0.32f;

            _footstepTimer -= Time.deltaTime;
            if (_footstepTimer > 0f) return;

            _footstepTimer = interval;

            float vol = Mathf.Lerp(0.18f, 0.38f, Mathf.InverseLerp(patrolSpeed, chaseSpeed, speed));

            var clip = WeaponAudioBank.Pick(audioBank?.Footstep);
            if (clip != null)
                _audio.PlayOneShot(clip, vol);
            else
                Utilities.ProceduralAudioLibrary.Play(
                    _audio, Utilities.ProceduralAudioLibrary.ClipFootstep, vol);
        }
        // ── New behaviour states ─────────────────────────────────────────────────────
        private void UpdateInvestigate()
        {
            // Regain sight? Return to chase immediately
            if (_playerVisible) { TransitionTo(EnemyState.Chase); return; }

            // Still en route to last known position
            if (_agent.pathPending || _agent.remainingDistance >= 1.0f) return;

            // Arrived — look around for a few seconds before giving up
            _searchTimer -= Time.deltaTime;
            if (_searchTimer <= 0f)
                TransitionTo(patrolPoints.Length > 0 ? EnemyState.Patrol : EnemyState.Idle);
        }

        private void UpdateFlank()
        {
            if (_playerVisible &&
                Vector3.Distance(transform.position, _player.position) <= attackRange)
            {
                TransitionTo(EnemyState.Attack);
                return;
            }
            if (!_agent.pathPending && _agent.remainingDistance < 1.2f)
                TransitionTo(EnemyState.Chase);
        }

        private void UpdateTakingCover()
        {
            if (_agent.pathPending || _agent.remainingDistance >= 0.8f) return;

            _coverTimer -= Time.deltaTime;
            if (_coverTimer <= 0f)
                TransitionTo(_playerVisible ? EnemyState.Attack : EnemyState.Chase);
        }

        private Vector3 ComputeFlankPosition()
        {
            if (_player == null) return transform.position;
            // Step to the player's side, slightly behind their facing
            Vector3 side      = (_strafeDir >= 0 ? _player.right : -_player.right);
            Vector3 candidate = _player.position + (side + _player.forward * 0.25f).normalized
                                                  * (attackRange * 0.8f);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                return hit.position;
            return _player.position - _player.forward * 3f;  // fallback: directly behind
        }

        private void SeekCover()
        {
            if (_player == null) return;
            Vector3 away  = (transform.position - _player.position).normalized;
            Vector3 spot  = transform.position + away * Random.Range(4f, 8f)
                            + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        private void OnTookDamage()
        {
            if (_state == EnemyState.Dead || _state == EnemyState.TakingCover) return;
            // 35 % chance to break and dive for cover when hit
            if (Random.value < 0.35f)
                TransitionTo(EnemyState.TakingCover);
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
