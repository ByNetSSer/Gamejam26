using UnityEngine;

namespace MenuUIBase.Core
{
    /// <summary>
    /// Small, reusable base for services that must survive scene changes.
    /// It prevents duplicates when a bootstrap object is present in several scenes.
    /// </summary>
    public abstract class PersistentService<T> : MonoBehaviour where T : PersistentService<T>
    {
        private static T instance;
        private static bool isQuitting;

        protected static void ResetServiceState() { instance = null; isQuitting = false; }

        public static bool HasInstance => !isQuitting && instance != null;

        // Teardown must never discover or create a service, especially after quit.
        public static bool TryGetExisting(out T existing)
        {
            existing = isQuitting ? null : instance;
            return existing != null;
        }

        public static T Instance
        {
            get
            {
                if (isQuitting)
                    return null;

                if (instance == null)
                    instance = FindFirstObjectByType<T>();

                if (instance == null)
                {
                    GameObject serviceObject = new GameObject(typeof(T).Name);
                    instance = serviceObject.AddComponent<T>();
                }

                return instance;
            }
        }

        protected virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = (T)this;
            DontDestroyOnLoad(gameObject);
            OnServiceReady();
        }

        protected virtual void OnServiceReady()
        {
        }

        protected virtual void OnApplicationQuit()
        {
            isQuitting = true;
        }
    }
}
