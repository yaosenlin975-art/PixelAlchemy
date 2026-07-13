/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using static Lin.Editor.FrameworkSynchronizer.SynchronizerConfig;

namespace Lin.Editor.FrameworkSynchronizer
{
    public partial class SynchronizerWindow
    {
        private ListView targetProjectsViwer;

        private void TargetProjectsViewerInit()
        {
            targetProjectsViwer = rootVisualElement.Q<ListView>("TargetProjectsViewer");

            rootVisualElement.Q<Button>("AddTargetProjectButton").clicked += OnAddTargetProjectBtnClick;

            RefreshTargetProjectsViewer();
        }

        private void RefreshTargetProjectsViewer()
        {
            var content = targetProjectsViwer.Q("unity-content-container");
            content.Clear();

            var map = SynchronizerConfig.GetInstance().targetProejcts;
            foreach (var pair in map)
                AddTargetProject(pair.Key);
        }

        private void OnAddTargetProjectBtnClick()
        {
            var folder = UnityEditor.EditorUtility.OpenFolderPanel("请选择目标项目根目录", string.Empty, string.Empty);
            if (string.IsNullOrEmpty(folder))
                return;

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var selectedPath = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var currentRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(selectedPath, currentRoot, System.StringComparison.OrdinalIgnoreCase))
            {
                UnityEditor.EditorUtility.DisplayDialog("非法选择", "不能选择当前项目根目录", "确定");
                return;
            }

            var assetsDir = Path.Combine(selectedPath, "Assets");
            var projectSettingsDir = Path.Combine(selectedPath, "ProjectSettings");
            if (!Directory.Exists(assetsDir) || !Directory.Exists(projectSettingsDir))
            {
                UnityEditor.EditorUtility.DisplayDialog("非法项目", "所选文件夹不是 Unity 项目，请选择包含 Assets 和 ProjectSettings 的目录。", "确定");
                return;
            }

            var targets = SynchronizerConfig.GetInstance().targetProejcts;
            if (!targets.ContainsKey(selectedPath))
            {
                targets.Add(selectedPath, ESyncOperationType.自动替换);
                AddTargetProject(selectedPath);
            }
        }

        private void AddTargetProject(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return;

            var targets = SynchronizerConfig.GetInstance().targetProejcts;
            var field = new EnumField(Path.GetFileName(folder), targets[folder]);
            field.RegisterValueChangedCallback(callback =>
            {
                targets[folder] = (ESyncOperationType)callback.newValue;
                SetDirty();
            });

            field.AddManipulator(new ContextualMenuManipulator(menuEvt =>
            {
                menuEvt.menu.AppendAction("删除", _ =>
                {
                    targets.Remove(folder);
                    field.RemoveFromHierarchy();
                    SetDirty();
                });
            }));

            var content = targetProjectsViwer.Q("unity-content-container");
            content.Add(field);

            SetDirty();
        }
    }
}
