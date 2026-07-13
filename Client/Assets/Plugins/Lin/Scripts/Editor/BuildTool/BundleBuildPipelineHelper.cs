using UnityEditor;
using YooAsset;
using YooAsset.Editor;
using BuildParameters = YooAsset.Editor.BuildParameters;

namespace Lin.Editor.BuildTool
{
    public static class BundleBuildPipelineHelper
    {
        public static bool Run(string packageName, string version, IEncryptionServices encryption, BuildPackagesSettings settings)
            => Run(packageName, version, encryption, settings, settings.pipelineType);

        public static bool Run(string packageName, string version, IEncryptionServices encryption, BuildPackagesSettings settings, EBuildPipeline pipelineType)
        {
            var parameters = GetBuildParameters(packageName, version, encryption, settings, pipelineType);
            var pipeline = GetBuildPipeline(pipelineType);
            var result = pipeline.Run(parameters, true);
            if (result.Success)
                UnityEditor.EditorUtility.RevealInFinder(result.OutputPackageDirectory);
            return result.Success;
        }

        private static BuildParameters GetBuildParameters(string packageName, string version, IEncryptionServices encryption, BuildPackagesSettings settings)
            => GetBuildParameters(packageName, version, encryption, settings, settings.pipelineType);

        private static BuildParameters GetBuildParameters(string packageName, string version, IEncryptionServices encryption, BuildPackagesSettings settings, EBuildPipeline pipelineType)
        {
            BuildParameters parameters;
            switch (pipelineType)
            {
                case EBuildPipeline.ScriptableBuildPipeline:
                    parameters = new ScriptableBuildParameters()
                    {
                        CompressOption = settings.compressOption,
                        BuiltinShadersBundleName = GetBuiltinShaderBundleName(packageName),
                        TrackSpriteAtlasDependencies = true
                    };
                    break;

                case EBuildPipeline.BuiltinBuildPipeline:
                    parameters = new BuiltinBuildParameters() { CompressOption = settings.compressOption };
                    break;

                case EBuildPipeline.EditorSimulateBuildPipeline:
                    parameters = new EditorSimulateBuildParameters();
                    break;

                case EBuildPipeline.RawFileBuildPipeline:
                default:
                    parameters = new RawFileBuildParameters();
                    break;
            }
            parameters.BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            parameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            parameters.BuildPipeline = pipelineType.ToString();
            parameters.BuildBundleType = 2;
            parameters.BuildTarget = EditorUserBuildSettings.activeBuildTarget;
            parameters.PackageName = packageName;
            parameters.PackageVersion = version;
            parameters.EnableSharePackRule = true;
            parameters.VerifyBuildingResult = true;
            parameters.FileNameStyle = settings.fileNameStyle;
#if !UNITY_WEBGL
            parameters.BuildinFileCopyOption = settings.buildinFileCopyOption;
#endif
            parameters.BuildinFileCopyParams = string.Empty;
            parameters.ClearBuildCacheFiles = settings.clearBuildCacheFiles;
            parameters.UseAssetDependencyDB = settings.useAssetDependencyDB;
            parameters.EncryptionServices = encryption;

            return parameters;
        }

        private static IBuildPipeline GetBuildPipeline(EBuildPipeline pipelineType)
        {
            switch (pipelineType)
            {
                case EBuildPipeline.ScriptableBuildPipeline:
                    return new ScriptableBuildPipeline();

                case EBuildPipeline.BuiltinBuildPipeline:
                    return new BuiltinBuildPipeline();

                case EBuildPipeline.EditorSimulateBuildPipeline:
                    return new EditorSimulateBuildPipeline();

                case EBuildPipeline.RawFileBuildPipeline:
                default:
                    return new RawFileBuildPipeline();
            }
        }

        /// <summary>
        /// 内置着色器资源包名称
        /// 注意：和自动收集的着色器资源包名保持一致！
        /// </summary>
        private static string GetBuiltinShaderBundleName(string packageName)
        {
            var uniqueBundleName = AssetBundleCollectorSettingData.Setting.UniqueBundleName;
            var packRuleResult = DefaultPackRule.CreateShadersPackRuleResult();
            return packRuleResult.GetBundleName(packageName, uniqueBundleName);
        }
    }
}