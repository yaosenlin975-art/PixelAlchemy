using Lin.Editor.UI.UIControl;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;


public class UIControlsWindow : EditorWindow
{
    private VisualElement container;
    private List<UIPreviewElement> allElements;

    private TextField searchField;

    [MenuItem("Lin/UI/控件列表")]
    public static void ShowExample()
    {
        UIControlsWindow wnd = GetWindow<UIControlsWindow>();
        wnd.titleContent = new GUIContent("UI控件");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // Import UXML
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/Lin/Scripts/Editor/UI/ControllerWindow/UIControlsWindow.uxml");
        VisualElement labelFromUXML = visualTree.Instantiate();
        root.Add(labelFromUXML);

        var buttom = root.Q("Buttom");

        searchField = root.Q<TextField>("SearchTextField");
        searchField.RegisterValueChangedCallback(OnSearchTextFieldValueChanged);
        root.Q<Button>("RefreshButton").clicked += OnRefreshButtonClicked;

        // 创建ScrollView
        var scrollView = new ScrollView();
        scrollView.style.flexGrow = 1;
        buttom.Add(scrollView);

        // 创建容器用于网格布局
        container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.flexWrap = Wrap.Wrap;
        scrollView.Add(container);

        allElements = new List<UIPreviewElement>();

        OnRefreshButtonClicked();
    }

    private void OnRefreshButtonClicked()
    {
        searchField.value = string.Empty;

        //移除旧元素
        foreach (var element in allElements)
            container.Remove(element);
        allElements.Clear();

        //扫描所有UIControl文件夹
        string currentScenePath = SceneManager.GetActiveScene().path;
        var allUIControlFolders = Directory.GetDirectories("Assets", "UIControl", SearchOption.AllDirectories);

        foreach (var folder in allUIControlFolders)
        {
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new string[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var element = new UIPreviewElement(path);

                // 设置UIPreviewElement的固定大小
                element.style.width = 120;
                element.style.height = 120;
                element.style.marginLeft = 5;
                element.style.marginRight = 5;
                element.style.marginTop = 5;
                element.style.marginBottom = 5;

                container.Add(element);
                allElements.Add(element);
            }
        }

        if (!string.IsNullOrEmpty(currentScenePath) && SceneManager.GetActiveScene().path != currentScenePath)
            EditorSceneManager.OpenScene(currentScenePath);
    }

    private void OnSearchTextFieldValueChanged(ChangeEvent<string> evt)
    {
        string searchText = evt.newValue.ToLower();
        foreach (var element in allElements)
        {
            bool shouldShow = string.IsNullOrEmpty(searchText) || 
                             Path.GetFileNameWithoutExtension(element.assetPath).ToLower().Contains(searchText);
            element.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}