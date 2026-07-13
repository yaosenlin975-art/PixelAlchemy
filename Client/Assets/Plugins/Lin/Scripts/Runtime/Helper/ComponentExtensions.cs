using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lin.Runtime.Helper
{
    public static class ComponentExtensions
    {
        public static bool CompareComponent(this Component obj1, string obj1OwnerName, Component obj2, string obj2OwnerName)
        {
            if (obj1.GetType() != obj2.GetType())
                return false;

            var name1 = obj1.name;
            var name2 = obj2.name;
            if (name1 != name2 && name1 != obj1OwnerName && name2 != obj2OwnerName)
                return false;

            //Debug.Log(obj1.GetType());
            switch (obj1)
            {
                case MeshRenderer:
                    var mr1 = (MeshRenderer)obj1;
                    var mr2 = (MeshRenderer)obj2;
                    if (mr1.sharedMaterials.Length != mr2.sharedMaterials.Length)
                        return false;
                    for (int i = 0; i < mr1.sharedMaterials.Length; i++)
                        if (mr1.sharedMaterials[i] != mr2.sharedMaterials[i])
                            return false;
                    return true;

                case Terrain:
                    var terrain1 = (Terrain)obj1;
                    var terrain2 = (Terrain)obj2;
                    return terrain1.terrainData == terrain2.terrainData;

                case TerrainCollider:
                    var tc1 = (TerrainCollider)obj1;
                    var tc2 = (TerrainCollider)obj2;
                    return tc1.terrainData == tc2.terrainData
                        && tc1.isTrigger == tc2.isTrigger
                        /*&& tc1.providesContacts == tc2.providesContacts 
                        && tc1.material == tc2.material 
                        && tc1.layerOverridePriority == tc2.layerOverridePriority
                        && tc1.includeLayers == tc2.includeLayers
                        && tc1.excludeLayers == tc2.excludeLayers*/;

                case MeshCollider:
                    var col1 = (MeshCollider)obj1;
                    var col2 = (MeshCollider)obj2;
                    return col1.sharedMesh == col2.sharedMesh && col1.sharedMaterial == col2.sharedMaterial && col1.isTrigger == col2.isTrigger;

                case MeshFilter:
                    var filter1 = (MeshFilter)obj1;
                    var filter2 = (MeshFilter)obj2;
                    return filter1.sharedMesh == filter2.sharedMesh;

                case BoxCollider:
                    var box1 = (BoxCollider)obj1;
                    var box2 = (BoxCollider)obj2;
                    return box1.center == box2.center && box1.size == box2.size && box1.isTrigger == box2.isTrigger;

                case Transform:
                    var transform1 = (Transform)obj1;
                    var transform2 = (Transform)obj2;
                    return transform1.localPosition == transform2.localPosition && transform1.localRotation == transform2.localRotation && transform1.localScale == transform2.localScale;

                case LODGroup:
                    var group1 = (LODGroup)obj1;
                    var group2 = (LODGroup)obj2;
                    return group1.lodCount == group2.lodCount && group1.animateCrossFading == group2.animateCrossFading && group1.fadeMode == group2.fadeMode && group1.localReferencePoint == group2.localReferencePoint && group1.size == group2.size;

                default:
                    return true;
            }
        }

        public static T AddComponent<T>(this Component component)
            where T : Component => component.gameObject.AddComponent<T>();
        public static Component AddComponent(this Component component, Type type)
            => component.gameObject.AddComponent(type);

        public static T GetOrAddComponent<T>(this Component component)
            where T : Component
        {
            if (!component.TryGetComponent<T>(out var attachedComponent))
                attachedComponent = component.AddComponent<T>();

            return attachedComponent;
        }

        public static Component GetOrAddComponent(this Component component, Type type)
        {
            if (!component.TryGetComponent(type, out var attachedComponent))
                attachedComponent = component.gameObject.AddComponent(type);

            return attachedComponent;
        }

        public static bool HasComponent<T>(this Component component)
            where T : Component => component.TryGetComponent<T>(out _);

        public static void DestroyComponent<T>(this Component component)
            where T : Component
        {
            if (component.TryGetComponent<T>(out var componentToDestroy))
                componentToDestroy.Destroy();
        }

        public static void Destroy(this Component component)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                Object.Destroy(component);
            else
            {
                var owner = component.gameObject;
                Object.DestroyImmediate(component);
                owner.EditorSave(false);
            }
#else
            Object.Destroy(component);
#endif
        }

        public static bool TryGetComponentInParent<T>(
            this Component component,
            out T componentFound,
            bool includeInactive = false
        )
            where T : Component
        {
            componentFound = component.gameObject.GetComponentInParent<T>(includeInactive);
            return componentFound != null;
        }

        public static bool TryGetComponentInChildren<T>(
            this Component component,
            out T componentFound,
            bool includeInactive = false
        )
            where T : Component
        {
            componentFound = component.gameObject.GetComponentInChildren<T>(includeInactive);
            return componentFound != null;
        }
    }
}
