/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using Lin.Editor.Attribute;
using Lin.Editor.Toolbar.Element;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace Lin.Editor.Toolbar
{
    [InitializeOnLoad]
    public static class ToolbarElements
    {
        private static HashSet<Type> ignores = new HashSet<Type>
        {
            typeof(ToolbarButton),
            typeof(ToolbarToggle),
        };

        static ToolbarElements()
        {
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (ToolbarRoot.leftAlign == null)
                return;

            // 反射获取所有以 Lin.Editor.Toolbar.Element.ToolbarElementBase为基类的类
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            // 创建一个字典来存储所有元素，按照对齐方式和可见模式进行分组
            Dictionary<EAlign, Dictionary<EVisibleMode, List<IToolbarElement>>> map = new Dictionary<EAlign, Dictionary<EVisibleMode, List<IToolbarElement>>>();
            
            // 初始化字典
            foreach (EAlign align in System.Enum.GetValues(typeof(EAlign)))
            {
                map[align] = new Dictionary<EVisibleMode, List<IToolbarElement>>();
                foreach (EVisibleMode visibleMode in System.Enum.GetValues(typeof(EVisibleMode)))
                {
                    map[align][visibleMode] = new List<IToolbarElement>();
                }
            }

            var toolbarBtnType = typeof(ToolbarButton);
            var editorAssemblies = assemblies.AsValueEnumerable().Where(c => c.FullName.Contains("Editor"));
            foreach (var assembly in editorAssemblies)
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (ignores.Contains(type))
                            continue;

                        // 反射获取IToolbarElement
                        if (type.IsClass && !type.IsAbstract && typeof(IToolbarElement).IsAssignableFrom(type) && type != toolbarBtnType)
                        {
                            try
                            {
                                var element = Activator.CreateInstance(type) as IToolbarElement;

                                // 将元素添加到对应的分组中
                                if (element != null)
                                    map[element.align][element.visibleMode].Add(element);
                            } 
                            catch (Exception ex)
                            {
                                UnityEngine.Debug.LogError($"创建工具栏元素 {type.Name} 时出错: {ex.Message}");
                            }
                        }
                        // 获取类中所有的静态方法
                        else
                        {
                            var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                            foreach (var method in methods)
                            {
                                // 检查方法是否有 ToolbarElementAttribute 特性
                                var attribute = method.GetCustomAttribute<ToolbarElementAttribute>();
                                IToolbarElement element = null;
                                switch (attribute)
                                {
                                    case ToolbarButtonAttribute buttonAttribute:
                                        try
                                        {
                                            // 创建按钮元素
                                            // 将MethodInfo转换为Action委托
                                            Action onClick = () => method.Invoke(null, null);
                                            element = new ToolbarButton(buttonAttribute, onClick);
                                        }
                                        catch (Exception ex)
                                        {
                                            UnityEngine.Debug.LogError($"创建工具栏按钮 {method.Name} 时出错: {ex.Message}");
                                        }
                                        break;

                                    case ToolbarToggleAttribute toggleAttribute:
                                        try
                                        {
                                            Action<bool> onValueChanged = active =>
                                            {
                                                Runtime.Helper.PrefsHelper.Set(toggleAttribute.key, active);
                                                method.Invoke(null, new object[] { active });
                                            };
                                            element = new ToolbarToggle(toggleAttribute, onValueChanged);
                                        }
                                        catch (Exception ex)
                                        {
                                            UnityEngine.Debug.LogError($"创建工具栏Toggle {method.Name} 时出错: {ex.Message}");
                                        }
                                        break;

                                    default:
                                        break;
                                }

                                // 将元素添加到对应的分组中
                                if (element != null)
                                    map[attribute.align][attribute.visibleMode].Add(element);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"加载程序集 {assembly.FullName} 时出错: {ex.Message}");
                }
            }

            // 创建并添加元素到工具栏
            bool isPlaying = EditorApplication.isPlaying;
            
            // 处理每种对齐方式
            foreach (var alignPair in map)
            {
                EAlign align = alignPair.Key;
                var visibleModeDict = alignPair.Value;
                
                // 获取对应的对齐区域
                VisualElement targetContainer = null;
                switch (align)
                {
                    case EAlign.Left:
                        targetContainer = ToolbarRoot.leftAlign;
                        break;
                    case EAlign.Middle:
                        targetContainer = ToolbarRoot.middleAlign;
                        break;
                    case EAlign.Right:
                        targetContainer = ToolbarRoot.rightAlign;
                        break;
                }
                
                if (targetContainer != null)
                {
                    targetContainer.style.flexDirection = FlexDirection.Row;
                    targetContainer.style.flexWrap = Wrap.NoWrap;
                    targetContainer.style.alignItems = Align.Center;
                    // 处理每种可见模式
                    foreach (var modePair in visibleModeDict)
                    {
                        EVisibleMode mode = modePair.Key;
                        var elements = modePair.Value;
                        
                        // 检查当前模式是否应该显示
                        bool shouldShow = false;
                        switch (mode)
                        {
                            case EVisibleMode.None:
                                shouldShow = false;
                                break;
                            case EVisibleMode.Editor:
                                shouldShow = !isPlaying;
                                break;
                            case EVisibleMode.Runtime:
                                shouldShow = isPlaying;
                                break;
                            case EVisibleMode.Both:
                                shouldShow = true;
                                break;
                        }
                        
                        if (shouldShow)
                        {
                            // 创建并添加元素
                            foreach (var element in elements)
                            {
                                try
                                {
                                    var visualElement = element.Create();
                                    if (visualElement != null)
                                    {
                                        float w = Mathf.Max(1f, element.width);
                                        visualElement.style.width = w;
                                        visualElement.style.minWidth = w;
                                        visualElement.style.maxWidth = w;
                                        visualElement.style.height = 20f;
                                        visualElement.style.flexShrink = 0;
                                        visualElement.style.marginLeft = 4f;
                                        visualElement.style.marginRight = 4f;
                                        targetContainer.Add(visualElement);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    UnityEngine.Debug.LogError($"创建元素 {element.GetType().Name} 的UI时出错: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            
            // 注册播放模式变化事件，以便在运行模式变化时更新元素可见性
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            EditorApplication.update -= OnUpdate;
        }
        
        /// <summary>
        /// 处理播放模式状态变化的回调方法
        /// </summary>
        /// <param name="state">播放模式状态</param>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 只在进入或退出播放模式时重新构建工具栏
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                // 清空现有的工具栏元素
                ToolbarRoot.leftAlign.Clear();
                ToolbarRoot.middleAlign.Clear();
                ToolbarRoot.rightAlign.Clear();
                
                // 重新构建工具栏
                EditorApplication.delayCall += () =>
                {
                    // 使用静态构造函数重新初始化工具栏
                    var constructorInfo = typeof(ToolbarElements).GetConstructor(
                        BindingFlags.Static | BindingFlags.NonPublic,
                        null, Type.EmptyTypes, null);
                    
                    if (constructorInfo != null)
                        constructorInfo.Invoke(null, null);
                    else
                        UnityEngine.Debug.LogError("无法找到ToolbarElements的静态构造函数");
                };
            }
        }
    }
}
