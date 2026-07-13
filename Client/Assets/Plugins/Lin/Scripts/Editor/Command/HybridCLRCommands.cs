#if HybridCLR
using Cysharp.Threading.Tasks;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Settings;
using Lin.Editor.Helper;
using Lin.Runtime.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using YooAsset.Editor;
using ZLinq;

namespace Lin.Editor.BuildTool
{
    public static class HybridCLRCommands
    {
        private const string PATCH_AOT_ASSEMBLIES_KEY = nameof(PATCH_AOT_ASSEMBLIES_KEY);
        private const string HOTFIX_ASSEMBLIES_KEY = nameof(HOTFIX_ASSEMBLIES_KEY);

        [MenuItem("HybridCLR/Compile dll and copy to Prefabs #h")]
        public static void BuildAndCopyABAOTHotUpdateDlls()
        {
            CompileDllCommand.CompileDll(EditorUserBuildSettings.activeBuildTarget);
            CopyDllsToPrefabs();
        }

        public static void CopyDllsToPrefabs()
        {
            var collector = YooAssetHelper.LoadAssetBundleCollectorSetting();
            var package = collector.GetPackage(GlobalConfig_SO.DEFAULT_PACKAGE_NAME);
            AssetBundleCollectorGroup group = package.InsureGroupExist("Code");
            group.GroupDesc = "热更程序集";

            var folder = GlobalConfig_SO.GetInstance().dllFolder;
            IOHelper.InsureExist(folder, false, true);

            CopyAOTAssembliesToPrefabs(group, folder);
            CopyHotUpdateAssembliesToPrefabs(group, folder);

            AddCollector(group, folder);
            collector.EditorSave();
        }

        public static void CopyAOTAssembliesToPrefabs(AssetBundleCollectorGroup group, string folder)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            string aotAssembliesSrcDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);

            var hybridSettingAOTs = SettingsUtil.AOTAssemblyNames;
            HashSet<string> dllSet = new HashSet<string>();
            AddDlls();

            bool shouldRefreshSettings = false;

            foreach (var dll in dllSet)
            {
                string srcDllPath = dll.EndsWith(".dll") ? $"{aotAssembliesSrcDir}/{dll}" : $"{aotAssembliesSrcDir}/{dll}.dll";
                if (!File.Exists(srcDllPath))
                {
                    bool hasAss = true;
                    var dllName = dll.Replace(".dll", string.Empty);
                    try
                    {
                        System.Reflection.Assembly.Load(dllName);
                    }
                    catch (FileNotFoundException)
                    {
                        hasAss = false;
                    }

                    if (!hasAss)
                    {
                        Debug.LogError($"Bundle中添加AOT补充元数据dll:{srcDllPath} 时发生错误, 程序集不存在。");
                        hybridSettingAOTs.Remove(dllName);
                        shouldRefreshSettings = true;
                    }
                    else
                        Debug.LogError($"Bundle中添加AOT补充元数据dll:{srcDllPath} 时发生错误, 文件不存在. 裁剪后的AOT dll在BuildPlayer时才能生成, 因此需要你先构建一次游戏App后再打包。");

                    continue;
                }
                string dllBytesPath = $"{folder}/{dll}.dll.bytes";
                File.Copy(srcDllPath, dllBytesPath, true);
                Debug.Log($"[Copy AOT-Assemblies To Prefabs] copy AOT dll {srcDllPath} -> {dllBytesPath}");

                if (shouldRefreshSettings)
                {
                    var hybridSettings = HybridCLRSettings.LoadOrCreate();
                    hybridSettings.patchAOTAssemblies = hybridSettingAOTs.ToArray();
                    hybridSettings.EditorSave(true);
                }
            }

            //生成预留的元数据DLL列表文件
            string path = $"{folder}/AOTAssemblies.txt";
            IOHelper.InsureExist(new FileInfo(path), true);
            AddDlls();
            File.WriteAllLines(path, dllSet);

            void AddDlls()
            {
                dllSet.Clear();
                dllSet.UnionWith(hybridSettingAOTs);
                dllSet.UnionWith(GetAOTAssemblyList());
                dllSet.UnionWith(LinkHelper.GetAssemblies());
            }
        }

        public static void CopyHotUpdateAssembliesToPrefabs(AssetBundleCollectorGroup group, string folder)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            string hotfixDllSrcDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);

            foreach (var dll in SettingsUtil.HotUpdateAssemblyFilesExcludePreserved)
            {
                string dllPath = $"{hotfixDllSrcDir}/{dll}";
                string dllBytesPath = $"{folder}/{dll}.bytes";
                File.Copy(dllPath, dllBytesPath, true);
                Debug.Log($"[Copy HotUpdate-Assemblies To Prefabs] copy hotfix dll {dllPath} -> {dllBytesPath}");
            }

            //生成热更DLL列表文件
            string path = $"{folder}/HotfixAssemblies.txt";
            IOHelper.InsureExist(new FileInfo(path), true);
            File.WriteAllLines(path, SettingsUtil.HotUpdateAssemblyFilesExcludePreserved.Select(d => d += ".bytes"));
        }

        private static void AddCollector(AssetBundleCollectorGroup group, string assetPath)
        {
            var collector = group.Collectors.Find(c => c.CollectPath == assetPath);
            if (collector == null)
            {
                collector = new AssetBundleCollector()
                {
                    CollectPath = assetPath,
                    CollectorType = ECollectorType.MainAssetCollector,
                    AddressRuleName = "AddressByFileName",
                    PackRuleName = "PackSeparately"
                };
                group.Collectors.Add(collector);
            }
        }

        private static IEnumerable<string> GetAOTAssemblyList()
        {
            string className = "AOTGenericReferences,Hotfix";
            Type type = Type.GetType(className);
            if (type is null)
                return null;

            IReadOnlyList<string> list = type.GetField("PatchedAOTAssemblyList").GetValue(null) as IReadOnlyList<string>;
            return list.Select(d => d.Replace(".dll", string.Empty));
        }

        [MenuItem("HybridCLR/Custom Settings")]
        public static void CustomSettings()
        {
            var settings = HybridCLRSettings.LoadOrCreate();
            //热更程序集
            if (settings.hotUpdateAssemblyDefinitions is null)
                settings.hotUpdateAssemblyDefinitions = new AssemblyDefinitionAsset[0];
            AssemblyDefinitionAsset hotfix = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>("Assets/Scripts/Hotfix.asmdef");
            settings.hotUpdateAssemblyDefinitions = settings.hotUpdateAssemblyDefinitions.Union(new[] { hotfix }).ToArray();

            //PatchAOTAssemblies
            if (settings.patchAOTAssemblies is null)
                settings.patchAOTAssemblies = new string[0];

            settings.patchAOTAssemblies = settings.patchAOTAssemblies
                .Union(new string[]
                {
                    "mscorlib",
                    "UnityEngine.CoreModule",
                })
                .Union(LinkHelper.GetAssemblies())
                .ToArray();
            Array.Sort(settings.patchAOTAssemblies);

            //link.xml
            settings.outputLinkFile = EditorConst.LINK_PATH_4_HYBIRD;
            settings.outputAOTGenericReferenceFile = EditorConst.AOT_GENERIC_REFERENCE_FILE;

            HybridCLRSettings.Save();
            Debug.Log("自定义修改HybridSettings成功");
        }

        public static void SavePatchAotAssemblies()
        {
            var settings = HybridCLRSettings.LoadOrCreate();
            PrefsHelper.Set(PATCH_AOT_ASSEMBLIES_KEY, settings.patchAOTAssemblies);
            PrefsHelper.Set(HOTFIX_ASSEMBLIES_KEY, settings.hotUpdateAssemblies);
        }

        //是否应该重新打包App
        public static bool ShouldBuildApplication()
        {
            var settings = HybridCLRSettings.LoadOrCreate();

            return !AssembliesEquals(PrefsHelper.Get(PATCH_AOT_ASSEMBLIES_KEY, new string[0]), settings.patchAOTAssemblies)
                || !AssembliesEquals(PrefsHelper.Get(HOTFIX_ASSEMBLIES_KEY, new string[0]), settings.hotUpdateAssemblies);

        }

        //检测程序集是否相同
        private static bool AssembliesEquals(string[] inArchive, string[] current)
        {
            Array.Sort(inArchive);
            Array.Sort(current);

            var execpt = current.AsValueEnumerable().Except(inArchive);
            bool result = true;
            foreach (var item in execpt)
            {
                Log.Debug("Assemblies", $"New AOTAssembly: {item}");
                result = false;
            }

            return result;
        }
    }
}
#endif
