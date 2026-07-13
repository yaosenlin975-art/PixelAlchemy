//CreateTime: 2025/12/24 14:36:09
using Lin.Runtime.Attribute;
using Lin.Runtime.DesignPattern.Singleton;
using Lin.Runtime.Helper;
using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lin.Runtime.UI
{
    [AssetPath("UI/AnalysisPanel", AssetPathAttribute.ELoaderType.Resources)]
    public partial class AnalysisPanel : PanelBase
    {
        [Serializable]
        class ConfigMapper : ArchiverSingleton<ConfigMapper>
        {
            public Dictionary<string, TargetConfig> sceneConfigs;
            public TargetConfig commonConfig;

            public TargetConfig Get()
            {
                if (sceneConfigs.TryGetValue(SceneManager.GetActiveScene().name, out var config))
                    return config;

                return commonConfig;
            }

            public bool TryGetSceneConfig(out TargetConfig config)
            {
                if (sceneConfigs == null)
                    sceneConfigs = new Dictionary<string, TargetConfig>();

                if (!sceneConfigs.TryGetValue(SceneManager.GetActiveScene().name, out config))
                {
                    config = commonConfig;
                    if (config == default)
                        config = TargetConfig.Default;

                    return false;
                }

                return true;
            }

            public void Set(TargetConfig config)
            {
                sceneConfigs[SceneManager.GetActiveScene().name] = config;
                Save();
            }

            public void SetCommon(TargetConfig config)
            {
                commonConfig = config;
                Save();
            }

            [Serializable]
            public struct TargetConfig : IEquatable<TargetConfig>
            {
                public int setPassCalls;
                public int drawCalls;
                public int vertexts;
                public int triangles;
                public int memory;  //MB
                public int fps;

                public static TargetConfig Default
                {
                    get
                    {
                        var platform = Application.platform;
                        var cpuCores = SystemInfo.processorCount;
                        var systemMemoryMb = SystemInfo.systemMemorySize;
                        var isDesktop =
                            platform == RuntimePlatform.WindowsPlayer ||
                            platform == RuntimePlatform.OSXPlayer ||
                            platform == RuntimePlatform.LinuxPlayer ||
                            platform == RuntimePlatform.WindowsEditor ||
                            platform == RuntimePlatform.OSXEditor ||
                            platform == RuntimePlatform.LinuxEditor;

                        var isMobile =
                            platform == RuntimePlatform.Android ||
                            platform == RuntimePlatform.IPhonePlayer;

                        bool highTier;
                        bool midTier;
                        if (isDesktop)
                        {
                            highTier = cpuCores >= 12 && systemMemoryMb >= 16 * 1024;
                            midTier = !highTier && cpuCores >= 8 && systemMemoryMb >= 8 * 1024;
                        }
                        else if (isMobile)
                        {
                            highTier = cpuCores >= 8 && systemMemoryMb >= 6 * 1024;
                            midTier = !highTier && cpuCores >= 6 && systemMemoryMb >= 4 * 1024;
                        }
                        else
                        {
                            highTier = false;
                            midTier = true;
                        }

                        if (isDesktop)
                        {
                            if (highTier)
                            {
                                return new TargetConfig
                                {
                                    setPassCalls = 450,
                                    drawCalls = 400,
                                    vertexts = 1_200_000,
                                    triangles = 600_000,
                                    memory = 2048,
                                    fps = 90
                                };
                            }
                            if (midTier)
                            {
                                return new TargetConfig
                                {
                                    setPassCalls = 300,
                                    drawCalls = 250,
                                    vertexts = 800_000,
                                    triangles = 400_000,
                                    memory = 1536,
                                    fps = 60
                                };
                            }

                            return new TargetConfig
                            {
                                setPassCalls = 180,
                                drawCalls = 150,
                                vertexts = 500_000,
                                triangles = 250_000,
                                memory = 1024,
                                fps = 60
                            };
                        }
                        else
                        {
                            if (highTier)
                            {
                                return new TargetConfig
                                {
                                    setPassCalls = 220,
                                    drawCalls = 200,
                                    vertexts = 600_000,
                                    triangles = 300_000,
                                    memory = 1024,
                                    fps = 60
                                };
                            }
                            if (midTier)
                            {
                                return new TargetConfig
                                {
                                    setPassCalls = 150,
                                    drawCalls = 130,
                                    vertexts = 400_000,
                                    triangles = 200_000,
                                    memory = 768,
                                    fps = 60
                                };
                            }

                            return new TargetConfig
                            {
                                setPassCalls = 100,
                                drawCalls = 90,
                                vertexts = 250_000,
                                triangles = 120_000,
                                memory = 512,
                                fps = 30
                            };
                        }
                    }
                }

                bool IEquatable<TargetConfig>.Equals(TargetConfig other)
                {
                    return vertexts == other.vertexts
                        && triangles == other.triangles
                        && memory == other.memory
                        && fps == other.fps
                        && setPassCalls == other.setPassCalls
                        && drawCalls == other.drawCalls;
                }

                public static bool operator ==(TargetConfig left, TargetConfig right)
                {
                    return left.vertexts == right.vertexts
                        && left.triangles == right.triangles
                        && left.memory == right.memory
                        && left.fps == right.fps
                        && left.setPassCalls == right.setPassCalls
                        && left.drawCalls == right.drawCalls;
                }

                public static bool operator !=(TargetConfig left, TargetConfig right) => !(left == right);

                public override bool Equals(object obj)
                {
                    if (obj is TargetConfig other)
                        return this == other;
                    return false;
                }

                public override int GetHashCode()
                {
                    unchecked
                    {
                        var hash = setPassCalls;
                        hash = (hash * 397) ^ drawCalls;
                        hash = (hash * 397) ^ vertexts;
                        hash = (hash * 397) ^ triangles;
                        hash = (hash * 397) ^ memory;
                        hash = (hash * 397) ^ fps;
                        return hash;
                    }
                }
            }
        }

        private ConfigMapper.TargetConfig currentConfig;

        private ProfilerRecorder setPassCallsRecorder;
        private ProfilerRecorder drawCallsRecorder;
        private ProfilerRecorder verticesRecorder;
        private ProfilerRecorder triangleRecorder;
        private ProfilerRecorder usedMemoryRecorder;

        // 帧率计算
        private float interval = 1 / 2f;
        private float fps;
        private int lastFrameCount;
        private float lastRefreshTime;

        private Color normalColor = Color.green;
        private Color warningColor = new Color(1, 0.5f, 0, 1);
        private Color outOfLimitColor = Color.red;

        protected override void Init()
        {
            base.Init();

            setPassCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            verticesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            triangleRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            usedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");

            var hasSceneConfig = ConfigMapper.GetInstance().TryGetSceneConfig(out currentConfig);
            tipsTextMeshProUGUI.gameObject.SetActive(hasSceneConfig);
        }

        private void Update()
        {
            CaculateFPS();
        }

        private void FixedUpdate()
        {
            var setPass = setPassCallsRecorder.LastValue;
            var draw = drawCallsRecorder.LastValue;
            var verts = verticesRecorder.LastValue;
            var tris = triangleRecorder.LastValue;
            var usedMb = usedMemoryRecorder.LastValue / 1024f / 1024f;

            StringExtension.RecycleNumberString(setPassCallsTextMeshProUGUI.text);
            StringExtension.RecycleNumberString(drawCallsTextMeshProUGUI.text);
            StringExtension.RecycleNumberString(vertexsTextMeshProUGUI.text);
            StringExtension.RecycleNumberString(trianglesTextMeshProUGUI.text);
            StringExtension.RecycleNumberString(usedMemoryTextMeshProUGUI.text);
            StringExtension.RecycleNumberString(fPSTextMeshProUGUI.text);

            setPassCallsTextMeshProUGUI.text = setPass.GetNumberString();
            drawCallsTextMeshProUGUI.text = draw.GetNumberString();
            vertexsTextMeshProUGUI.text = verts.GetNumberString();
            trianglesTextMeshProUGUI.text = tris.GetNumberString();
            usedMemoryTextMeshProUGUI.text = usedMb.GetNumberString();
            fPSTextMeshProUGUI.text = fps.GetNumberString(0);

            var overRatio = 1.1f;
            var fpsWarnRatio = 0.8f;

            // 资源阈值
            setPassCallsTextMeshProUGUI.color = setPass <= currentConfig.setPassCalls
                ? normalColor
                : (setPass <= currentConfig.setPassCalls * overRatio ? warningColor : outOfLimitColor);

            drawCallsTextMeshProUGUI.color = draw <= currentConfig.drawCalls
                ? normalColor
                : (draw <= currentConfig.drawCalls * overRatio ? warningColor : outOfLimitColor);

            vertexsTextMeshProUGUI.color = verts <= currentConfig.vertexts
                ? normalColor
                : (verts <= currentConfig.vertexts * overRatio ? warningColor : outOfLimitColor);

            trianglesTextMeshProUGUI.color = tris <= currentConfig.triangles
                ? normalColor
                : (tris <= currentConfig.triangles * overRatio ? warningColor : outOfLimitColor);

            usedMemoryTextMeshProUGUI.color = usedMb <= currentConfig.memory
                ? normalColor
                : (usedMb <= currentConfig.memory * overRatio ? warningColor : outOfLimitColor);

            fPSTextMeshProUGUI.color = fps >= currentConfig.fps
                ? normalColor
                : (fps >= currentConfig.fps * fpsWarnRatio ? warningColor : outOfLimitColor);
        }

        protected override void OnRelease()
        {
            setPassCallsRecorder.Dispose();
            drawCallsRecorder.Dispose();
            verticesRecorder.Dispose();
            triangleRecorder.Dispose();
            usedMemoryRecorder.Dispose();
        }

        private void RegisterCustomEvents()
        {
            // 自行补充需要注册的事件监听
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void DeregisterCustomEvents()
        {
            // 添加监听后记得添加注销监听
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }


        private void CaculateFPS()
        {
            var currentInterval = Time.time - lastRefreshTime;
            if (currentInterval < interval)
                return;

            fps = (Time.frameCount - lastFrameCount) / currentInterval;
            lastFrameCount = Time.frameCount;
            lastRefreshTime = Time.time;
        }

        #region - Callback -

        private void OnSceneChanged(Scene arg0, Scene arg1)
        {
            var active = ConfigMapper.GetInstance().TryGetSceneConfig(out currentConfig);

            currentSceneTextMeshProUGUI.text = arg1.name;
            tipsTextMeshProUGUI.gameObject.SetActive(active);
        }

        private void OnSettingsButtonClick()
        {
            settingBarRectTransform.gameObject.SetActive(!settingBarRectTransform.gameObject.activeSelf);
            if (settingBarRectTransform.gameObject.activeSelf)
            {
                setPassCallsSlider.value = currentConfig.setPassCalls;
                drawCallsSlider.value = currentConfig.drawCalls;
                vertextsSlider.value = currentConfig.vertexts;
                trianglesSlider.value = currentConfig.triangles;
                fpsSlider.value = currentConfig.fps;
                memorySlider.value = currentConfig.memory;
            }
        }

        private void OnCloseButtonClick() => settingBarRectTransform.gameObject.SetActive(false);

        private void OnSaveAsSceneConfigButtonClick()
        {
            ConfigMapper.GetInstance().Set(currentConfig);
            tipsTextMeshProUGUI.gameObject.SetActive(false);
            OnCloseButtonClick();
        }

        private void OnSaveAsCommonConfigButtonClick()
        {
            ConfigMapper.GetInstance().SetCommon(currentConfig);
            OnCloseButtonClick();
        }

        private void OnSetPassCallsSliderValueChanged(float value)
        {
            currentConfig.setPassCalls = (int)value;
            setPassCallsValueTextMeshProUGUI.text = value.GetNumberString(0);
        }

        private void OnDrawCallsSliderValueChanged(float value)
        {
            currentConfig.drawCalls = (int)value;
            drawCallsValueTextMeshProUGUI.text = value.GetNumberString(0);
        }

        private void OnVertextsSliderValueChanged(float value)
        {
            currentConfig.vertexts = (int)value;
            vertexsValueTextMeshProUGUI.text = value.GetNumberString(0);
        }

        private void OnTrianglesSliderValueChanged(float value)
        {
            currentConfig.triangles = (int)value;
            trianglesValueTextMeshProUGUI.text = value.GetNumberString(0);
        }

        private void OnMemorySliderValueChanged(float value)
        {
            currentConfig.memory = (int)value;
            memoryValueTextMeshProUGUI.text = value.GetNumberString(0);
        }

        private void OnFpsSliderValueChanged(float value)
        {
            currentConfig.fps = (int)value;
            fpsValueTextMeshProUGUI.text = value.GetNumberString(0);
        }

        #endregion

    }
}
