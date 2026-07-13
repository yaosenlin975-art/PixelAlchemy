using UnityEngine;

namespace Lin.Runtime.Helper
{
    public static class GameObjectExtensions
    {
        //检测两个非预制体能否共用一个预制体
        public static bool CompareObjects(GameObject obj1, GameObject obj2)
        {
            if (obj1 is null || obj2 is null)
                return false;

            // 检查两个物体的组件
            Component[] components1 = obj1.GetComponentsInChildren<Component>();
            Component[] components2 = obj2.GetComponentsInChildren<Component>();

            // 检查组件数量
            if (components1.Length != components2.Length)
                return false;

            // 检查每个组件的类型和属性
            for (int i = 0; i < components1.Length; i++)
            {
                if (components1[i] is Transform && components1[i].name == obj1.name)
                    continue;

                if (!ComponentExtensions.CompareComponent(components1[i], obj1.name, components2[i], obj2.name))
                    return false;
            }

            // 如果通过了所有比较，则返回true
            return true;
        }

        public static T GetOrAddComponent<T>(this GameObject self)
            where T : Component
        {
            if (!self.TryGetComponent<T>(out var attachedComponent))
            {
                attachedComponent = self.AddComponent<T>();
            }

            return attachedComponent;
        }

        public static bool HasComponent<T>(this GameObject self)
            where T : Component => self.TryGetComponent<T>(out _);

        public static GameObject DestroyAllChildren(this GameObject self)
        {
            foreach (Transform child in self.transform)
                child.gameObject.Destroy();

            return self;
        }

        public static GameObject DestroyComponent<T>(this GameObject self)
            where T : Component
        {
            if (self.TryGetComponent<T>(out var componentToDestroy))
                componentToDestroy.Destroy();

            return self;
        }

        public static void Destroy(this GameObject component)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                Object.Destroy(component);
            else
                Object.DestroyImmediate(component);
#else
            Object.Destroy(component);
#endif
        }

        public static bool TryGetComponentInChildren<T>(
            this GameObject self,
            out T component,
            bool includeInactive = false
        )
            where T : Component
        {
            component = self.GetComponentInChildren<T>(includeInactive);
            return component != null;
        }

        public static bool TryGetComponentInParent<T>(
            this GameObject self,
            out T component,
            bool includeInactive = false
        )
            where T : Component
        {
            component = self.GetComponentInParent<T>(includeInactive);
            return component != null;
        }

        public static GameObject Enable(this GameObject self)
        {
            self.SetActive(true);
            return self;
        }

        public static GameObject Disable(this GameObject self)
        {
            self.SetActive(false);
            return self;
        }

        public static GameObject EnableIfDisabled(this GameObject self)
        {
            if (!self.activeInHierarchy)
                self.SetActive(true);

            return self;
        }

        public static GameObject DisableIfEnabled(this GameObject self)
        {
            if (self.activeInHierarchy)
                self.SetActive(false);

            return self;
        }

        public static GameObject Toggle(this GameObject self)
        {
            self.SetActive(!self.activeInHierarchy);
            return self;
        }

        public static T GetComponentInChildren<T>(this GameObject self, string name) where T : Component
        {
            var child = self.transform.Find(name);
            if (child != null)
                return child.GetComponent<T>();

            foreach (Transform item in self.transform)
            {
                var result = item.GetComponentInChildren<T>(name);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
