/*
┌────────────────────────────┐
│　Description: Hybrid dll加载器
│　Remark: 
└────────────────────────────┘
*/
using Lin.Runtime.Helper;
using Lin.Runtime.Event;

#if HybridCLR
using UnityEngine.Scripting;
using Cysharp.Threading.Tasks;
#if !UNITY_EDITOR
using Cysharp.Text;
using HybridCLR;
using Lin.Runtime.Const;
using System.Reflection;
using UnityEngine;
using Lin.Runtime.Resource;
#endif

namespace Lin.Runtime
{
    [Preserve]
    public static class HybridLoader
    {
        public async static UniTask Load()
        {
            var title = nameof(HybridLoader);
            //加载
#if !UNITY_EDITOR
            var globel = GlobalConfig_SO.GetInstance();
            Log.Debug(nameof(HybridLoader), "加载热更程序集");

            string[] aotDlls = (await ResLoader.LoadTextAssetAsync(ConfigConst.AOT_ASSEMBLIES_FILE)).text.Split("\r\n");

            /// 注意, 补充元数据是给AOT dll补充元数据, 而不是给热更新dll补充元数据。
            foreach (var aotDllName in aotDlls)
            {
                if (string.IsNullOrEmpty(aotDllName))
                    continue;

                //Assets/Prefabs/Hotfix/??.dll.bytes
                string dllName = aotDllName.EndsWith(".dll.bytes") ? aotDllName : (aotDllName + ".dll.bytes");
                try
                {
                    var dllBytes = await ResLoader.LoadTextAssetAsync(ZString.Concat(globel.dllFolder, '/', dllName));
                    var err = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes.bytes, HomologousImageMode.SuperSet);
                    Log.Debug(title, $"LoadMetadataForAOTAssembly:{aotDllName}. ret:{err}");
                }
                catch (System.Exception e)
                {
                    Log.Error(title, $"LoadMetadataForAOTAssembly:{aotDllName}, ret:{e.Message}");
                }
            }

            //加载热更DLL
            string[] hotfixDlls = (await ResLoader.LoadTextAssetAsync(ConfigConst.HOTFIX_ASSEMBLIES_FILE)).text.Split("\r\n");
            foreach (var dll in hotfixDlls)
            {
                if (string.IsNullOrEmpty(dll))
                    continue;

                var dllText = await ResLoader.LoadTextAssetAsync(ZString.Concat(globel.dllFolder, '/', dll));
                Assembly.Load(dllText.bytes);

                Log.Debug(title, $"LoadHotfixAssembliy: {dll}");
            }
            Log.Debug(title, "Load dlls complete.");
#else
            Log.Debug(title, "无需加载dll");
#endif
            new HybridLoadFinishedEvent().Dispatch();
            await UniTask.CompletedTask;
        }
    }
}
#endif