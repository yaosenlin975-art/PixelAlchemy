/*
┌────────────────────────────┐
│　Description: 带有测试宏时激活对应目标
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: TestActive
└──────────────┘
*/
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

namespace Lin.Runtime.Tool
{
    class TestActive : MonoBehaviour
    {
        [SerializeField, SceneObjectsOnly] List<Object> targets;

        private void Awake()
        {
#if TEST
            SetActive(true);
#else
            SetActive(false);
#endif
        }

        private void SetActive(bool active)
        {
            gameObject.SetActive(active);
            foreach (var target in targets)
            {
                switch (target)
                {
                    case GameObject gameObject:
                        gameObject.SetActive(active); 
                        break;

                    case MonoBehaviour behaviour:
                        behaviour.enabled = active;
                        break;

                    default:
                        break;
                }
            }
        }
    }
}