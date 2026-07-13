using Lin.Runtime.Helper;
using Lin.Runtime.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lin.Editor.Asset
{
    public class AssetDetailContainer : IInitialize<List<string>>, IPoolObject
    {
        private VisualElement container { get; }
        private Label nameLabel { get; }
        private Label sizeLabel { get; }
        private RepeatedAssetCheckerWindow window { get; }
        private Queue<AssetDetail> details;

        public List<string> currentFiles { get; private set; }

        public bool IsInitialized { get; private set; }

        public AssetDetailContainer(VisualElement container, RepeatedAssetCheckerWindow window)
        {
            this.container = container;
            nameLabel = container.Q<Label>("Name");
            sizeLabel = container.Q<Label>("Size");
            container.Q<Button>("IgnoreAllButton").clicked += OnIgnoreAllButtonClick;

            this.window = window;
            details = new Queue<AssetDetail>();
        }

        public void Initialize(List<string> files)
        {
            if (IsInitialized)
                return;

            IsInitialized = true;

            var singleFileSize = new FileInfo(files[0]).Length;
            nameLabel.text = Path.GetFileNameWithoutExtension(files[0]);
            sizeLabel.text = $"单个资源大小: {IOHelper.GetSizeString(singleFileSize)}\t总资源大小: {IOHelper.GetSizeString(singleFileSize * files.Count)}";

            currentFiles = files;

            foreach (var file in files)
            {
                var detail = window.GetDetail();
                var path = file.Replace(Application.dataPath, string.Empty).Replace("\\", "/").Substring(1);
                detail.Initialize(path);
                container.Add(detail.root);
                details.Enqueue(detail);
            }
        }

        public void OnGet()
        {
            container.SendToBack();
        }

        public void OnRelease()
        {
            while (details.TryDequeue(out var detail))
                window.ReleaseDetail(detail);

            container.BringToFront();
        }

        private void OnIgnoreAllButtonClick()
        {
            foreach (var file in currentFiles)
                window.AddIgnoreAssetPath(file);

            window.ReleaseContainer(this);
        }
    }
}
