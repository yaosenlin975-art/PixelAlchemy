/*
┌────────────────────────────┐
│　Description: APK打包
│　Remark: 
└────────────────────────────┘
*/
using Lin.Editor.Helper;
using Lin.Runtime.Const;
using Lin.Runtime.Helper;
using Lin.Runtime.Resource.Updater;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lin.Editor.BuildTool
{
    public class BuildApplicationWindow : OdinEditorWindow
    {
        [SerializeField, HideLabel] BuildApplicationSettings settings;
        private const string BUILD_APPLICAITON_WINDOW_SIZE_KEY = nameof(BUILD_APPLICAITON_WINDOW_SIZE_KEY);


        [MenuItem("Build/App/Build With Infos")]
        public static void Init()
        {
            var window = GetWindow<BuildApplicationWindow>("游戏打包");
            window.Show();
            window.minSize = Vector2.one * 400;
        }

        [MenuItem("Build/App/Quick Build")]
        public static void QuickBuild()
        {
            var settings = PrefsHelper.Get(BuildApplicationSettings.BUILD_APP_SETTINGS_KEY, BuildApplicationSettings.Create());
            ExecuteBuild(settings);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            LoadSettings();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            SaveSettings();
        }

        private void SaveSettings()
        {
            PrefsHelper.Set(BuildApplicationSettings.BUILD_APP_SETTINGS_KEY, settings);
            PrefsHelper.Set(BUILD_APPLICAITON_WINDOW_SIZE_KEY, new float[] { position.x, position.y, position.width, position.height });
            PrefsHelper.Set(BuildApplicationSettings.INSTALL_TO_SIMULATOR_KEY, settings.install);
        }

        private void LoadSettings()
        {
            settings = PrefsHelper.Get(BuildApplicationSettings.BUILD_APP_SETTINGS_KEY, BuildApplicationSettings.Create());

            var posArray = PrefsHelper.Get(BUILD_APPLICAITON_WINDOW_SIZE_KEY, new float[] { position.x, position.y, 400, 400 });
            Rect pos = new Rect(posArray[0], posArray[1], posArray[2], posArray[3]);
            position = pos;
        }

        [Button]
        private void Build()
        {
            SaveSettings(); 
            ExecuteBuild(settings);
        }

        public static void ExecuteBuild(BuildApplicationSettings settings)
        {
            PlayerSettings.bundleVersion = $"{settings.version}.{IOHelper.GetLocalTimeStamp()}";
            string dir = $"{settings.output}/{EditorUserBuildSettings.activeBuildTarget}";
            string versionPath = $"{dir}/{ResourceConst.VERSION_FILE_NAME}";
            string apkPath = BuildTools.Build(dir, out var isSucceeded);
            if (!isSucceeded)
            {
                Debug.LogError("打包失败");
                return;
            }
            //version
            File.WriteAllText(versionPath, PlayerSettings.bundleVersion);
            //description
            string jsonPath = $"{dir}/{ResourceConst.VERSION_INFOS_FILE_NAME}";
            string md5 = IOHelper.GetMD5(apkPath);
#if UNITY_ANDROID
            var apkDescription = settings.Translate();
            apkDescription.md5 = md5;
            apkDescription.apkSize = new FileInfo(apkPath).Length;
            File.WriteAllText(jsonPath, apkDescription.ToString());
            if (settings.extraCopy)
            {
                IOHelper.InsureExist(settings.extraOutput, false);
                File.Copy(versionPath, Path.Combine(settings.extraOutput, Path.GetFileName(versionPath)), true);
                File.Copy(apkPath, Path.Combine(settings.extraOutput, Path.GetFileName(apkPath)), true);
                File.Copy(jsonPath, Path.Combine(settings.extraOutput, Path.GetFileName(jsonPath)), true);
                
                // 检测并创建adb安装.bat文件
                CreateAdbInstallBatIfNotExists(settings.extraOutput);
            }
#endif

#if HybridCLR
            HybridCLRCommands.SavePatchAotAssemblies();
#endif
            LinkHelper.SaveMD5();
            Debug.Log($"打包完成, 版本为: {PlayerSettings.bundleVersion}");
        }

        /// <summary>
        /// 检测并创建adb安装.bat文件
        /// </summary>
        /// <param name="outputPath">输出路径</param>
        private static void CreateAdbInstallBatIfNotExists(string outputPath)
        {
            string batFilePath = Path.Combine(outputPath, "adb安装.bat");
            
            // 检测文件是否存在
            if (!File.Exists(batFilePath))
            {
                // 创建bat文件内容
                string batContent = 
                    @"@echo off
echo 正在安装APK文件...
adb install -r update.apk
if %errorlevel% equ 0 (
    echo 安装成功！
) else (
    echo 安装失败，请检查设备连接和APK文件。
)
echo.
echo 按任意键退出...
pause >nul";
                
                // 写入文件
                File.WriteAllText(batFilePath, batContent, System.Text.Encoding.UTF8);
                Debug.Log($"已创建adb安装.bat文件: {batFilePath}");
            }
            else
            {
                Debug.Log($"adb安装.bat文件已存在: {batFilePath}");
            }
        }
    }

    [Serializable]
    public class BuildApplicationSettings
    {
        public const string BUILD_APP_SETTINGS_KEY = nameof(BUILD_APP_SETTINGS_KEY);

        public const string INSTALL_TO_SIMULATOR_KEY = nameof(INSTALL_TO_SIMULATOR_KEY);

        [LabelText("版本号")]
        public string version;

        [LabelText("输出地址"), FolderPath(AbsolutePath = false, RequireExistingPath = true)]
        public string output;

        [LabelText("复制到额外地址"), VerticalGroup]
        public bool extraCopy;

        [HideLabel, VerticalGroup, ShowIf(nameof(extraCopy)), FolderPath(AbsolutePath = true, RequireExistingPath = true)]
        public string extraOutput;

        [LabelText("安装到模拟器")]
        public bool install;

        [LabelText("更新说明"), TextArea]
        public List<string> infos;

        public static BuildApplicationSettings Create()
        {
            var settings = new BuildApplicationSettings();
            settings.version = "0.0.1";
            settings.output = Application.dataPath.Replace("Assets", "Bin");
            return settings;
        }

#if UNITY_ANDROID
        public AndroidVersionDescriptions Translate()
        {
            AndroidVersionDescriptions descriptions = new AndroidVersionDescriptions();
            descriptions.descriptions = infos?.ToArray() ?? new string[0];
            return descriptions;
        }
#endif
    }
}