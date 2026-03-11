using UnityEngine;
using UnityEngine.AI;
using FreeWorld.Utilities;

namespace FreeWorld.Enemy
{
    /// <summary>
    /// Animator-backed movement/combat animation for real humanoid character models.
    /// Drop alongside EnemyAI on the same GameObject — the Animator component lives
    /// on the imported character model that is a child of this root.
    ///
    /// Replaces EnemyProceduralAnimator when a CC0/imported model is in use.
    /// Falls back gracefully if no Animator is found (procedural side takes over).
    ///
    /// Required Animator parameters (create with exact names + types):
    ///   Speed      (Float)  — world-space velocity magnitude
    ///   IsAttacking(Bool)   — enemy is in Attack state
    ///   IsReloading(Bool)   — shooting module is reloading
    ///   IsDead     (Bool)   — triggers death clip, disables Update
    /// </summary>
    public class EnemyModelAnimator : MonoBehaviour
    {
        // ── Animator parameter hashes (pre-computed, no GC) ──────────────────
        private static readonly int MoveXHash    = Animator.StringToHash("MoveX");
        private static readonly int MoveZHash    = Animator.StringToHash("MoveZ");
        private static readonly int SpeedHash    = Animator.StringToHash("Speed");
        private static readonly int AttackHash   = Animator.StringToHash("IsAttacking");
        private static readonly int ReloadHash   = Animator.StringToHash("IsReloading");
        private static readonly int DeadHash     = Animator.StringToHash("IsDead");

        private Animator             _animator;
        private NavMeshAgent         _agent;
        private EnemyHealth          _health;
        private EnemyShootingModule  _shootModule;
        private EnemyAI              _ai;

        // ── Initialise — called from EnemyAI.Awake after building body parts ─
        /// <summary>
        /// Mirror of EnemyProceduralAnimator.Init so EnemyAI can call the same
        /// signature regardless of which animator component is active.
        /// </summary>
        public void Init(EnemyBodyParts parts, Animator animator = null)
        {
            // Prefer the animator handed in by EnemyAI; fall back to child search
            _animator    = animator != null ? animator : GetComponentInChildren<Animator>();
            _agent       = GetComponent<NavMeshAgent>();
            _health      = GetComponent<EnemyHealth>();
            _shootModule = GetComponent<EnemyShootingModule>();
            _ai          = GetComponent<EnemyAI>();

            if (_animator == null)
            {
                Debug.LogWarning("[EnemyModelAnimator] No Animator found — make sure the model has an Animator component.", gameObject);
            }
            else if (_animator.runtimeAnimatorController == null)
            {
                // Self-heal: try loading from Resources (mirrored there by CharacterModelImporter)
                var fallback = Resources.Load<RuntimeAnimatorController>("EnemyAnimator");
                if (fallback != null)
                {
                    _animator.runtimeAnimatorController = fallback;
                }
                else
                {
                    Debug.LogWarning("[EnemyModelAnimator] Animator has no AnimatorController assigned. Re-run FreeWorld > Setup > 5.", gameObject);
                }
            }
        }

        // ── Per-frame parameter push ──────────────────────────────────────────
        // LateUpdate guarantees this runs AFTER EnemyAI.Update() has set AnimationVelocity.
        private void LateUpdate()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            if (_health != null && !_health.IsAlive)
            {
                _animator.SetBool(DeadHash, true);
                enabled = false;
                return;
            }

            // Use EnemyAI's explicit AnimationVelocity — never zero due to NavMesh clamping.
            // NavMeshAgent.velocity is 0 during strafe (agent.Move bypasses the velocity field).
            Vector3 worldVel = (_ai != null) ? _ai.AnimationVelocity : Vector3.zero;
            float   speed    = worldVel.magnitude;

            float moveX = 0f, moveZ = 0f;
            if (speed > 0.01f)
            {
                const float k_StrafeRef = 2.2f;
                float       runRef      = _agent != null ? Mathf.Max(_agent.speed, 0.1f) : k_StrafeRef;
                Vector3     local       = transform.InverseTransformDirection(worldVel);
                moveX = Mathf.Clamp(local.x / k_StrafeRef, -1f, 1f);
                moveZ = Mathf.Clamp(local.z / runRef,       -1f, 1f);
            }

            bool attacking = _ai    != null && _ai.CurrentState == EnemyState.Attack;
            bool reloading = _shootModule != null && _shootModule.IsReloading;

            // Drive upper-body layer weight: 0 = full locomotion only, 1 = aim overrides torso/arms
            _animator.SetLayerWeight(1, attacking ? 1f : 0f);

            _animator.SetFloat(MoveXHash,  moveX,  0.1f, Time.deltaTime);
            _animator.SetFloat(MoveZHash,  moveZ,  0.1f, Time.deltaTime);
            _animator.SetFloat(SpeedHash,  speed,  0.12f, Time.deltaTime);
            _animator.SetBool (AttackHash,  attacking);
            _animator.SetBool (ReloadHash,  reloading);
        }
    }
}
