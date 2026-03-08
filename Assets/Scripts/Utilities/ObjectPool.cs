using System.Collections.Generic;
using UnityEngine;

namespace FreeWorld.Utilities
{
    /// <summary>
    /// Generic object pool. Avoids Instantiate/Destroy overhead for
    /// frequent objects like bullet impacts, muzzle flashes, shell casings.
    ///
    /// Usage:
    ///   var pool = ObjectPool.GetPool(impactPrefab, 20);
    ///   GameObject obj = pool.Get(position, rotation);
    ///   pool.Return(obj);          // or auto-return after delay
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        // ── Static pool registry ──────────────────────────────────────────────
        private static Dictionary<GameObject, ObjectPool> _pools
            = new Dictionary<GameObject, ObjectPool>();

        public static ObjectPool GetPool(GameObject prefab, int initialSize = 10)
        {
            if (_pools.TryGetValue(prefab, out ObjectPool existing))
                return existing;

            // Create a dedicated GameObject to hold the pool
            GameObject host = new GameObject($"Pool_{prefab.name}");
            DontDestroyOnLoad(host);

            ObjectPool pool = host.AddComponent<ObjectPool>();
            pool.Init(prefab, initialSize);
            _pools[prefab] = pool;
            return pool;
        }

        // ── Instance pool ─────────────────────────────────────────────────────
        private GameObject         _prefab;
        private Queue<GameObject>  _available = new Queue<GameObject>();

        private void Init(GameObject prefab, int initialSize)
        {
            _prefab = prefab;
            for (int i = 0; i < initialSize; i++)
                _available.Enqueue(CreateNew());
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject obj = _available.Count > 0
                           ? _available.Dequeue()
                           : CreateNew();

            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
            return obj;
        }

        /// <summary>Returns the object to the pool.</summary>
        public void Return(GameObject obj)
        {
            obj.SetActive(false);
            _available.Enqueue(obj);
        }

        /// <summary>Returns the object to the pool after a delay.</summary>
        public void ReturnAfter(GameObject obj, float delay)
        {
            StartCoroutine(ReturnRoutine(obj, delay));
        }

        private System.Collections.IEnumerator ReturnRoutine(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            Return(obj);
        }

        private GameObject CreateNew()
        {
            GameObject obj = Instantiate(_prefab, transform);
            obj.SetActive(false);
            return obj;
        }
    }
}
