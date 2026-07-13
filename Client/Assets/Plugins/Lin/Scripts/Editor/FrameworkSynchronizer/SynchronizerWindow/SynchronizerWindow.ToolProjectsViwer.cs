/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UIElements;

namespace Lin.Editor.FrameworkSynchronizer
{
    public partial class SynchronizerWindow
    {
        private ListView toolProjectsViwer;
        private List<Toggle> toolProjectToggles;

        private void ToolProjectsViewerInit()
        {
            toolProjectsViwer = rootVisualElement.Q<ListView>("ToolProjectViewer");
            toolProjectsViwer.parent.Q<Button>("SelectAllButton").clicked += OnToolProjectsViewerSelectAllBtnClick;
            toolProjectToggles = new List<Toggle>();

            RefreshToolProjectsViewer();
        }

        private void OnToolProjectsViewerSelectAllBtnClick()
        {
            var targetActive = false;
            foreach (var toggle in toolProjectToggles)
            {
                if (!toggle.value)
                {
                    targetActive = true;
                    break;
                }
            }

            foreach (var toggle in toolProjectToggles)
                toggle.value = targetActive;
        }

        private void RefreshToolProjectsViewer()
        {
            var plugins = Directory.GetDirectories("ToolProjects");
            var map = SynchronizerConfig.GetInstance().toolProejcts;
            var content = toolProjectsViwer.Q<VisualElement>("unity-content-container");
            content.Clear();
            toolProjectToggles.Clear();

            foreach (var plugin in plugins)
                CreateToggle(plugin);

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
                    toolProjectToggles.Add(toggle);

                    toggleContainer.Q<Label>().text = Path.GetFileName(name);
                }
                content.Add(toggleContainer);
            }
        }
    }
}
