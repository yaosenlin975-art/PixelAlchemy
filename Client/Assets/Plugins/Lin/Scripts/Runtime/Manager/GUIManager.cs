/*
┌────────────────────────────┐
│　Description: 常用在编辑器环境下的GUI管理器, 由一个MB执行可使用GUILayout
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: GUIManager
└──────────────┘
*/
using Lin.Runtime.DesignPattern.Singleton;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace Lin.Runtime.Manager
{
    public class GUIManager : MonoSingleton<GUIManager>
{
    public UnityEvent<GUIStyle> onGUI;
    [SerializeField, Range(1, 100)] int fontSize = 40;
    [SerializeField] Color fontColor = new Color(1f, 1f, 1f, 0.5f);

    private GUIStyle defaultStyle;

    protected override void Init()
    {
        onGUI = new UnityEvent<GUIStyle>();

        defaultStyle = new GUIStyle();
        defaultStyle.fontSize = 40;
        defaultStyle.normal.textColor = fontColor;
    }

    private void OnGUI()
    {
        onGUI.Invoke(defaultStyle);
    }
}
}
