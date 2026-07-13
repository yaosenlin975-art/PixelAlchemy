/*
┌────────────────────────────┐
│　Description: 编译监测, 插件安装检测
└────────────────────────────┘
*/
using Lin.Editor.BuildTool;
using Lin.Runtime.Helper;
using Lin.Runtime.Resource;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
#if YooAsset
using YooAsset.Editor;
#endif

namespace Lin.Editor
{
    [InitializeOnLoad]
    public static class EditorTools
    {
        private const string GAME_LAUNCHER_SCRIPT_PATH = "Assets/Scripts/Game.cs";
        private static ConcurrentQueue<Action> mainThreadTasks;

        static EditorTools()
        {
            mainThreadTasks = new ConcurrentQueue<Action>();
            mainThreadTasks.Enqueue(CheckMainSceneExist);
            mainThreadTasks.Enqueue(CheckGlobeSettingExist);
            mainThreadTasks.Enqueue(CheckGameScriptExist);
            mainThreadTasks.Enqueue(CheckEnvironment);
            mainThreadTasks.Enqueue(CheckPackages);
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            try
            {
                while (mainThreadTasks.TryDequeue(out var task))
                    task();
            }
            catch (Exception e)
            {
                Log.Error("EditorTools", $"操作执行失败: {e.Message}");
            }
        }

#if YooAsset
        // 检测默认资源包
        private static void CheckPackages()
        {
            var globalSettings = GlobalConfig_SO.GetInstance();
            var collectorSettings = YooAssetHelper.LoadAssetBundleCollectorSetting();
            if (collectorSettings == null)
            {
                collectorSettings = ScriptableObject.CreateInstance<AssetBundleCollectorSetting>();
                AssetDatabase.CreateAsset(collectorSettings, YooAssetHelper.SETTINGS_PATH);
                AssetDatabase.Refresh();
            }

            CheckUpdatePackage();
            CheckDefaultPackage();

            // 更新界面包
            void CheckUpdatePackage()
            {
                foreach (var package in collectorSettings.Packages)
                {
                    if (package.PackageName == ResLoader.UPDATE_PANEL_PACKAGE_NAME)
                        return;
                }

                var newPackage = new AssetBundleCollectorPackage
                {
                    SupportExtensionless = true,
                    AutoCollectShaders = true,
                    EnableAddressable = true,
                    PackageName = ResLoader.UPDATE_PANEL_PACKAGE_NAME,
                    PackageDesc = "更新界面包, 将更新界面从初始包体中分离"
                };

                // UI
                var uiGroup = AddGroup("UI", "界面", newPackage);
                {
                    var uiDir = "Assets/Plugins/Lin/Prefabs/UI";
                    IOHelper.InsureExist(uiDir, false);
                    AddAssetToGroup(uiGroup, uiDir);
                }

                collectorSettings.Packages.Add(newPackage);
                collectorSettings.EditorSave();
                Log.Debug("YooAsset", "补充Package: Update");
            }

            // 默认游戏资源包
            void CheckDefaultPackage()
            {
                foreach (var package in collectorSettings.Packages)
                {
                    if (package.PackageName == ResLoader.DEFAULT_PACKAGE_NAME)
                        return;
                }

                var newPackage = new AssetBundleCollectorPackage
                {
                    SupportExtensionless = true,
                    AutoCollectShaders = true,
                    EnableAddressable = true,
                    PackageName = ResLoader.DEFAULT_PACKAGE_NAME,
                    PackageDesc = "默认资源包, UI Code 一些配置 都直接放在该包中"
                };

                // Scene
                var sceneGroup = AddGroup("Scene", "场景", newPackage);
                AddAssetToGroup(sceneGroup, "Assets/Plugins/Lin/Scenes/Translating.unity");

                //UI
                var uiGroup = AddGroup("UI", "界面", newPackage);
                {
                    var uiDir = $"{globalSettings.prefabDirectory}/UI";
                    IOHelper.InsureExist(uiDir, false);
                    AddAssetToGroup(uiGroup, uiDir);
                }

#if HybridCLR
                // Code
                var codeGroup = AddGroup("Code", "热更程序集", newPackage);
                {
                    IOHelper.InsureExist(globalSettings.configDirectory, false);
                    AddAssetToGroup(codeGroup, globalSettings.configDirectory);
                }
#endif
                // Config
                AddGroup("Config", "配置文件", newPackage);

                // Shaders
                AddGroup("Shadervariants", "包体的Shader变体集", newPackage);

                collectorSettings.Packages.Add(newPackage);
                collectorSettings.EditorSave();
                Log.Debug("YooAsset", "补充Package: Game");
            }

            AssetBundleCollectorGroup AddGroup(string groupName, string groupDesc, AssetBundleCollectorPackage package)
            {
                var group = new AssetBundleCollectorGroup
                {
                    GroupName = groupName,
                    GroupDesc = groupDesc
                };
                package.Groups.Add(group);
                return group;
            }

            void AddAssetToGroup(AssetBundleCollectorGroup group, string assetPath)
            {
                group.Collectors.Add(new AssetBundleCollector
                {
                    CollectPath = assetPath,
                    CollectorType = ECollectorType.MainAssetCollector,
                    AddressRuleName = "AddressByFileName",
                    PackRuleName = "PackSeparately"
                });
            }
        }

        //检测主场景是否被包含
        private static void CheckMainSceneExist()
        {
            string mainScenePath = EditorConst.MAIN_SCENE_PATH;
            var buildScenes = EditorBuildSettings.scenes;

            if (buildScenes.Any(b => b.path == mainScenePath))
                return;

            EditorBuildSettingsScene mainScene = new EditorBuildSettingsScene(mainScenePath, true);
            ArrayUtility.Add(ref buildScenes, mainScene);
            EditorBuildSettings.scenes = buildScenes;
            Log.Debug(nameof(EditorTools), "Boot is added to BuildSettings.");
        }
#endif

        //检测配置文件是否存在
        private static void CheckGlobeSettingExist()
        {
            string path = "Assets/Resources/GlobeConfig.asset";
            if (File.Exists(path))
                return;

            IOHelper.InsureExist(Path.GetDirectoryName(path), false);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GlobalConfig_SO>(), path);
            AssetDatabase.Refresh();
        }

        //检测游戏启动脚本是否存在
        private static void CheckGameScriptExist()
        {
            if (Type.GetType("Hotfix.Game,Hotfix") is not null || Type.GetType("Game,Assembly-CSharp") is not null || File.Exists(GAME_LAUNCHER_SCRIPT_PATH))
                return;

            //生成代码
            string typeStr = "Game";

            CodeTypeDeclaration gameScript = new CodeTypeDeclaration(typeStr);
            gameScript.IsClass = true;
            gameScript.Attributes = MemberAttributes.Static | MemberAttributes.Public;

            gameScript.Comments.Add(new CodeCommentStatement("<summary>", true));
            gameScript.Comments.Add(new CodeCommentStatement("游戏逻辑启动", true));
            gameScript.Comments.Add(new CodeCommentStatement("</summary>", true));

            CodeMemberMethod method = new CodeMemberMethod();
            method.Name = "Start";
            method.Attributes = MemberAttributes.Static | MemberAttributes.Public;
            gameScript.Members.Add(method);

            CodeCompileUnit unit = new CodeCompileUnit();
#if HybridCLR
            CodeNamespace space = new CodeNamespace("Hotfix");
            method.Statements.Add(new CodeSnippetStatement("\t\t\tthrow new System.NotImplementedException(\"TODO:运行逻辑\");"));
#else
            CodeNamespace space = new CodeNamespace();
            method.Statements.Add(new CodeSnippetStatement("\t\tthrow new System.NotImplementedException(\"TODO:运行逻辑\");"));
#endif
            space.Types.Add(gameScript);
            unit.Namespaces.Add(space);

            // 获取C#语言的实例
            CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp");
            //代码生成器选项类
            CodeGeneratorOptions options = new CodeGeneratorOptions();
            //设置支撑的样式
            options.BracingStyle = "C";
            //在成员之间插入空行
            options.BlankLinesBetweenMembers = true;

            //生成最终代码
            IOHelper.InsureExist("Assets/Scripts", false, false);
            using (StreamWriter writer = new StreamWriter(GAME_LAUNCHER_SCRIPT_PATH, false, Encoding.GetEncoding("UTF-8")))
                provider.GenerateCodeFromCompileUnit(unit, writer, options);

            Log.Debug(nameof(EditorTools), "已生Game.cs");
        }

        //检测是否使用IL2CPP
        private static void CheckEnvironment()
        {
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            ScriptingImplementation curScriptingImplementation = PlayerSettings.GetScriptingBackend(buildTargetGroup);
            ScriptingImplementation targetScriptingImplementation = ScriptingImplementation.IL2CPP;
            if (curScriptingImplementation != targetScriptingImplementation)
            {
                Log.Error("[EditorStartup]", $"脚本编译环境设置为 {targetScriptingImplementation}");
                PlayerSettings.SetScriptingBackend(buildTargetGroup, targetScriptingImplementation);
            }
        }

        // 编辑器主线程任务
        public static void Add2MainThread(Action action) => mainThreadTasks.Enqueue(action);
    }
}