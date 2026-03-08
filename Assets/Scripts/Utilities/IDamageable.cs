using UnityEngine;

namespace FreeWorld
{
    /// <summary>
    /// Any object that can receive damage implements this interface.
    /// Decouples bullet/explosion systems from specific target types.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 hitPoint = default, Vector3 hitDirection = default);
        bool IsAlive { get; }
    }
}
