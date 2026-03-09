using UnityEngine;

namespace FreeWorld.Utilities
{
    /// <summary>
    /// Collectible item that can restore health, grant armor, or add ammo.
    /// Place on a pickup GameObject with a trigger collider.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        public enum PickupType { Health, Armor, Ammo }

        [Header("Pickup Settings")]
        [SerializeField] private PickupType pickupType  = PickupType.Health;
        [SerializeField] private float      amount      = 25f;
        [SerializeField] private float      bobSpeed    = 2f;
        [SerializeField] private float      rotateSpeed = 90f;
        [SerializeField] private AudioClip  pickupSound;

        /// <summary>Configure pickup type and amount at runtime (used by procedural loot drops).</summary>
        public void Configure(PickupType type, float pickupAmount)
        {
            pickupType = type;
            amount     = pickupAmount;
        }

        private Vector3 _startPos;

        private void Awake() => _startPos = transform.position;

        private void Update()
        {
            // Idle animation: float and rotate
            transform.position = _startPos + Vector3.up *
                                  (Mathf.Sin(Time.time * bobSpeed) * 0.15f);
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            switch (pickupType)
            {
                case PickupType.Health:
                    other.GetComponent<Player.PlayerHealth>()?.Heal(amount);
                    break;
                case PickupType.Armor:
                    other.GetComponent<Player.PlayerHealth>()?.AddArmor(amount);
                    break;
                case PickupType.Ammo:
                    other.GetComponent<Weapons.WeaponManager>()
                         ?.RefillCurrentWeaponAmmo(Mathf.RoundToInt(amount));
                    break;
            }

            if (pickupSound != null)
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            else
                ProceduralAudioLibrary.PlayAt(ProceduralAudioLibrary.ClipPickup, transform.position);

            Destroy(gameObject);
        }
    }
}
