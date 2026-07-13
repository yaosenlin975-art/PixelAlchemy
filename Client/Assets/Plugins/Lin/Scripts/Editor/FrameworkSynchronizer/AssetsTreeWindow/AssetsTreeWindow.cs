using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Text;

namespace Lin.Editor.FrameworkSynchronizer
{
    // TODO: 点击窗口中的文件/文件夹时, 若是Assets或Packages目录下的文件/文件夹, 则选中该文件/文件夹
    public class AssetsTreeWindow : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        private Label ProjectName;
        private TreeView AssetTreeViewer;
        private List<TreeViewItemData<Item>> rootItems;
        private List<TreeViewItemData<Item>> displayItems;
        private Dictionary<string, Item> itemMap;
        private bool confirmPressed;
        private bool changeOnly;

        private Action<List<string>> onConfirm;
        private Action onCancel;

        public static void ShowWindow(string title, ConcurrentDictionary<string, AssetInfos.EAssetState> assetMapper, Action<List<string>> onConfirm, Action onCancel)
        {
            AssetsTreeWindow wnd = GetWindow<AssetsTreeWindow>();
            wnd.titleContent = new GUIContent("资源列表");
            wnd.Initialize(title, assetMapper, onConfirm, onCancel); 
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            ProjectName = root.Q<Label>(nameof(ProjectName));
            AssetTreeViewer = root.Q<TreeView>(nameof(AssetTreeViewer));

            root.Q<Button>("AllButton").clicked += OnAllButtonClick;
            root.Q<Button>("NoneButton").clicked += OnNoneButtonClick;
            root.Q<Button>("CopyButton").clicked += OnCopyButtonClick;
            root.Q<Toggle>("ChangeOnlyToggle").RegisterValueChangedCallback(OnChangeOnlyToggleValueChanged);
        }

        private void OnChangeOnlyToggleValueChanged(ChangeEvent<bool> evt)
        {
            changeOnly = evt.newValue;
            if (changeOnly)
                displayItems = BuildFilteredRootItems();
            else
                displayItems = rootItems;
            AssetTreeViewer.SetRootItems(displayItems);
            AssetTreeViewer.Rebuild();
        }

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            confirmPressed = false;
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Close;
            if (!confirmPressed)
                onCancel?.Invoke();
        }
        
        private void OnCopyButtonClick()
        {
            confirmPressed = true;
            Close();
            var selected = new List<string>(256);
            if (displayItems != null)
                foreach (var r in displayItems)
                    CollectSelected(r.data, selected);
            onConfirm?.Invoke(selected); 
        }

        private void OnNoneButtonClick()
        {
            if (displayItems == null)
                return;
            foreach (var r in displayItems)
                SetSelectedRecursively(r.data, false);
            AssetTreeViewer.Rebuild();
        }

        private void OnAllButtonClick()
        {
            if (displayItems == null)
                return;
            foreach (var r in displayItems)
                SetSelectedRecursively(r.data, true);
            AssetTreeViewer.Rebuild();
        }

        private void Initialize(string title, ConcurrentDictionary<string, AssetInfos.EAssetState> assetMapper, Action<List<string>> onConfirm, Action onCancel)
        {
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;

            ProjectName.text = title;

            var rootMap = new Dictionary<string, Node>();
            foreach (var kv in assetMapper)
            {
                var path = kv.Key.Replace('\\', '/');
                var parts = path.Split('/');
                if (parts.Length == 0)
                    continue;

                var current = GetOrAdd(rootMap, parts[0]);
                for (int i = 1; i < parts.Length; i++)
                {
                    current = GetOrAdd(current.children, parts[i]);
                }
                current.state = kv.Value;
                current.fullPath = path;
            }

            itemMap = new Dictionary<string, Item>(256);
            var items = new List<TreeViewItemData<Item>>();
            int id = 1;
            foreach (var kv in rootMap)
                items.Add(BuildItem(kv.Value, ref id));

            rootItems = items;
            displayItems = rootItems;

            AssetTreeViewer.makeItem = () =>
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;

                var toggle = new Toggle();
                toggle.name = "toggle";
                toggle.style.width = 20;

                var typeIcon = new Image();
                typeIcon.name = "typeIcon";
                typeIcon.style.width = 16;
                typeIcon.style.height = 16;
                typeIcon.scaleMode = ScaleMode.ScaleToFit;

                var nameLabel = new Label();
                nameLabel.name = "name";
                nameLabel.style.flexGrow = 1;

                var stateLabel = new Label();
                stateLabel.name = "state";
                stateLabel.style.minWidth = 20;
                stateLabel.style.unityTextAlign = TextAnchor.MiddleRight;

                row.Add(toggle);
                row.Add(typeIcon);
                row.Add(nameLabel);
                row.Add(stateLabel);
                return row;
            };

            AssetTreeViewer.bindItem = (e, index) =>
            {
                var data = AssetTreeViewer.GetItemDataForIndex<Item>(index);

                var toggle = e.Q<Toggle>("toggle");
                var typeIcon = e.Q<Image>("typeIcon");
                var nameLabel = e.Q<Label>("name");
                var stateLabel = e.Q<Label>("state");

                toggle.style.display = DisplayStyle.Flex;
                var enable = data.isFolder ? data.selectable : data.state != AssetInfos.EAssetState.Same;
                toggle.SetEnabled(enable);
                toggle.SetValueWithoutNotify(data.selected);
                toggle.RegisterValueChangedCallback(ev =>
                {
                    SetSelectedRecursively(data, ev.newValue);
                    AssetTreeViewer.Rebuild();
                });

                var n = data.name;
                nameLabel.text = n;

                var icon = GetTypeIcon(data);
                typeIcon.image = icon;

                if (data.isFolder)
                    stateLabel.text = string.Empty;
                else
                {
                    switch (data.state)
                    {
                        case AssetInfos.EAssetState.Same:
                            stateLabel.text = string.Empty;
                            stateLabel.style.color = Color.gray;
                            break;

                        case AssetInfos.EAssetState.New:
                            stateLabel.text = "新增";
                            stateLabel.style.color = Color.green;
                            break;

                        default:
                            stateLabel.text = "变化";
                            stateLabel.style.color = Color.yellow;
                            break;
                    }
                }

                e.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;
                    if (evt.target == toggle)
                        return;
                    var path = GetItemPath(data);
                    if (string.IsNullOrEmpty(path))
                        return;
                    if (path.StartsWith("Assets/") || path.StartsWith("Packages/"))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                        if (obj != null)
                        {
                            Selection.activeObject = obj;
                            EditorGUIUtility.PingObject(obj);
                        }
                    }
                });
            };

            AssetTreeViewer.SetRootItems(displayItems);
        }

        private class Node
        {
            public string name;
            public string fullPath;
            public AssetInfos.EAssetState state;
            public Dictionary<string, Node> children = new Dictionary<string, Node>();
        }

        private class Item
        {
            public string name;
            public bool isFolder;
            public string fullPath;
            public AssetInfos.EAssetState state;
            public bool selected;
            public List<Item> children;
            public Item parent;
            public bool selectable;
        }

        private static Node GetOrAdd(Dictionary<string, Node> dict, string name)
        {
            if (!dict.TryGetValue(name, out var node))
            {
                node = new Node { name = name };
                dict[name] = node;
            }
            return node;
        }

        private TreeViewItemData<Item> BuildItem(Node node, ref int id)
        {
            var childrenData = new List<TreeViewItemData<Item>>();
            var childItems = new List<Item>();
            foreach (var kv in node.children)
            {
                var cd = BuildItem(kv.Value, ref id);
                childItems.Add(cd.data);
                childrenData.Add(cd);
            }

            var isFolder = node.children.Count > 0;
            var item = new Item
            {
                name = node.name,
                isFolder = isFolder,
                fullPath = node.fullPath,
                state = node.state,
                children = childItems
            };

            if (isFolder)
            {
                bool anySelectable = false;
                foreach (var ci in childItems)
                    if (ci.selectable)
                    {
                        anySelectable = true;
                        break;
                    }
                item.selectable = anySelectable;
                item.selected = anySelectable;
                foreach (var ci in childItems)
                    ci.parent = item;
            }
            else
            {
                item.selectable = item.state != AssetInfos.EAssetState.Same;
                item.selected = item.selectable;
            }

            var data = new TreeViewItemData<Item>(id++, item, childrenData);
            if (!string.IsNullOrEmpty(item.fullPath))
                itemMap[item.fullPath] = item;
            return data;
        }

        private List<TreeViewItemData<Item>> BuildFilteredRootItems()
        {
            var result = new List<TreeViewItemData<Item>>();
            int id = 1;
            if (rootItems != null)
                foreach (var r in rootItems)
                {
                    var cd = BuildFilteredItem(r.data, ref id, out var included);
                    if (included)
                        result.Add(cd);
                }
            return result;
        }

        private TreeViewItemData<Item> BuildFilteredItem(Item item, ref int id, out bool included)
        {
            if (!item.isFolder)
            {
                if (item.state == AssetInfos.EAssetState.Same)
                {
                    included = false;
                    return default;
                }
                var emptyChildren = new List<TreeViewItemData<Item>>();
                included = true;
                return new TreeViewItemData<Item>(id++, item, emptyChildren);
            }

            var childrenData = new List<TreeViewItemData<Item>>();
            if (item.children != null)
                foreach (var c in item.children)
                {
                    var cd = BuildFilteredItem(c, ref id, out var inc);
                    if (inc)
                        childrenData.Add(cd);
                }

            if (childrenData.Count == 0)
            {
                included = false;
                return default;
            }

            included = true;
            return new TreeViewItemData<Item>(id++, item, childrenData);
        }

        private void SetSelectedRecursively(Item item, bool selected)
        {
            if (!item.selectable)
                return;
            item.selected = selected;
            if (item.children != null)
                foreach (var c in item.children)
                    SetSelectedRecursively(c, selected);
        }

        private void CollectSelected(Item item, List<string> output)
        {
            if (!item.isFolder)
            {
                if (item.selectable && item.selected && !string.IsNullOrEmpty(item.fullPath))
                    output.Add(item.fullPath);
                return;
            }
            if (item.children != null)
                foreach (var c in item.children)
                    CollectSelected(c, output);
        }

        private Texture2D GetTypeIcon(Item data)
        {
            if (data.isFolder)
            {
                var t = EditorGUIUtility.FindTexture("Folder Icon");
                return t as Texture2D;
            }
            var tex = AssetDatabase.GetCachedIcon(data.fullPath) as Texture2D;
            if (tex == null)
                tex = EditorGUIUtility.FindTexture("DefaultAsset Icon") as Texture2D;
            return tex;
        }

        private string GetItemPath(Item item)
        {
            if (!string.IsNullOrEmpty(item.fullPath))
                return item.fullPath;

            var segments = new List<string>(8);
            var current = item;
            while (current != null)
            {
                segments.Add(current.name);
                current = current.parent;
            }
            segments.Reverse();

            var sb = ZString.CreateStringBuilder();
            for (int i = 0; i < segments.Count; i++)
            {
                if (i > 0)
                    sb.Append('/');
                sb.Append(segments[i]);
            }
            var result = sb.ToString();
            sb.Dispose();
            return result;
        }
        
    }
}
