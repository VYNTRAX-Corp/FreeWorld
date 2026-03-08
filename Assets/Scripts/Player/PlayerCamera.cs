using FreeWorld.Managers;
using UnityEngine;

namespace FreeWorld.Player
{
    /// <summary>
    /// FPS mouse-look camera with head bob and recoil support.
    /// Attach to the Camera GameObject (child of Player root).
    /// </summary>
    public class PlayerCamera : MonoBehaviour
    {
        [Header("Look Sensitivity")]
        [SerializeField] private float mouseSensitivity = 200f;
        [SerializeField] private float verticalClamp    = 85f;

        [Header("Head Bob")]
        [SerializeField] private bool  headBobEnabled   = true;
        [SerializeField] private float bobFrequency     = 12f;
        [SerializeField] private float bobAmplitude     = 0.05f;

        [Header("Recoil Recovery")]
        [SerializeField] private float recoilRecoverySpeed = 8f;

        // ── References ────────────────────────────────────────────────────────
        [SerializeField] private Transform playerBody;   // the root Player transform

        // ── Internal state ────────────────────────────────────────────────────
        private float   _xRotation;
        private float   _bobTimer;
        private Vector3 _baseLocalPosition;

        // Recoil accumulator — add to this from WeaponBase
        private float _recoilX;
        private float _recoilY;

        // Explosion shake
        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeTimer;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            _baseLocalPosition = transform.localPosition;
        }

        private void Update()
        {
            // Freeze look while not playing (pause menu, game over, etc.)
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameState.Playing)
            {
                RecoverRecoil();
                return;
            }

            HandleLook();
            if (headBobEnabled) HandleHeadBob();
            RecoverRecoil();
            ApplyExplosionShake();
        }

        // ── Mouse look ───────────────────────────────────────────────────────
        private void HandleLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            // Apply recoil on top of look input
            _xRotation -= mouseY + _recoilX;
            _xRotation  = Mathf.Clamp(_xRotation, -verticalClamp, verticalClamp);

            transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
            playerBody.Rotate(Vector3.up * (mouseX + _recoilY));
        }

        // ── Head bob ─────────────────────────────────────────────────────────
        private void HandleHeadBob()
        {
            PlayerController pc = playerBody.GetComponent<PlayerController>();
            if (pc == null || !pc.IsGrounded || pc.CurrentSpeed < 0.1f)
            {
                // Smoothly return to base position when not moving
                transform.localPosition = Vector3.Lerp(
                    transform.localPosition, _baseLocalPosition, 10f * Time.deltaTime);
                _bobTimer = 0f;
                return;
            }

            float speedFactor = pc.IsSprinting ? 1.4f : 1f;
            _bobTimer += Time.deltaTime * bobFrequency * speedFactor;

            Vector3 bob = new Vector3(
                Mathf.Cos(_bobTimer * 0.5f) * bobAmplitude,
                Mathf.Sin(_bobTimer)        * bobAmplitude,
                0f);

            transform.localPosition = Vector3.Lerp(
                transform.localPosition, _baseLocalPosition + bob, 15f * Time.deltaTime);
        }

        // ── Recoil ────────────────────────────────────────────────────────────
        private void RecoverRecoil()
        {
            _recoilX = Mathf.Lerp(_recoilX, 0f, recoilRecoverySpeed * Time.deltaTime);
            _recoilY = Mathf.Lerp(_recoilY, 0f, recoilRecoverySpeed * Time.deltaTime);
        }

        /// <summary>Call this from WeaponBase when a shot is fired.</summary>
        public void ApplyRecoil(float vertical, float horizontal)
        {
            _recoilX += vertical;
            _recoilY += Random.Range(-horizontal, horizontal);
        }

        /// <summary>Toggle cursor lock (pause menu).</summary>
        public void SetCursorLock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible   = !locked;
        }

        /// <summary>Called by SettingsManager when sensitivity changes at runtime.</summary>
        public void SetSensitivity(float value)
        {
            mouseSensitivity = value;
        }

        // ── Explosion shake ──────────────────────────────────────────────────
        public void StartExplosionShake(float intensity = 0.15f, float duration = 0.4f)
        {
            _shakeIntensity = intensity;
            _shakeDuration  = duration;
            _shakeTimer     = 0f;
        }

        private void ApplyExplosionShake()
        {
            if (_shakeTimer >= _shakeDuration) return;

            _shakeTimer += Time.deltaTime;
            float t       = 1f - (_shakeTimer / _shakeDuration);
            Vector3 shake = Random.insideUnitSphere * (_shakeIntensity * t);
            transform.localPosition = _baseLocalPosition + shake;
        }
    }
}
