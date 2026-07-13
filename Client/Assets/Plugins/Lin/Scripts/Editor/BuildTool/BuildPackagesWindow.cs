//Description: AssetBundle打包
using ZLinq;
using System;
using YooAsset;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;
using System.CodeDom;
using YooAsset.Editor;
using Newtonsoft.Json;
using Lin.Runtime.Const;
using Lin.Editor.Helper;
using System.Reflection;
using Lin.Runtime.Helper;
using Lin.Editor.Settings;
using UnityEngine.Scripting;
using Sirenix.OdinInspector;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using Sirenix.OdinInspector.Editor;
using System.Threading.Tasks;

namespace Lin.Editor.BuildTool
{
    class BuildPackagesWindow : OdinEditorWindow
    {
        [SerializeField, HideLabel] private BuildPackagesSettings settings;
        private const string BUILD_PACKAGES_TARGETS_KEY = "BUILD_PACKAGES_TARGETS_KEY";

        [TableList, LabelText("打包目标"), SerializeField]
        private List<PackageConfig> targets;

        [MenuItem("Build/Res/Build With Infos #b")]
        private static void Init()
        {
            var window = GetWindow<BuildPackagesWindow>();
            window.Show();
        }

        [MenuItem("Build/Res/Build All Packages %#b")]
        private static void QuickBuild()
        {
            var buildSettings = PrefsHelper.Get<BuildPackagesSettings>(BuildPackagesSettings.KEY, defaultValue: null);
            if (buildSettings is null)
                throw new Exception();

            var collector = YooAssetHelper.LoadAssetBundleCollectorSetting();
            var targets = PrefsHelper.Get(BUILD_PACKAGES_TARGETS_KEY, new List<PackageConfig>());
            foreach (var package in collector.Packages)
            {
                var count = package.Groups.Sum(g => g.Collectors.Count);
                if (count == 0)
                    continue;

                int index = targets.FindIndex(t => t.packageName == package.PackageName);
                if (index == -1)
                    throw new Exception($"请先在 'Build With Infos' 中设置 {package.PackageName} 是否为默认包");
            }
            var type = GetEncryTypes().Find(c => buildSettings.encryType.Contains(c.Name));
            IEncryptionServices encryption = null;
            if (type != null)
                encryption = (IEncryptionServices)Activator.CreateInstance(type);

            ExecuteBuilds(buildSettings, targets);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            //Settings
            settings = PrefsHelper.Get(BuildPackagesSettings.KEY, new BuildPackagesSettings());

            targets = PrefsHelper.Get(BUILD_PACKAGES_TARGETS_KEY, new List<PackageConfig>());
            var collector = YooAssetHelper.LoadAssetBundleCollectorSetting();
            foreach (var package in collector.Packages)
            {
                var count = package.Groups.Sum(g => g.Collectors.Count);
                if (count == 0)
                    continue;

                int index = targets.FindIndex(t => t.packageName == package.PackageName);
                if (index == -1)
                    targets.Add(new PackageConfig() { packageName = package.PackageName, build = true });
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            PrefsHelper.Set(BuildPackagesSettings.KEY, settings);
            PrefsHelper.Set(BUILD_PACKAGES_TARGETS_KEY, targets);
        }

        [Button, ButtonGroup]
        public void All()
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var config = targets[i];
                config.build = true;
                targets[i] = config;
            }
        }

        [Button, ButtonGroup]
        public void None()
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var config = targets[i];
                config.build = false;
                targets[i] = config;
            }
        }

        [Button]
        public void Build()
        {
            PrefsHelper.Set(BuildPackagesSettings.KEY, settings);
            ExecuteBuilds(settings, targets);
        }

        private static async void ExecuteBuilds(BuildPackagesSettings settings, List<PackageConfig> targets)
        {
            var collector = YooAssetHelper.LoadAssetBundleCollectorSetting();

            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string outputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            string version = settings.useTimeStamp ? $"{settings.version} {IOHelper.GetLocalTimeStamp()}" : settings.version;

            // 生成脚本，脚本内只有一个string[]
            if (GlobalConfig_SO.GetInstance())
                CreateScript4DefaultPackageArray(targets);

            // 收集shader变体
            if (settings.collectShaderVariants)
                await ShaderVariantsCollector.CollectShaders(targets);

            // 输出配置文件
            if (settings.extraCopy)
            {
                string destination = $"{settings.extraPath}/{EditorUserBuildSettings.activeBuildTarget}";
                IOHelper.InsureExist(destination, false);
                File.WriteAllText($"{destination}/{ResourceConst.VERSION_FILE_NAME}", version);
#if !UNITY_WEBGL
                File.WriteAllText($"{destination}/{ResourceConst.VERSION_INFOS_FILE_NAME}", JsonConvert.SerializeObject(settings.infos));
#endif
            }

            Dictionary<string, bool> results = new Dictionary<string, bool>();
            var compressSettings = AssetsCompressSettings.GetInstance();
            foreach (var target in targets)
            {
                if (!target.build)
                    continue;

                // 资源配置
                HashSet<string> assetPaths = new HashSet<string>();
                foreach (var group in collector.GetPackage(target.packageName).Groups)
                {
                    var key = YooAssetHelper.GetGroupPath(target.packageName, group.GroupName);
                    var groupSettings = compressSettings.Get(key);
                    groupSettings.SetGroupAssets(key, group);
                    assetPaths.Clear();
                }

                // WebGL: 若 StreamingAssets/yoo/{packageName} 缺失, 先按当前 pipeline 打包一份并写入
                EnsureWebGLBuildinPackage(target.packageName, settings, version, outputRoot);
                // 打包
                BuildPackage(target.packageName);
            }

            string outputDir = $"{settings.extraPath}/{EditorUserBuildSettings.activeBuildTarget}";
            CombineLink($"{outputRoot}/{EditorUserBuildSettings.activeBuildTarget}", settings.pipelineType);

            bool shouldBuildApp = LinkHelper.ShouldBuildApplication();
#if HybridCLR
            if (settings.compileHybrid)
            {
                AssetDatabase.Refresh();
                HybridCLRCommands.BuildAndCopyABAOTHotUpdateDlls();
                AssetDatabase.Refresh();

                // 再次对存有dll的包进行打包
                var package = collector.GetPackage(GlobalConfig_SO.DEFAULT_PACKAGE_NAME);
                if (package is null)
                {
                    Debug.LogError("请先将 Assets/Prefabs/Hotfix 文件夹加入收集器中");
                    return;
                }
                YooAssetHelper.ClearBundles();
                // 上面主循环已 build 过一次(产 link.xml), 这里 BuildPackage 会再次 results.Add, 必须先 Remove 避免键冲突
                results.Remove(package.PackageName);
                if (!BuildPackage(package.PackageName))
                {
                    Debug.LogError("Failed to build code bundle.");
                    return;
                }
                HybridCLRCommands.CustomSettings();
                shouldBuildApp |= HybridCLRCommands.ShouldBuildApplication();
            }
#endif

            YooAsset.Editor.EditorTools.ClearUnityConsole();
            System.Diagnostics.Process.Start(outputDir);
            Debug.Log($"{results.Count}个包，全部打包成功。");
            if (shouldBuildApp)
                Debug.Log("有引用到新的dll, <b>游戏</b>需要重新打包");

            bool BuildPackage(string packageName)
            {
                var isSucceeded = BundleBuildPipelineHelper.Run(packageName, version, null, settings);
                results.Add(packageName, isSucceeded);

                if (isSucceeded)
                {
                    if (settings.extraCopy)
                    {
                        string outputSource = $"{outputRoot}/{EditorUserBuildSettings.activeBuildTarget}/{packageName}/{version}";
                        string destination = $"{settings.extraPath}/{EditorUserBuildSettings.activeBuildTarget}/{packageName}";

                        IOHelper.InsureExist(destination, false, true);
                        // Parallel.ForEach 并行复制文件
                        Parallel.ForEach(Directory.GetFiles(outputSource), new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Environment.ProcessorCount - 1
                        },
                        file =>
                        {
                            File.Copy(file, $"{destination}/{Path.GetFileName(file)}", true);
                        });
                    }
                }
                else
                    Debug.LogError($"Failed to build {packageName}.");

                return isSucceeded;
            }
        }

        /// <summary>
        /// WebGL 环境: 若 StreamingAssets/yoo/{packageName} 中没有 BuildinCatalog, 则按 EditorSimulateBuildPipeline 打包一份并复制过去
        /// 供 WebGL 离线模式 / DefaultBuildinFileSystem 读取
        /// </summary>
        private static void EnsureWebGLBuildinPackage(string packageName, BuildPackagesSettings settings, string version, string outputRoot)
        {
#if UNITY_WEBGL
            var streamingRoot = Path.Combine(Application.streamingAssetsPath, "yoo", packageName);
            var catalogPath = Path.Combine(streamingRoot, "BuildinCatalog.bytes");
            if (File.Exists(catalogPath))
            {
                Log.Debug(nameof(BuildPackagesWindow), $"[WebGL] {packageName} 已在 StreamingAssets/yoo 中, 跳过预打包");
                return;
            }

            Log.Debug(nameof(BuildPackagesWindow), $"[WebGL] {packageName} 在 StreamingAssets/yoo 中不存在, 开始预打包(EditorSimulateBuildPipeline)...");

            var isSucceeded = BundleBuildPipelineHelper.Run(packageName, version, null, settings, EBuildPipeline.EditorSimulateBuildPipeline);
            if (!isSucceeded)
            {
                Log.Error(nameof(BuildPackagesWindow), $"[WebGL] {packageName} 预打包失败, 跳过写入 StreamingAssets/yoo");
                return;
            }

            var platform = EditorUserBuildSettings.activeBuildTarget.ToString();
            var sourceDir = $"{outputRoot}/{platform}/{packageName}/{version}";
            if (!Directory.Exists(sourceDir))
            {
                Log.Error(nameof(BuildPackagesWindow), $"[WebGL] {packageName} 找不到构建产物: {sourceDir}");
                return;
            }

            IOHelper.InsureExist(streamingRoot, false, true);
            Parallel.ForEach(Directory.GetFiles(sourceDir), new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount - 1
            },
            file =>
            {
                File.Copy(file, $"{streamingRoot}/{Path.GetFileName(file)}", true);
            });
            Log.Debug(nameof(BuildPackagesWindow), $"[WebGL] {packageName} 已写入 StreamingAssets/yoo");

            // 关键: 清理 simulate build 写到 outputRoot 的 version 目录, 否则下一步 BuildPackage 的 TaskPrepare
            // 会因 "Package outout directory exists" 抛异常导致主 build 失败
            Directory.Delete(sourceDir, true);
#endif
        }

        private static void CombineLink(string outputDir, EBuildPipeline pipeline)
        {
            if (pipeline != EBuildPipeline.ScriptableBuildPipeline)
                return;

            try
            {
                var links = Directory.GetFiles(outputDir, "*.xml", SearchOption.AllDirectories);
                LinkHelper.Combine(links);
            }
            catch (Exception ex)
            {
                Log.Error(nameof(BuildPackagesWindow), ex.Message);
            }
        }

        private static List<Type> GetEncryTypes() => YooAsset.Editor.EditorTools.GetAssignableTypes(typeof(IEncryptionServices));

        private static void CreateScript4DefaultPackageArray(List<PackageConfig> targets)
        {
            string scriptPath = $"{EditorSettings_SO.GetInstance().cSharpOutput}/DefaultPackages.cs";
            string scriptName = "DefaultPackages";
            CodeTypeDeclaration classScript = new CodeTypeDeclaration(scriptName);
            classScript.IsClass = true;
            classScript.TypeAttributes = TypeAttributes.Public;
            classScript.CustomAttributes.Add(new CodeAttributeDeclaration(typeof(PreserveAttribute).FullName));
            CodeMemberField arrayField = new CodeMemberField(typeof(string[]), "packages");
            arrayField.Attributes = MemberAttributes.Static | MemberAttributes.Public;
            arrayField.InitExpression = new CodeArrayCreateExpression(
                typeof(string[]),
                targets.Select(t => new CodePrimitiveExpression(t.packageName)).ToArray()
                );
            classScript.Members.Add(arrayField);

            //命名空间
            CodeCompileUnit unit = new CodeCompileUnit();
            CodeNamespace space = new CodeNamespace("Config");
            space.Types.Add(classScript);
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
            new FileInfo(scriptPath).InsureExist(true);
            using StreamWriter writer = new StreamWriter(scriptPath, false, Encoding.GetEncoding("UTF-8"));
            provider.GenerateCodeFromCompileUnit(unit, writer, options);
        }
    }
}
