using UnityEngine;
using UnityEngine.AI;

namespace FreeWorld.Enemy
{
    /// <summary>
    /// Procedural animation for the multi-part humanoid enemy body.
    /// Drives walking (arm/leg swing, body bob) and idle breathing.
    /// Plays an attack lean when swinging at the player.
    /// </summary>
    public class EnemyProceduralAnimator : MonoBehaviour
    {
        private Utilities.EnemyBodyParts _parts;
        private NavMeshAgent             _agent;
        private EnemyHealth              _health;
        private EnemyShootingModule      _shootModule;

        // Animation state
        private float _walkCycle;
        private float _breathCycle;

        // Cached rest rotations
        private static readonly Quaternion ArmRestL  = Quaternion.Euler(  0f, 0f, -22f);
        private static readonly Quaternion ArmRestR  = Quaternion.Euler(  0f, 0f,  22f);
        private static readonly Quaternion LegRest   = Quaternion.identity;

        // Gun-aim pose — shoulder 60° + elbow 30° = 90° → barrel points world-forward
        private static readonly Quaternion GunAimUpperR = Quaternion.Euler(60f,  0f,  -8f);
        private static readonly Quaternion GunAimLowerR = Quaternion.Euler(30f,  0f,   0f);
        private static readonly Quaternion GunAimUpperL = Quaternion.Euler(62f,  0f,  16f);
        private static readonly Quaternion GunAimLowerL = Quaternion.Euler(28f,  0f,   0f);

        // Reload pose — right arm drops, left hand supports
        private static readonly Quaternion ReloadUpperR = Quaternion.Euler(45f, -30f, -12f);
        private static readonly Quaternion ReloadLowerR = Quaternion.Euler(80f,   0f,   0f);
        private static readonly Quaternion ReloadUpperL = Quaternion.Euler(55f,  20f,  12f);
        private static readonly Quaternion ReloadLowerL = Quaternion.Euler(40f,   0f,   0f);

        // ── Initialise (called from EnemyAI.Awake) ────────────────────────────
        public void Init(Utilities.EnemyBodyParts parts)
        {
            _parts       = parts;
            _agent       = GetComponent<NavMeshAgent>();
            _health      = GetComponent<EnemyHealth>();
            _shootModule = GetComponent<EnemyShootingModule>();
        }

        // ── Main update ───────────────────────────────────────────────────────
        private void Update()
        {
            if (_parts?.Body == null) return;
            if (_health != null && !_health.IsAlive)
            {
                PlayDeathPose();
                enabled = false;
                return;
            }

            float speed   = _agent != null ? _agent.velocity.magnitude : 0f;
            var   ai      = GetComponent<EnemyAI>();
            bool  attacking = ai != null && ai.CurrentState == EnemyState.Attack;

            if (speed > 0.15f)
                AnimateWalk(speed, attacking);
            else if (attacking)
                AnimateAttackIdle();
            else
                AnimateIdle();
        }

        // ── Walk ──────────────────────────────────────────────────────────────
        private void AnimateWalk(float speed, bool attacking)
        {
            _walkCycle += Time.deltaTime * speed * 2.8f;

            float legSwing   = Mathf.Sin(_walkCycle)                           * 28f;
            float lowerBend  = Mathf.Max(0f, Mathf.Sin(_walkCycle + 0.6f))    * 35f;
            float lowerBendR = Mathf.Max(0f, Mathf.Sin(_walkCycle + 0.6f + Mathf.PI)) * 35f;

            // Legs — opposite phases
            SetRot(_parts.LeftUpperLeg,  Quaternion.Euler( legSwing, 0f, 0f));
            SetRot(_parts.RightUpperLeg, Quaternion.Euler(-legSwing, 0f, 0f));
            SetRot(_parts.LeftLowerLeg,  Quaternion.Euler(lowerBend,  0f, 0f));
            SetRot(_parts.RightLowerLeg, Quaternion.Euler(lowerBendR, 0f, 0f));

            // Arms — gun hold when attacking, natural swing otherwise
            if (attacking)
            {
                SetRot(_parts.LeftUpperArm,  GunAimUpperL);
                SetRot(_parts.RightUpperArm, GunAimUpperR);
                SetRot(_parts.LeftForeArm,   GunAimLowerL);
                SetRot(_parts.RightForeArm,  GunAimLowerR);
            }
            else
            {
                float armSwing = Mathf.Sin(_walkCycle) * 22f;
                SetRot(_parts.LeftUpperArm,  Quaternion.Euler(-armSwing, 0f, -22f));
                SetRot(_parts.RightUpperArm, Quaternion.Euler( armSwing, 0f,  22f));
                SetRot(_parts.LeftForeArm,   Quaternion.identity);
                SetRot(_parts.RightForeArm,  Quaternion.identity);
            }

            // Body bob
            if (_parts.Body != null)
            {
                float bob = Mathf.Abs(Mathf.Sin(_walkCycle)) * 0.035f;
                _parts.Body.localPosition = new Vector3(0f, -1f + bob, 0f);
            }

            // Torso slight lean toward player when chasing
            if (_parts.Torso != null)
                _parts.Torso.localRotation = Quaternion.Euler(12f, 0f, 0f);
        }

        // ── Attack idle: gun aimed at player ────────────────────────────────
        private void AnimateAttackIdle()
        {
            float t        = Time.deltaTime * 10f;
            bool reloading = _shootModule != null && _shootModule.IsReloading;

            if (reloading)
            {
                // Right arm drops to change mag; left arm stays up supporting
                SetRot(_parts.RightUpperArm, Quaternion.Slerp(_parts.RightUpperArm.localRotation, ReloadUpperR, t));
                SetRot(_parts.RightForeArm,  Quaternion.Slerp(_parts.RightForeArm.localRotation,  ReloadLowerR, t));
                SetRot(_parts.LeftUpperArm,  Quaternion.Slerp(_parts.LeftUpperArm.localRotation,  ReloadUpperL, t));
                SetRot(_parts.LeftForeArm,   Quaternion.Slerp(_parts.LeftForeArm.localRotation,   ReloadLowerL, t));
            }
            else
            {
                // Both arms raise into gun-aim pose
                SetRot(_parts.RightUpperArm, Quaternion.Slerp(_parts.RightUpperArm.localRotation, GunAimUpperR, t));
                SetRot(_parts.RightForeArm,  Quaternion.Slerp(_parts.RightForeArm.localRotation,  GunAimLowerR, t));
                SetRot(_parts.LeftUpperArm,  Quaternion.Slerp(_parts.LeftUpperArm.localRotation,  GunAimUpperL, t));
                SetRot(_parts.LeftForeArm,   Quaternion.Slerp(_parts.LeftForeArm.localRotation,   GunAimLowerL, t));
            }

            // Legs at rest
            SetRot(_parts.LeftUpperLeg,  LegRest);
            SetRot(_parts.RightUpperLeg, LegRest);
            SetRot(_parts.LeftLowerLeg,  LegRest);
            SetRot(_parts.RightLowerLeg, LegRest);

            if (_parts.Torso != null)
                _parts.Torso.localRotation = Quaternion.Euler(10f, 0f, 0f);
        }

        // ── Idle breathe ──────────────────────────────────────────────────────
        private void AnimateIdle()
        {
            _breathCycle += Time.deltaTime * 1.1f;
            float breath   = Mathf.Sin(_breathCycle) * 0.008f;

            // Upper arms hang at sides; forearms straight
            SetRot(_parts.LeftUpperArm,  Quaternion.Slerp(_parts.LeftUpperArm  != null
                ? _parts.LeftUpperArm.localRotation  : ArmRestL, ArmRestL, Time.deltaTime * 4f));
            SetRot(_parts.RightUpperArm, Quaternion.Slerp(_parts.RightUpperArm != null
                ? _parts.RightUpperArm.localRotation : ArmRestR, ArmRestR, Time.deltaTime * 4f));
            SetRot(_parts.LeftForeArm,   Quaternion.identity);
            SetRot(_parts.RightForeArm,  Quaternion.identity);

            // Reset legs
            SetRot(_parts.LeftUpperLeg,  LegRest);
            SetRot(_parts.RightUpperLeg, LegRest);
            SetRot(_parts.LeftLowerLeg,  LegRest);
            SetRot(_parts.RightLowerLeg, LegRest);

            // Subtle chest scale breathing
            if (_parts.Torso != null)
            {
                var s = _parts.Torso.localScale;
                _parts.Torso.localScale    = new Vector3(s.x, 0.52f + breath, s.z);
                _parts.Torso.localRotation = Quaternion.Euler(Mathf.Sin(_breathCycle * 0.3f) * 2f, 0f, 0f);
            }

            // Body neutral
            if (_parts.Body != null)
                _parts.Body.localPosition = Vector3.Lerp(_parts.Body.localPosition,
                    new Vector3(0f, -1f, 0f), Time.deltaTime * 4f);
        }

        // ── Death ─────────────────────────────────────────────────────────────
        private void PlayDeathPose()
        {
            // Slump arms down, lean torso
            SetRot(_parts.LeftUpperArm,  Quaternion.Euler( 30f, 0f, -70f));
            SetRot(_parts.RightUpperArm, Quaternion.Euler(-20f, 0f,  70f));
            if (_parts.Torso != null)
                _parts.Torso.localRotation = Quaternion.Euler(40f, 0f, 0f);
        }

        private static void SetRot(Transform t, Quaternion rot)
        {
            if (t != null) t.localRotation = rot;
        }
    }
}
