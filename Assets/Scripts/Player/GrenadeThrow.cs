using UnityEngine;

namespace FreeWorld.Player
{
    /// <summary>
    /// Grenade throwing. Press G to cook and release to throw.
    /// Attach to the Player root alongside PlayerController.
    /// </summary>
    public class GrenadeThrow : MonoBehaviour
    {
        [Header("Throw Settings")]
        [SerializeField] private GameObject grenadePrefab;
        [SerializeField] private float      minThrowForce  = 8f;
        [SerializeField] private float      maxThrowForce  = 22f;
        [SerializeField] private float      maxCookTime    = 2f;   // how long G can be held
        [SerializeField] private int        maxGrenades    = 2;

        [Header("Throw Origin")]
        [SerializeField] private Transform  throwOrigin;   // set to FPSCamera transform

        // ── UI feedback ───────────────────────────────────────────────────────
        [Header("UI")]
        [SerializeField] private TMPro.TextMeshProUGUI grenadeCountText;

        private int   _grenadeCount;
        private bool  _cooking;
        private float _cookTimer;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _grenadeCount = maxGrenades;
            UpdateUI();
        }

        private void Update()
        {
            if (_grenadeCount <= 0 || grenadePrefab == null) return;

            if (Input.GetKeyDown(KeyCode.G))
            {
                _cooking   = true;
                _cookTimer = 0f;
            }

            if (_cooking)
            {
                _cookTimer = Mathf.Min(_cookTimer + Time.deltaTime, maxCookTime);

                if (Input.GetKeyUp(KeyCode.G))
                    ReleaseThrow();
            }
        }

        private void ReleaseThrow()
        {
            _cooking = false;
            _grenadeCount--;
            UpdateUI();

            // Spawn slightly in front of camera
            Transform origin = throwOrigin != null ? throwOrigin : Camera.main.transform;
            Vector3 spawnPos = origin.position + origin.forward * 0.5f;
            GameObject go    = Instantiate(grenadePrefab, spawnPos, origin.rotation);

            // Bake partial fuse time already spent cooking into the grenade
            var grenade = go.GetComponent<Utilities.Grenade>();
            if (grenade != null)
            {
                // Reduce fuse by time already cooked (accessed via SendMessage fallback)
                go.SendMessage("ReduceFuse", _cookTimer, SendMessageOptions.DontRequireReceiver);
            }

            // Throw force scales with cook time
            float force = Mathf.Lerp(minThrowForce, maxThrowForce, _cookTimer / maxCookTime);
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.AddForce(origin.forward * force, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.VelocityChange);
            }
        }

        public void RefillGrenades(int amount = 1)
        {
            _grenadeCount = Mathf.Min(_grenadeCount + amount, maxGrenades);
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (grenadeCountText != null)
                grenadeCountText.text = $"G  {_grenadeCount}";
        }
    }
}
