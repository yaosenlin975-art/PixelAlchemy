/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/


using Lin.Editor.BuildTool;
using Lin.Runtime.Helper;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lin.Editor
{
    [InitializeOnLoad]
    public static class MissingShaderVariantSuppler
    {

        public const string MISSING_SHADER_VARIANTS_DIR = "MissingShaderVariants";

        static MissingShaderVariantSuppler()
        {
            EventHelper.Register<BeforeBuildPackagesEvent>(BeforeBuildPackages);
        }

        private static void BeforeBuildPackages(BeforeBuildPackagesEvent @event) => Supply();

        [MenuItem("Lin/Shader/Supply Missing Variants")]
        public static void Supply()
        {
            if (!Directory.Exists(MISSING_SHADER_VARIANTS_DIR))
            {
                Debug.LogError($"Directory not found: {MISSING_SHADER_VARIANTS_DIR}");
                return;
            }
            var missingVariants = new Dictionary<string, Dictionary<string, List<List<string>>>>();
            var files = Directory.GetFiles(MISSING_SHADER_VARIANTS_DIR, "*.json");

            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var variants = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<List<string>>>>>(json);
                    if (variants != null)
                    {
                        MergeVariants(missingVariants, variants);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to process file {file}: {e.Message}");
                }
            }

            if (missingVariants.Count == 0)
            {
                Debug.LogWarning("No variants found in json files.");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Supply Missing Variants Report:");

            int totalAdded = 0;

            // 处理 "Unknown" 变体集
            Dictionary<string, List<List<string>>> unknownVariants = null;
            if (missingVariants.ContainsKey("Unknown"))
            {
                unknownVariants = missingVariants["Unknown"];
                missingVariants.Remove("Unknown");
            }

            var settings = YooAssetHelper.LoadAssetBundleCollectorSetting();
            foreach (var packagePair in missingVariants)
            {
                if (!settings.FindPackage(packagePair.Key, out var result))
                    continue;

                string packageName = result.PackageName;
                var shaderMap = packagePair.Value;
                int packageAddedCount = 0;

                string assetPath = $"Assets/Prefabs/Shadervariants/{packageName}.shadervariants";
                ShaderVariantCollection collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(assetPath);

                if (collection == null)
                {
                    Debug.LogWarning($"ShaderVariantCollection not found for package '{packageName}' at {assetPath}. Creating new one.");
                    // Ensure directory exists
                    string dir = Path.GetDirectoryName(assetPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    collection = new ShaderVariantCollection();
                    AssetDatabase.CreateAsset(collection, assetPath);
                }

                AddVariants(collection, shaderMap, ref packageAddedCount);

                if (packageAddedCount > 0)
                {
                    totalAdded += packageAddedCount;
                    sb.AppendLine($"  - Package '{packageName}': Added {packageAddedCount} variants");
                    EditorUtility.SetDirty(collection);
                }
            }

            // 将 "Unknown" 变体集添加到所有 ShaderVariantCollection
            if (unknownVariants != null)
            {
                int unknownAddedTotal = 0;
                string[] guids = AssetDatabase.FindAssets("t:ShaderVariantCollection");
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    ShaderVariantCollection collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
                    if (collection != null)
                    {
                        int count = 0;
                        AddVariants(collection, unknownVariants, ref count);
                        if (count > 0)
                        {
                            EditorUtility.SetDirty(collection);
                            unknownAddedTotal += count;
                        }
                    }
                }

                if (unknownAddedTotal > 0)
                {
                    totalAdded += unknownAddedTotal;
                    sb.AppendLine($"  - Unknown variants added to all collections: {unknownAddedTotal} instances");
                }
            }

            AssetDatabase.SaveAssets();
            sb.Insert(0, $"Supply Missing Variants Complete. Total Added: {totalAdded}\n");
            Debug.Log(sb.ToString());
            string mergedPath = Path.Combine(MISSING_SHADER_VARIANTS_DIR, "merged.json");
            try
            {
                string mergedJson = JsonConvert.SerializeObject(missingVariants, Formatting.Indented);
                File.WriteAllText(mergedPath, mergedJson);
                Debug.Log($"Merged variants json written: {mergedPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to write merged json: {e.Message}");
            }

            foreach (var file in files)
            {
                if (file.Contains("merged"))
                    continue;

                File.Delete(file);
            }
        }

        private static void AddVariants(ShaderVariantCollection collection, Dictionary<string, List<List<string>>> shaderMap, ref int addedCount)
        {
            foreach (var shaderPair in shaderMap)
            {
                string shaderName = shaderPair.Key;
                List<List<string>> variants = shaderPair.Value;

                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogError($"Shader not found: {shaderName}");
                    continue;
                }

                foreach (var keywords in variants)
                {
                    string[] keywordArray = keywords.ToArray();

                    // URP 项目主要使用 ScriptableRenderPipeline
                    try
                    {
                        var variant = new ShaderVariantCollection.ShaderVariant(shader, PassType.ScriptableRenderPipeline, keywordArray);
                        if (!collection.Contains(variant))
                        {
                            collection.Add(variant);
                            addedCount++;
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Failed to add variant for shader {shaderName}: {e.Message}");
                    }
                }
            }
        }

        private static void MergeVariants(
            Dictionary<string, Dictionary<string, List<List<string>>>> target,
            Dictionary<string, Dictionary<string, List<List<string>>>> source)
        {
            foreach (var pkgPair in source)
            {
                if (!target.ContainsKey(pkgPair.Key))
                {
                    target[pkgPair.Key] = new Dictionary<string, List<List<string>>>();
                }

                var targetPkg = target[pkgPair.Key];

                foreach (var shaderPair in pkgPair.Value)
                {
                    if (!targetPkg.ContainsKey(shaderPair.Key))
                    {
                        targetPkg[shaderPair.Key] = new List<List<string>>();
                    }

                    var targetShaderVariants = targetPkg[shaderPair.Key];

                    foreach (var newVariant in shaderPair.Value)
                    {
                        if (!ContainsVariant(targetShaderVariants, newVariant))
                        {
                            targetShaderVariants.Add(newVariant);
                        }
                    }
                }
            }
        }

        private static bool ContainsVariant(List<List<string>> existingVariants, List<string> newVariant)
        {
            var newSet = new HashSet<string>(newVariant);
            foreach (var existing in existingVariants)
            {
                if (existing.Count == newVariant.Count && newSet.SetEquals(existing))
                {
                    return true;
                }
            }
            return false;
        }
    }
}