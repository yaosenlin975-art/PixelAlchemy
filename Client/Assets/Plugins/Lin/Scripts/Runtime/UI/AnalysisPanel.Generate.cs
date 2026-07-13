//CreateTime: 2025/12/24 14:36:09
using UnityEngine;
using UnityEngine.UI;
using Lin.Runtime.Helper;
using Sirenix.OdinInspector;
using TMPro;
using System;

namespace Lin.Runtime.UI
{
    public partial class AnalysisPanel
    {
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Button settingsButton;
        public Button SettingsButton => settingsButton;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI setPassCallsTextMeshProUGUI;
        public TextMeshProUGUI SetPassCallsTextMeshProUGUI => setPassCallsTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI drawCallsTextMeshProUGUI;
        public TextMeshProUGUI DrawCallsTextMeshProUGUI => drawCallsTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI vertexsTextMeshProUGUI;
        public TextMeshProUGUI VertexsTextMeshProUGUI => vertexsTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI trianglesTextMeshProUGUI;
        public TextMeshProUGUI TrianglesTextMeshProUGUI => trianglesTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI usedMemoryTextMeshProUGUI;
        public TextMeshProUGUI UsedMemoryTextMeshProUGUI => usedMemoryTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI fPSTextMeshProUGUI;
        public TextMeshProUGUI FPSTextMeshProUGUI => fPSTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI currentSceneTextMeshProUGUI;
        public TextMeshProUGUI CurrentSceneTextMeshProUGUI => currentSceneTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private RectTransform settingBarRectTransform;
        public RectTransform SettingBarRectTransform => settingBarRectTransform;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Button closeButton;
        public Button CloseButton => closeButton;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Slider setPassCallsSlider;
        public Slider SetPassCallsSlider => setPassCallsSlider;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI setPassCallsValueTextMeshProUGUI;
        public TextMeshProUGUI SetPassCallsValueTextMeshProUGUI => setPassCallsValueTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Slider drawCallsSlider;
        public Slider DrawCallsSlider => drawCallsSlider;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI drawCallsValueTextMeshProUGUI;
        public TextMeshProUGUI DrawCallsValueTextMeshProUGUI => drawCallsValueTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Slider vertextsSlider;
        public Slider VertextsSlider => vertextsSlider;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI vertexsValueTextMeshProUGUI;
        public TextMeshProUGUI VertexsValueTextMeshProUGUI => vertexsValueTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Slider trianglesSlider;
        public Slider TrianglesSlider => trianglesSlider;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI trianglesValueTextMeshProUGUI;
        public TextMeshProUGUI TrianglesValueTextMeshProUGUI => trianglesValueTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Slider memorySlider;
        public Slider MemorySlider => memorySlider;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI memoryValueTextMeshProUGUI;
        public TextMeshProUGUI MemoryValueTextMeshProUGUI => memoryValueTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Slider fpsSlider;
        public Slider FpsSlider => fpsSlider;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI fpsValueTextMeshProUGUI;
        public TextMeshProUGUI FpsValueTextMeshProUGUI => fpsValueTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI tipsTextMeshProUGUI;
        public TextMeshProUGUI TipsTextMeshProUGUI => tipsTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Button saveAsSceneConfigButton;
        public Button SaveAsSceneConfigButton => saveAsSceneConfigButton;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Button saveAsCommonConfigButton;
        public Button SaveAsCommonConfigButton => saveAsCommonConfigButton;
        private bool reigsteredControlsListener;

        private void Reset()
        {
            settingsButton = gameObject.GetComponentInChildren<Button>("[Button] Settings");
            setPassCallsTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] SetPassCalls");
            drawCallsTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] DrawCalls");
            vertexsTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] Vertexs");
            trianglesTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] Triangles");
            usedMemoryTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] UsedMemory");
            fPSTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] FPS");
            currentSceneTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] CurrentScene");
            settingBarRectTransform = gameObject.GetComponentInChildren<RectTransform>("[RectTransform] SettingBar");
            closeButton = gameObject.GetComponentInChildren<Button>("[Button] Close");
            setPassCallsSlider = gameObject.GetComponentInChildren<Slider>("[Slider] SetPassCalls");
            setPassCallsValueTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] SetPassCallsValue");
            drawCallsSlider = gameObject.GetComponentInChildren<Slider>("[Slider] DrawCalls");
            drawCallsValueTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] DrawCallsValue");
            vertextsSlider = gameObject.GetComponentInChildren<Slider>("[Slider] Vertexts");
            vertexsValueTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] VertexsValue");
            trianglesSlider = gameObject.GetComponentInChildren<Slider>("[Slider] Triangles");
            trianglesValueTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] TrianglesValue");
            memorySlider = gameObject.GetComponentInChildren<Slider>("[Slider] Memory");
            memoryValueTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] MemoryValue");
            fpsSlider = gameObject.GetComponentInChildren<Slider>("[Slider] Fps");
            fpsValueTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] FpsValue");
            tipsTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] Tips");
            saveAsSceneConfigButton = gameObject.GetComponentInChildren<Button>("[Button] SaveAsSceneConfig");
            saveAsCommonConfigButton = gameObject.GetComponentInChildren<Button>("[Button] SaveAsCommonConfig");
        }

        public override void RegisterEvents()
        {
            RegisterCustomEvents();
            // 组件固定监听, 不在Deregister中移除, 避免组件重复注册监听
            if (reigsteredControlsListener)
                return;

            // Buttons OnClick
            settingsButton.AddOnClickListener(OnSettingsButtonClick);
            closeButton.AddOnClickListener(OnCloseButtonClick);
            saveAsSceneConfigButton.AddOnClickListener(OnSaveAsSceneConfigButtonClick);
            saveAsCommonConfigButton.AddOnClickListener(OnSaveAsCommonConfigButtonClick);

            // Sliders OnValueChanged
            setPassCallsSlider.AddOnValueChangedListener(OnSetPassCallsSliderValueChanged);
            drawCallsSlider.AddOnValueChangedListener(OnDrawCallsSliderValueChanged);
            vertextsSlider.AddOnValueChangedListener(OnVertextsSliderValueChanged);
            trianglesSlider.AddOnValueChangedListener(OnTrianglesSliderValueChanged);
            memorySlider.AddOnValueChangedListener(OnMemorySliderValueChanged);
            fpsSlider.AddOnValueChangedListener(OnFpsSliderValueChanged);
            reigsteredControlsListener = true;
        }

        public override void DeregisterEvents()
        {
            DeregisterCustomEvents();
        }

    }
}
