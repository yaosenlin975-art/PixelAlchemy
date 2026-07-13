/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: SpriteSlincer
└──────────────┘
*/

using UnityEngine;
using UnityEditor;
using System.IO;
using Lin.Editor.Helper;
using System.Collections.Generic;
using Lin.Runtime.Helper;
using System.Linq;

namespace Lin.Editor.SpriteTool
{
    public static class SpriteMenuItems
    {
        private const int 默认动画帧率 = 12;
        private const int 单元像素 = 33;

        [MenuItem("Assets/精灵图工具/把Multi拆解为单个图")]
        private static void ProcessToSprite()
        {
            ToMultiple();

            Texture2D image = Selection.activeObject as Texture2D;//获取旋转的对象
            string rootPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(image));//获取路径名称
            string path = rootPath + "/" + image.name + ".PNG";//图片路径名称


            TextureImporter texImp = AssetImporter.GetAtPath(path) as TextureImporter;//获取图片入口


            AssetDatabase.CreateFolder(rootPath, image.name);//创建文件夹


            foreach (SpriteMetaData metaData in texImp.spritesheet)//遍历小图集
            {
                Texture2D myimage = new Texture2D((int)metaData.rect.width, (int)metaData.rect.height);

                //abc_0:(x:2.00, y:400.00, width:103.00, height:112.00)
                for (int y = (int)metaData.rect.y; y < metaData.rect.y + metaData.rect.height; y++)//Y轴像素
                {
                    for (int x = (int)metaData.rect.x; x < metaData.rect.x + metaData.rect.width; x++)
                        myimage.SetPixel(x - (int)metaData.rect.x, y - (int)metaData.rect.y, image.GetPixel(x, y));
                }


                //转换纹理到EncodeToPNG兼容格式
                if (myimage.format != TextureFormat.ARGB32 && myimage.format != TextureFormat.RGB24)
                {
                    Texture2D newTexture = new Texture2D(myimage.width, myimage.height);
                    newTexture.SetPixels(myimage.GetPixels(0), 0);
                    myimage = newTexture;
                }
                var pngData = myimage.EncodeToPNG();


                //AssetDatabase.CreateAsset(myimage, rootPath + "/" + image.name + "/" + metaData.name + ".PNG");
                File.WriteAllBytes(rootPath + "/" + image.name + "/" + metaData.name + ".PNG", pngData);
                // 刷新资源窗口界面
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("Assets/精灵图工具/Icon")]
        private static void SpriteIcon()
        {
            Texture2D image = Selection.activeObject as Texture2D;
            string path = AssetDatabase.GetAssetPath(image);

            TextureImporter texImp = AssetImporter.GetAtPath(path) as TextureImporter;//获取图片入口
            string newPath = $"{Path.GetDirectoryName(path)}\\{Path.GetFileName(path)}-icon.png";
            if (texImp.spritesheet.Length == 0)
                File.Copy(path, newPath);
            else
            {
                var metaData = texImp.spritesheet[0];
                Texture2D myimage = new Texture2D((int)metaData.rect.width, (int)metaData.rect.height);

                for (int y = (int)metaData.rect.y; y < metaData.rect.y + metaData.rect.height; y++)//Y轴像素
                {
                    for (int x = (int)metaData.rect.x; x < metaData.rect.x + metaData.rect.width; x++)
                        myimage.SetPixel(x - (int)metaData.rect.x, y - (int)metaData.rect.y, image.GetPixel(x, y));
                }

                //转换纹理到EncodeToPNG兼容格式
                if (myimage.format != TextureFormat.ARGB32 && myimage.format != TextureFormat.RGB24)
                {
                    Texture2D newTexture = new Texture2D(myimage.width, myimage.height);
                    newTexture.SetPixels(myimage.GetPixels(0), 0);
                    myimage = newTexture;
                }
                var pngData = myimage.EncodeToPNG();
                File.WriteAllBytes(newPath, pngData);
            }
            AssetDatabase.Refresh();

            texImp = AssetImporter.GetAtPath(newPath) as TextureImporter;
            texImp.textureType = TextureImporterType.Sprite;
            texImp.spritePixelsPerUnit = 100;
            AssetDatabase.Refresh();
        }

        private static void ToMultiple()
        {
            var directory = PathHelper.GetSelectedPathOrFallback();
            var files = Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                TextureImporter importer = AssetImporter.GetAtPath(file) as TextureImporter;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                if (importer.spritesheet.Length == 0)
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
                    int partCount = texture.width / texture.height;
                    SpriteMetaData[] datas = new SpriteMetaData[partCount];
                    for (int i = 0; i < partCount; i++)
                        datas[i] = new SpriteMetaData()
                        {
                            name = $"{texture.name}_{i}",
                            rect = new Rect(i * texture.height, 0, texture.height, texture.height)
                        };

                    importer.spritesheet = datas;
                }
                importer.SaveAndReimport();
            }
            AssetDatabase.Refresh();
        }

        private static IEnumerable<Texture2D> LoadAllTextureFromSelection()
        {
            var selectedObjects = Selection.objects;
            if (selectedObjects.Length == 0)
                return null;

            var folders = selectedObjects.Where(obj => obj is DefaultAsset);
            var result = selectedObjects.Where(obj => obj is Texture2D).Select(c => c as Texture2D);
            foreach (var folder in folders)
            {
                var folderPath = AssetDatabase.GetAssetPath(folder);
                var folderTextures = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid)));

                result = result.Union(folderTextures);
            }

            return result;
        }

        [MenuItem("Assets/精灵图工具/统一单元像素")]
        private static void ModifyPixelsPerUnit()
        {
            var textures = LoadAllTextureFromSelection();
            if (textures is null)
                return;

            foreach (var texture in textures)
            {
                var path = AssetDatabase.GetAssetPath(texture);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);

                if (importer != null && importer.spritePixelsPerUnit != 单元像素)
                {
                    importer.spritePixelsPerUnit = 单元像素; // 设置PixelsPerUnit为33
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport(); // 保存并重新导入
                }
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 创建动画片段的主入口方法
        /// 通过Unity菜单项触发，处理选中的纹理或文件夹
        /// </summary>
        [MenuItem("Assets/精灵图工具/精灵图转动画")]
        private static void CreateClips()
        {
            var textures = LoadAllTextureFromSelection();
            if (textures is null)
                return;

            var folderDict = new Dictionary<string, List<Texture2D>>();
            foreach (var texture in textures)
            {
                string directory = Path.GetFileName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(texture)));
                if (!folderDict.TryGetValue(directory, out var list))
                {
                    list = new List<Texture2D>();
                    folderDict.Add(directory, list);
                }
                list.Add(texture);
            }

            foreach (var value in folderDict.Values)
                CreateAnimationClips(value);
        }

        [MenuItem("Assets/精灵图工具/分割精灵图")]
        private static void SplitSelection()
        {
            var selectedObject = Selection.activeObject;
            if (selectedObject is null)
            {
                Log.Warning(nameof(SpriteMenuItems), "请选择精灵图或带有精灵图的文件夹");
                return;
            }

            string path = AssetDatabase.GetAssetPath(selectedObject);
            SpriteSpliterWindow.ShowSpliter(path);
        }

        /// <summary>
        /// 为纹理列表创建动画片段
        /// </summary>
        /// <param name="textures">要处理的纹理列表</param>
        private static void CreateAnimationClips(List<Texture2D> textures)
        {
            var singleSprites = new List<Sprite>();

            foreach (var texture in textures)
            {
                var path = AssetDatabase.GetAssetPath(texture);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;

                if (importer.spriteImportMode == SpriteImportMode.Single)
                {
                    singleSprites.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
                }
                else if (importer.spriteImportMode == SpriteImportMode.Multiple)
                {
                    CreateClipsForMultipleSprites(importer);
                }
            }

            if (singleSprites.Count > 0)
            {
                CreateClipForMultipleSingleSprites(singleSprites);
            }
        }

        /// <summary>
        /// 为多个Single模式的Sprite创建单个动画片段
        /// </summary>
        /// <param name="sprites">Sprite列表</param>
        private static void CreateClipForMultipleSingleSprites(List<Sprite> sprites)
        {
            var clip = new AnimationClip();
            var directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(sprites.First()));
            clip.name = sprites.First().name;
            clip.frameRate = 默认动画帧率;

            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

            var curve = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                curve[i] = new ObjectReferenceKeyframe
                {
                    time = i / clip.frameRate,
                    value = sprites[i]
                };
            }
            AddEventForSpecialClip(clip);

            AnimationUtility.SetObjectReferenceCurve(clip, binding, curve);
            directory = Path.GetFileName(Path.GetDirectoryName(directory));
            SaveClip(clip, $"Assets/Arts/动画/{directory}/{Path.GetFileName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(sprites.First())))}/{clip.name}.anim");
        }

        private static void AddEventForSpecialClip(AnimationClip clip)
        {
            var name = clip.name.ToLower();
            if (name.Contains("attack"))
            {
                AddEvent(clip, "AttackEffectEvent");
            }
            else if (name.Contains("run") || name.Contains("walk") || name.Contains("move"))
            {
                AddEvent(clip, "StepEvent");
            }
        }

        private static void AddEvent(AnimationClip clip, string eventName)
        {
            var stepEvent = new AnimationEvent
            {
                time = 0,
                functionName = eventName,
                messageOptions = SendMessageOptions.RequireReceiver
            };
            AnimationUtility.SetAnimationEvents(clip, new[] { stepEvent });
        }

        /// <summary>
        /// 为包含多个精灵的纹理创建多个动画片段
        /// </summary>
        /// <param name="importer">纹理导入器</param>
        private static void CreateClipsForMultipleSprites(TextureImporter importer)
        {
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(importer.assetPath).OfType<Sprite>().ToArray();
            var clip = new AnimationClip();
            clip.name = Path.GetFileNameWithoutExtension(importer.assetPath);
            var directory = Path.GetDirectoryName(importer.assetPath);
            clip.frameRate = 默认动画帧率;

            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var curve = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                curve[i] = new ObjectReferenceKeyframe
                {
                    time = i / clip.frameRate,
                    value = sprites[i]
                };
            }
            AddEventForSpecialClip(clip);

            AnimationUtility.SetObjectReferenceCurve(clip, binding, curve);
            SaveClip(clip, $"Assets/Arts/动画/{Path.GetFileName(Path.GetDirectoryName(directory))}/{Path.GetFileName(directory)}/{clip.name}.anim");
        }

        /// <summary>
        /// 保存动画片段到指定路径
        /// </summary>
        /// <param name="clip">要保存的动画片段</param>
        private static void SaveClip(AnimationClip clip, string outputPath)
        {
            Log.Debug(nameof(SpriteMenuItems), $"创建动画 {outputPath}", clip);
            var directory = Path.GetDirectoryName(outputPath);
            IOHelper.InsureExist(directory, false, false);
            //TODO:动画是否直接覆盖
            AssetDatabase.CreateAsset(clip, outputPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/精灵图工具/打包图集", false, 2000)]
        public static void PackSpriteAtlas()
        {
            var selections = Selection.objects;
            if (selections == null || selections.Length == 0)
            {
                UnityEditor.EditorUtility.DisplayDialog("提示", "请先选择需要打包的资源！", "确定");
                return;
            }

            HashSet<TextureImporter> textures = new HashSet<TextureImporter>();
            foreach (var obj in selections)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                if (obj is DefaultAsset && AssetDatabase.IsValidFolder(path))
                {
                    string[] guids = AssetDatabase.FindAssets("t:texture", new[] { path });
                    foreach (var guid in guids)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        var importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as TextureImporter;
                        if (importer != null)
                            textures.Add(importer);
                    }
                }
                else if (obj is Texture2D || obj is Sprite)
                {
                    var importer = UnityEditor.AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null)
                        textures.Add(importer);
                }
            }

            if (textures.Count == 0)
            {
                UnityEditor.EditorUtility.DisplayDialog("提示", "未找到可打包的图片资源！", "确定");
                return;
            }

            string atlasName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(selections[0]));
            SpriteAtlasHelper.PackSpriteAtlas(textures, atlasName);
            Debug.Log("已打包图集");
        }
    }
}