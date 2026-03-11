using FreeWorld.Managers;
using UnityEngine;
using System.Collections;

namespace FreeWorld.Player
{
    /// <summary>
    /// Handles FPS player movement: walk, sprint, crouch, jump.
    /// Attach to the Player root GameObject alongside a CharacterController.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed    = 5f;
        [SerializeField] private float sprintSpeed  = 9f;
        [SerializeField] private float crouchSpeed  = 2.5f;

        [Header("Jump & Gravity")]
        [SerializeField] private float jumpHeight   = 1.2f;
        [SerializeField] private float gravity      = -19.62f;

        [Header("Crouch Settings")]
        [SerializeField] private float standHeight  = 1.8f;
        [SerializeField] private float crouchHeight = 0.9f;
        [SerializeField] private float crouchTransitionSpeed = 10f;

        [Header("Slope Handling")]
        [SerializeField] [Range(30f, 89f)] private float slopeLimit = 80f;
        [SerializeField] [Min(0f)] private float stepOffset = 0.35f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundDistance = 0.3f;
        [SerializeField] private LayerMask groundMask;

        [Header("Footsteps")]
        [SerializeField] private AudioClip[] footstepClips;
        [SerializeField] private float walkStepInterval   = 0.5f;
        [SerializeField] private float sprintStepInterval = 0.3f;
        [SerializeField] private float crouchStepInterval = 0.7f;
        [SerializeField] [Range(0f, 1f)] private float footstepVolume = 0.4f;

        [Header("Exhaustion")]
        [SerializeField] [Range(0f, 1f)] private float exhaustedSpeedMultiplier = 0.55f; // walk speed penalty when stamina = 0

        // ── Internal state ──────────────────────────────────────────────
        private CharacterController _cc;
        private PlayerVitals        _vitals;
        private PlayerStats         _stats;
        private Vector3             _velocity;
        private bool                _isGrounded;
        private bool                _isCrouching;
        private float               _targetHeight;

        // Footstep state
        private AudioSource _footstepAudio;
        private float _stepTimer;

        public bool  IsSprinting  { get; private set; }
        // Exposed for other systems (camera bob, UI, etc.)
        public bool  IsCrouching  => _isCrouching;
        public bool  IsGrounded   => _isGrounded;
        public float CurrentSpeed { get; private set; }

        // ────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _cc     = GetComponent<CharacterController>();

            // Make hill climbing deterministic regardless of prefab defaults.
            _cc.slopeLimit = slopeLimit;
            _cc.stepOffset = stepOffset;

            // Get existing PlayerVitals, or add it now so it's always ready before Update()
            _vitals = GetComponent<PlayerVitals>() ?? gameObject.AddComponent<PlayerVitals>();
            _stats  = GetComponent<PlayerStats>()  ?? gameObject.AddComponent<PlayerStats>();
            _targetHeight = standHeight;
            _footstepAudio = gameObject.AddComponent<AudioSource>();
            _footstepAudio.spatialBlend = 0f;
            _footstepAudio.playOnAwake  = false;
        }

        private void Update()
        {
            // Freeze all input while the game is not in the Playing state
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameState.Playing) return;

            CheckGround();
            HandleCrouch();
            HandleMovement();
            ApplyGravity();
            HandleFootsteps();
        }

        private void Start()
        {
            StartCoroutine(EnsureSpawnOnGround());
        }

        private IEnumerator EnsureSpawnOnGround()
        {
            // Wait a frame so ProceduralTerrain generation and collider sync finish.
            yield return null;

            var terrain = Terrain.activeTerrain;
            if (terrain == null) yield break;

            Vector3 p = transform.position;
            float terrainY = terrain.SampleHeight(p) + terrain.transform.position.y;
            float targetY = terrainY + (_cc.height * 0.5f) + 0.05f;

            if (p.y < targetY)
            {
                _cc.enabled = false;
                transform.position = new Vector3(p.x, targetY, p.z);
                _cc.enabled = true;
                _velocity = Vector3.zero;
            }
        }

        // ── Ground detection ─────────────────────────────────────────────────
        private void CheckGround()
        {
            _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

            if (_isGrounded && _velocity.y < 0f)
                _velocity.y = -2f;   // keep grounded firmly
        }

        // ── Movement ─────────────────────────────────────────────────────────
        private void HandleMovement()
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            // Lazy re-fetch in case component was added after Awake (edge case)
            if (_vitals == null) _vitals = GetComponent<PlayerVitals>();

            bool exhausted      = _vitals != null && !_vitals.CanSprint;
            bool wantsToSprint  = Input.GetKey(KeyCode.LeftShift) && !_isCrouching && v > 0.1f;
            IsSprinting = wantsToSprint && !exhausted;

            float statMult = _stats != null ? _stats.SpeedMultiplier : 1f;
            float speed = _isCrouching ? crouchSpeed
                        : IsSprinting  ? sprintSpeed  * statMult
                        : exhausted    ? walkSpeed * exhaustedSpeedMultiplier
                        : walkSpeed * statMult;

            CurrentSpeed = (Mathf.Abs(h) + Mathf.Abs(v)) > 0.1f ? speed : 0f;

            Vector3 move = transform.right * h + transform.forward * v;
            _cc.Move(move.normalized * speed * Time.deltaTime);

            // Track distance for Speed XP
            if (CurrentSpeed > 0.1f)
                _stats?.TrackMovement(speed * Time.deltaTime);

            if (Input.GetButtonDown("Jump") && _isGrounded && !_isCrouching)
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // ── Gravity ───────────────────────────────────────────────────────────
        private void ApplyGravity()
        {
            _velocity.y += gravity * Time.deltaTime;
            _cc.Move(_velocity * Time.deltaTime);
        }

        // ── Crouch ────────────────────────────────────────────────────────────
        private void HandleCrouch()
        {
            if (Input.GetKeyDown(KeyCode.LeftControl))
                _isCrouching = !_isCrouching;

            _targetHeight = _isCrouching ? crouchHeight : standHeight;
            _cc.height = Mathf.Lerp(_cc.height, _targetHeight, crouchTransitionSpeed * Time.deltaTime);

            // Keep character controller center aligned with height change
            _cc.center = new Vector3(0f, _cc.height / 2f, 0f);
        }

        // ── Footsteps ─────────────────────────────────────────────────────────
        private void HandleFootsteps()
        {
            if (!_isGrounded || CurrentSpeed < 0.1f)
            {
                _stepTimer = 0f;
                return;
            }

            float interval = _isCrouching ? crouchStepInterval
                           : IsSprinting  ? sprintStepInterval
                           : walkStepInterval;

            _stepTimer += Time.deltaTime;
            if (_stepTimer >= interval)
            {
                _stepTimer = 0f;

                // Use assigned clips if available, otherwise fall back to procedural
                if (footstepClips != null && footstepClips.Length > 0)
                    _footstepAudio.PlayOneShot(
                        footstepClips[Random.Range(0, footstepClips.Length)], footstepVolume);
                else
                    Utilities.ProceduralAudioLibrary.Play(
                        _footstepAudio, Utilities.ProceduralAudioLibrary.ClipFootstep,
                        footstepVolume * (IsSprinting ? 1f : 0.7f));
            }
        }
    }
}
