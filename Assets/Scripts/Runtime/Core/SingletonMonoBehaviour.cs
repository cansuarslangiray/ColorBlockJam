using UnityEngine;

namespace Runtime.Core
{
    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);
                }

                return _instance;
            }
        }

        protected virtual bool DestroyDuplicateInstances => true;

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                return;
            }

            if (_instance == this)
            {
                return;
            }

            if (DestroyDuplicateInstances)
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
