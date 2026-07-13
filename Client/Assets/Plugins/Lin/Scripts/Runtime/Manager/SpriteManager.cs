/*
┌────────────────────────────┐
│　Description: 图集管理
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: SpriteManager
└──────────────┘
*/
using Cysharp.Threading.Tasks;
using Cysharp.Text;
using UnityEngine;
using Lin.Runtime.Resource;
using Lin.Runtime.Helper;
using System.Collections.Generic;
using UnityEngine.U2D;

namespace Lin.Runtime.Manager
{
    public static class SpriteManager
    {
        private static Dictionary<string, SpriteAtlas> atlasMap = new Dictionary<string, SpriteAtlas>();
        const int ATLAS_LOAD_TIMEOUT_MS = 5000;

        public static async UniTask<Sprite> GetAsync(string atlasName, string spriteName)
        {
            if (!atlasMap.ContainsKey(atlasName))
            {
                //Assets/Prefabs/SpriteAtlas
                string path = $"Assets/Prefabs/SpriteAtlas/{atlasName}.spriteatlas";
                var package = ResLoader.CheckLocationValid(path);
                if (package is null)
                {
                    Log.Error(nameof(SpriteManager), ZString.Format("图集 {0} 不存在", atlasName));
                    return null;
                }
                atlasMap.Add(atlasName, null);
                var spriteAtlas = await package.LoadAssetAsync<SpriteAtlas>(path);
                atlasMap[atlasName] = spriteAtlas;
            }

            int waited = 0;
            while (atlasMap[atlasName] == null)
            {
                await UniTask.Delay(10);
                waited += 10;
                if (waited >= ATLAS_LOAD_TIMEOUT_MS)
                {
                    Log.Error(nameof(SpriteManager), ZString.Format("图集 {0} 加载超时", atlasName));
                    return null;
                }
            }

            return atlasMap[atlasName].GetSprite(spriteName);
        }

#if !UNITY_WEBGL
        public static Sprite Get(string atlasName, string spriteName)
        {
            if (!atlasMap.TryGetValue(atlasName, out var spriteAtlas))
            {
                //Assets/Prefabs/SpriteAtlas
                string path = $"Assets/Prefabs/SpriteAtlas/{atlasName}.spriteatlas";
                spriteAtlas = ResLoader.LoadAsset<SpriteAtlas>(path);
                if (spriteAtlas == null)
                    Log.Error(nameof(SpriteManager), ZString.Format("图集 {0} 不存在", atlasName));
                atlasMap.Add(atlasName, spriteAtlas);
            }

            if (spriteAtlas is null)
                return null;

            return atlasMap[atlasName].GetSprite(spriteName);
        }
#endif
    }
}
