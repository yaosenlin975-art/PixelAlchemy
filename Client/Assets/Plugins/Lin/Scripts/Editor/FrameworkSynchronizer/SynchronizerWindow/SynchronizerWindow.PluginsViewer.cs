/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;

namespace Lin.Editor.FrameworkSynchronizer
{
    public partial class SynchronizerWindow
    {
        private ListView pluginsViwer;
        private List<Toggle> pluginToggles;
        private VisualTreeAsset pluginToggleTemplate;

        private void PluginsViewerInit()
        {
            pluginToggleTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/Lin/Scripts/Editor/FrameworkSynchronizer/SynchronizerWindow/PluginToggle.uxml");

            pluginsViwer = rootVisualElement.Q<ListView>("PluginsViewer");
            pluginsViwer.parent.Q<Button>("SelectAllButton").clicked += OnPluginsViewerSelectAllBtnClick;
            pluginToggles = new List<Toggle>();

            RefreshPluginsViewer();
        }

        private void OnPluginsViewerSelectAllBtnClick()
        {
            var targetActive = false;
            foreach (var toggle in pluginToggles)
            {
                if (!toggle.value)
                {
                    targetActive = true;
                    break;
                }
            }

            foreach (var toggle in pluginToggles)
                toggle.value = targetActive;
        }

        private void RefreshPluginsViewer()
        {
            var plugins = Directory.GetDirectories("Assets/Plugins");
            var map = SynchronizerConfig.GetInstance().plugins;
            var content = pluginsViwer.Q<VisualElement>("unity-content-container");
            content.Clear();
            pluginToggles.Clear();

            foreach (var plugin in plugins)
                CreateToggle(plugin);

            var packages = Directory.GetDirectories("Packages");
            foreach (var customPackage in packages)
                CreateToggle(customPackage);

            void CreateToggle(string name)
            {
                if (!map.ContainsKey(name))
                    map.Add(name, false);

                var toggleContainer = pluginToggleTemplate.Instantiate();
                {
                    var toggle = toggleContainer.Q<Toggle>();
                    toggle.value = map[name];
                    toggle.RegisterValueChangedCallback(callback =>
                    {
                        map[name] = callback.newValue;
                        SetDirty();
                    });
                    pluginToggles.Add(toggle);

                    toggleContainer.Q<Label>().text = Path.GetFileName(name);
                }
                content.Add(toggleContainer);
            }
        }
    }
}