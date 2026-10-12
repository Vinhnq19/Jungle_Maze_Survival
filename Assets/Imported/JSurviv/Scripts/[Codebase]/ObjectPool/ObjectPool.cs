using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Codebase_.ObjectPool
{
    public class ObjectPool
    {
        private readonly HashSet<GameObject> _activeObjs   = new();
        private readonly HashSet<GameObject> _inactiveObjs = new();

        private readonly GameObject _prefab;

        public ObjectPool(GameObject prefab)
        {
            this._prefab = prefab;
        }

        public GameObject Take()
        {
            var obj = this._inactiveObjs.FirstOrDefault();

            if (obj == null)
            {
                obj = Object.Instantiate(this._prefab);
            }
            else
            {
                this._inactiveObjs.Remove(obj);
            }

            this._activeObjs.Add(obj);
            obj.SetActive(true);

            return obj;
        }

        public T Take<T>() where T : Component
        {
            var obj = this.Take().GetComponent<T>();
            return obj;
        }

        public bool Free(GameObject obj)
        {
            var result = this._activeObjs.Remove(obj);

            if (result)
            {
                this._inactiveObjs.Add(obj);
                obj.SetActive(false);
                obj.transform.SetParent(null);
            }

            return result;
        }

        public void Clear()
        {
            foreach (var obj in this._activeObjs)
            {
                Object.Destroy(obj);
            }

            this._activeObjs.Clear();

            foreach (var obj in this._inactiveObjs)
            {
                Object.Destroy(obj);
            }

            this._inactiveObjs.Clear();
        }
    }
}