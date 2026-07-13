/*
┌────────────────────────────┐
│　Description: Hierarchy窗口中场景对象名字旁边的功能绘制
│　Remark: 
└────────────────────────────┘
*/
using Lin.Editor.Interface;
using Lin.Runtime.Helper;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Lin.Editor.Hierarchy
{
    [InitializeOnLoad]
    static class HierarchyDrawer
    {
        private static List<IHierarchyDrawable> drawers; 

        static HierarchyDrawer()
        {
            InitializeDrawers();
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI; 
        }

        /// <summary>
        /// 初始化所有实现了IHierarchyDrawable接口的类
        /// </summary>
        private static void InitializeDrawers()
        {
            drawers = new List<IHierarchyDrawable>();
            try
            {
                // 获取当前程序集中所有类型
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // 查找所有实现IHierarchyDrawable接口的类型
                        var drawerTypes = ReflectionHelper.GetAllInheritedTypes<IHierarchyDrawable>(assembly, ReflectionHelper.ECollectFlags.Class);

                        // 实例化找到的类型
                        foreach (var drawerType in drawerTypes)
                        {
                            try
                            {
                                var instance = Activator.CreateInstance(drawerType) as IHierarchyDrawable;
                                if (instance != null)
                                    drawers.Add(instance);
                            }
                            catch (Exception ex)
                            {
                                Error($"实例化 {drawerType.Name} 时出错: {ex.Message}");
                            }
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // 某些程序集可能无法加载所有类型，跳过这些程序集
                        Error($"跳过程序集 {assembly.FullName}: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Error($"处理程序集 {assembly.FullName} 时出错: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Error($"初始化绘制器时出错: {ex.Message}");
            }

            drawers.Sort((a, b) => b.drawPriority - a.drawPriority);
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            var currentRect = selectionRect;
            currentRect.x += 5;    // 紧贴预制体的跳转按钮
            // 调用所有drawer的Draw方法
            foreach (var drawer in drawers)
            {
                try
                {
                    var usedWidth = drawer.DrawInHierarchy(instanceID, currentRect);
                    if (usedWidth != 0)
                        usedWidth += 5;
                    currentRect.x -= usedWidth;
                }
                catch (Exception ex)
                {
                    Error($"绘制器 {drawer.GetType().Name} 执行Draw方法时出错: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        private static void Error(string message) => Log.Error("Hierarchy", message);
    }
}
