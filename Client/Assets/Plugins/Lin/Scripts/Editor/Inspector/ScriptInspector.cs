/*
┌────────────────────────────┐
│　Description：
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：ScriptInspector
└──────────────┘
*/

using Lin.Runtime.Attribute;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lin.Editor.Inspector
{
    public static class ScriptInspector
    {
        [MenuItem("CONTEXT/MonoBehaviour/修改GameObject的名字")]
        public static void RenameGameObject(MenuCommand command)
        {
            MonoBehaviour monoBehaviour = command.context as MonoBehaviour;
            if (monoBehaviour != null)
            {
                var nameAtt = monoBehaviour.GetType().GetCustomAttributes(typeof(NameAttribute), false);
                if (nameAtt?.Any() ?? false)
                    monoBehaviour.name = (nameAtt.First() as NameAttribute).Name;
                else
                    monoBehaviour.name = monoBehaviour.GetType().Name;
            }
        }
    }
}
