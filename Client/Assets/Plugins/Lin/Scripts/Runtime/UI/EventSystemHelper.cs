/*
┌────────────────────────────┐
│　Description: 保证场景中只有一个ES
│　Remark: 
└────────────────────────────┘
*/
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Lin.Runtime.UI
{
    [RequireComponent(typeof(EventSystem))]
    class EventSystemHelper : MonoBehaviour
    {
        private EventSystem self;

        private void Awake()
        {
            self = GetComponent<EventSystem>();
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
            SceneManager_activeSceneChanged(default, default);
        }

        private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
        {
            foreach (var es in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (es != self)
                    Destroy(es.gameObject);
        }
    }
}