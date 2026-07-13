using Cysharp.Text;
using Lin.Editor.Helper;
using Lin.Editor.Settings;
using Lin.Editor.SpriteTool;
using Lin.Runtime.Helper;
using Lin.Runtime.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;
using ZLinq;

namespace Lin.Editor.UI
{
    public static class Generator
    {
        private const string GENERATE_ALTAS_TAG = nameof(GENERATE_ALTAS_TAG);

        [InitializeOnEnterPlayMode]
        public static void GenerateSpriteAltasBeforePlay()
        {
            var gc = GlobalConfig_SO.GetInstance();
            var uiFolder = $"{gc.prefabDirectory}/UI";
            var guids = AssetDatabase.FindAssets("t:prefab", new[] { uiFolder });

            HashSet<TextureImporter> toPack = new HashSet<TextureImporter>();
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var uiImporter = AssetImporter.GetAtPath(assetPath);
                if (uiImporter.ContainsUserTag(GENERATE_ALTAS_TAG))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                var graphics = prefab.GetComponentsInChildren<MaskableGraphic>();
                foreach (var g in graphics)
                {
                    if (g.mainTexture == null)
                        continue;

                    var texturePath = AssetDatabase.GetAssetPath(g.mainTexture);
                    if (!texturePath.StartsWith("Asset"))
                        continue;

                    toPack.Add(AssetImporter.GetAtPath(texturePath) as TextureImporter);
                }

                if (SpriteAtlasHelper.PackSpriteAtlas(toPack, prefab.name))
                {
                    uiImporter.AddUserTag(GENERATE_ALTAS_TAG, true);

                    var panel = prefab.GetComponent<PanelBase>();
                    foreach (var im in toPack.AsValueEnumerable())
                    {
                        var altasName = im.GetSpriteAtlasTag();
                        var altas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>($"{gc.prefabDirectory}/SpriteAtlas/{altasName}.spriteatlas");
                        if (!panel.usingAtlas.Contains(altas))
                            panel.usingAtlas.Add(altas);
                    }

                    panel.EditorSave();
                }
            }

            AssetDatabase.Refresh();
        }

        #region - Common -

        [Serializable]
        private abstract class GenerationInfoBase
        {
            public string targetGlobalObjectId;
            public int targetInstanceId;
            public string targetName;
            public string name;
            public string prefabFolder;
            public string scriptsFolder;
            public List<(string type, string objectName, string fieldName)> components;
            // 预制体资产内的子对象: GlobalObjectId 域重载后可能失效, 用资产路径+层级路径回退恢复
            public string targetAssetPath;
            public List<int> targetHierarchyPath;
        }

        private static (string scriptsFolder, string prefabsFolder) InsureFoldersExist()
        {
            var settings = EditorSettings_SO.GetInstance();
            var root = settings.cSharpOutput;

            // 创建目录
            var scriptsFolder = Path.Combine(root, "UI");
            var prefabsFolder = Path.Combine(GlobalConfig_SO.GetInstance().prefabDirectory, "UI");
            IOHelper.InsureExist(scriptsFolder, false);
            IOHelper.InsureExist(prefabsFolder, false);

            return (scriptsFolder, prefabsFolder);
        }

        // 在项目中搜索指定名称的资产, 返回第一个精确匹配的路径, 找不到返回 null
        // 用于检测脚本/预制体是否被移动, 避免重复生成
        private static string FindAssetPathByName(string fileNameWithExt)
        {
            var nameNoExt = Path.GetFileNameWithoutExtension(fileNameWithExt);
            var ext = Path.GetExtension(fileNameWithExt);
            var filter = ext switch
            {
                ".cs" => "t:MonoScript",
                ".prefab" => "t:Prefab",
                _ => string.Empty
            };
            var guids = AssetDatabase.FindAssets($"{nameNoExt} {filter}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).Equals(fileNameWithExt, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            return null;
        }

        // 存储目标对象的多种标识信息（用于 GlobalObjectId 失效时逐级回退恢复）
        private static void StoreTargetInfo(GameObject selectedObject, GenerationInfoBase info)
        {
            info.targetGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(selectedObject).ToString();
            info.targetInstanceId = selectedObject.GetInstanceID();
            info.targetName = selectedObject.name;

            var assetPath = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrEmpty(assetPath))
                return;

            info.targetAssetPath = assetPath;

            // 资产根对象不需要层级路径, 但子对象需要
            var rootAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (rootAsset == null || selectedObject == rootAsset)
                return;

            info.targetHierarchyPath = new List<int>();
            var current = selectedObject.transform;
            while (current != null && current != rootAsset.transform)
            {
                info.targetHierarchyPath.Add(current.GetSiblingIndex());
                current = current.parent;
            }
        }

        // GlobalObjectId 失效时, 通过资产路径 + 层级路径恢复预制体内的子对象
        // 返回值来自 LoadPrefabContents 的临时副本, 使用方需通过 SaveAsPrefabAsset + UnloadPrefabContents 持久化修改
        private static GameObject RecoverTargetFromAsset(GenerationInfoBase info)
        {
            if (string.IsNullOrEmpty(info.targetAssetPath) || info.targetHierarchyPath == null || info.targetHierarchyPath.Count == 0)
                return null;

            // 加载预制体的临时可编辑副本, 避免直接修改资产
            var root = PrefabUtility.LoadPrefabContents(info.targetAssetPath);
            if (root == null)
                return null;

            // 从根向下按 siblingIndex 路径找到目标子对象
            var current = root.transform;
            for (int i = info.targetHierarchyPath.Count - 1; i >= 0; i--)
            {
                var index = info.targetHierarchyPath[i];
                if (index < 0 || index >= current.childCount)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                    return null;
                }
                current = current.GetChild(index);
            }
            return current.gameObject;
        }

        private static void CompleteGeneration<TInfo>(string key, bool isUIPanel) where TInfo : GenerationInfoBase
        {
            var info = PrefsHelper.Get<TInfo>(key);
            if (info == null)
            {
                // Info 已被清空(用户主动 Discard 或流程已 Completed),无需处理
                return;
            }

            // 关键:原实现立即 DeleteKey 会让阶段二失败后无法续跑
            // 改为:流程 Completed 后再删 Info,失败时 Info + Checkpoint 都保留,让下次 [DidReloadScripts] 续跑
            var checkpoint = LoadCheckpoint<TInfo>() ?? new GenerationCheckpoint
            {
                infoKey = key,
                prefabName = info.name,
                prefabPath = $"{info.prefabFolder}/{info.name}.prefab"
            };
            // 确保 infoKey 总是最新(Info 的 key 与 PrefsArchive 类型一一对应,不会变)
            checkpoint.infoKey = key;

            // 续跑场景:日志提示,让用户能直观看到"我在从哪步续跑"
            if (checkpoint.completedStep != GenerationStep.None && checkpoint.completedStep != GenerationStep.Completed)
            {
                Debug.LogError($"[UI Generator] 检测到 {info.name} 上次中断在 {checkpoint.completedStep}, 错误: {checkpoint.lastError ?? "无"}, 自动从下一阶段续跑");
            }

            // 共享上下文:6 个跨步骤传递的可变状态收敛到一个对象
            // 优势:catch 中 SafeUnload(ctx.target, ctx.needsUnload) 签名更短,主体 try/catch 块视觉更整齐
            // 流程:每步开始时先检测 checkpoint.completedStep 决定是否跳过(幂等)
            var ctx = new GenerationContext();

            try
            {
                // -------------------- Step 1: RecoverTarget --------------------
                // 幂等:每次都重新执行,FindTarget 内部使用新实例的临时副本(原副本由 UnloadPrefabContents 释放)
                // 失败原因:ctx.target 已被删除、GlobalObjectId 解析失败、LoadPrefabContents 失败
                // 找不到时优先尝试 RecreateTarget 自动重建占位,重建失败才走 DiscardCheckpointAndInfo
                if (checkpoint.completedStep < GenerationStep.TargetRecovered)
                {
                    try
                    {
                        if (!FindTarget(info, out ctx.target, out ctx.sourcePrefabPath, out var contentsRoot))
                        {
                            if (!TryRecoverOrRecreate(info, ctx, key, isResume: false, isUIPanel: isUIPanel))
                                return;
                        }
                        else
                        {
                            // ctx.needsUnload 由 contentsRoot != null 推导
                            ctx.needsUnload = contentsRoot != null || ctx.sourcePrefabPath != null;
                        }
                        // 检测 target 是不是 prefab asset 根(Project 窗口选中 / Prefab Mode 内)
                        // 若是,Step5 跳过生成 prefabFilePath,Step6 原地写回原资产
                        ctx.isInPlaceSave = DetectInPlaceSave(info, ctx.target, out ctx.inPlaceAssetPath);
                        MarkStepComplete<TInfo>(checkpoint, GenerationStep.TargetRecovered,
                            info.name, checkpoint.prefabPath, ctx.sourcePrefabPath, ApplyBranch.None, ctx.isInPlaceSave);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UI Generator] Step1 RecoverTarget 异常: {FormatError(ex)}");
                        SafeUnload(ctx.target, ctx.needsUnload);
                        MarkStepFailed<TInfo>(checkpoint, FormatError(ex));
                        return;
                    }
                }
                else
                {
                    // 续跑场景:从 Checkpoint 还原 ctx.sourcePrefabPath,然后重新 FindTarget
                    // 注意:必须保留 FindTarget 的 sourcePrefabPath 输出(用于 ctx.needsUnload 推断),
                    //      不能用 out _ 丢弃,否则 LoadPrefabContents 临时副本不会被释放
                    ctx.sourcePrefabPath = checkpoint.sourcePrefabPath;
                    if (!FindTarget(info, out ctx.target, out var recoveredSourcePath, out var recoveredContentsRoot))
                    {
                        if (!TryRecoverOrRecreate<TInfo>(info, ctx, key, isResume: true, isUIPanel: isUIPanel))
                            return;
                        // 重建成功:同步 in-memory checkpoint(让后续 step 走"重新执行"分支而非"续跑跳过"分支)
                        // 持久化的 Checkpoint 已在 TryRecoverOrRecreate 内部 SaveCheckpoint 时落盘
                        checkpoint.completedStep = GenerationStep.None;
                        checkpoint.lastError = null;
                        checkpoint.prefabPath = $"{info.prefabFolder}/{info.name}.prefab";
                        checkpoint.sourcePrefabPath = null;
                    }
                    else
                    {
                        // FindTarget 给出的 sourcePrefabPath 才是真实值(可能与 Checkpoint 记录的一致)
                        ctx.sourcePrefabPath = recoveredSourcePath ?? checkpoint.sourcePrefabPath;
                        ctx.needsUnload = recoveredContentsRoot != null || ctx.sourcePrefabPath != null;
                    }
                    // 续跑:优先沿用 Checkpoint 的 isInPlaceSave(避免 RecreateTarget 重建后 DetectInPlaceSave 重新判定)
                    // 若 Checkpoint 没有记录(isInPlaceSave 默认 false),再用 DetectInPlaceSave 兜底
                    if (checkpoint.isInPlaceSave)
                    {
                        ctx.isInPlaceSave = true;
                        ctx.inPlaceAssetPath = info.targetAssetPath;
                    }
                    else
                    {
                        ctx.isInPlaceSave = DetectInPlaceSave(info, ctx.target, out ctx.inPlaceAssetPath);
                    }
                }

                // -------------------- Step 2: ResolveType --------------------
                // 幂等:反射是纯查询,可重做
                // 失败原因:用户生成的 .cs 编译报错(常见漏写 using、namespace 冲突)
                if (checkpoint.completedStep < GenerationStep.TypeResolved)
                {
                    try
                    {
                        ctx.type = ResolveGeneratedType(info);
                        if (ctx.type == null)
                        { 
                            var ns = GetNamespaceForPath(info.scriptsFolder);
                            var err = $"反射找不到类型 {ns}.{info.name} (通常 .cs 编译失败), 请修复后通过 Resume 续跑";
                            Debug.LogError($"[UI Generator] {err}\n  - {info.scriptsFolder}/{info.name}.Generate.cs\n  - {info.scriptsFolder}/{info.name}.cs");
                            SafeUnload(ctx.target, ctx.needsUnload);
                            MarkStepFailed<TInfo>(checkpoint, err);
                            return;
                        }
                        MarkStepComplete<TInfo>(checkpoint, GenerationStep.TypeResolved,
                            info.name, checkpoint.prefabPath, ctx.sourcePrefabPath, ApplyBranch.None, ctx.isInPlaceSave);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UI Generator] Step2 ResolveType 异常: {FormatError(ex)}");
                        SafeUnload(ctx.target, ctx.needsUnload);
                        MarkStepFailed<TInfo>(checkpoint, FormatError(ex));
                        return;
                    }
                }
                else
                {
                    // 续跑:重做反射(成本低,确保拿到最新 type)
                    ctx.type = ResolveGeneratedType(info);
                    if (ctx.type == null)
                    {
                        var err = "续跑时反射类型失败, 请确认脚本已正确编译";
                        Debug.LogError($"[UI Generator] {err}");
                        MarkStepFailed<TInfo>(checkpoint, err);
                        return;
                    }
                }

                // -------------------- Step 3: AttachComponent --------------------
                // 幂等:已挂载则跳过 AddComponent,GetComponent 会返回非空
                // 失败原因:组件类型不兼容 ctx.target,极少
                if (checkpoint.completedStep < GenerationStep.ComponentAttached)
                {
                    try
                    {
                        if (!TryAttachComponent(ctx.target, ctx.type, out var comp))
                        {
                            var err = "AddComponent 失败, 目标 GameObject 不兼容该组件类型";
                            Debug.LogError($"[UI Generator] {err}");
                            SafeUnload(ctx.target, ctx.needsUnload);
                            MarkStepFailed<TInfo>(checkpoint, err);
                            return;
                        }
                        MarkStepComplete<TInfo>(checkpoint, GenerationStep.ComponentAttached,
                            info.name, checkpoint.prefabPath, ctx.sourcePrefabPath, ApplyBranch.None, ctx.isInPlaceSave);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UI Generator] Step3 AttachComponent 异常: {FormatError(ex)}");
                        SafeUnload(ctx.target, ctx.needsUnload);
                        MarkStepFailed<TInfo>(checkpoint, FormatError(ex));
                        return;
                    }
                }
                // else: 已挂载,幂等跳过

                // -------------------- Step 4: ComponentReset --------------------
                // 幂等:Reset 每次都重新执行(重新绑定子节点引用),无副作用
                // 失败原因:Reset 内部调用 GetComponentsInChildren 失败(对象被销毁)
                if (checkpoint.completedStep < GenerationStep.ComponentReset)
                {
                    try
                    {
                        var comp = ctx.target.GetComponent(ctx.type);
                        if (comp == null)
                        {
                            // 异常:ctx.target 被外部销毁,无法续跑 Reset
                            var err = "Step4 找不到已挂载的组件, 目标对象可能已销毁";
                            Debug.LogError($"[UI Generator] {err}");
                            SafeUnload(ctx.target, ctx.needsUnload);
                            MarkStepFailed<TInfo>(checkpoint, err);
                            return;
                        }
                        InvokeReset(comp);
                        MarkStepComplete<TInfo>(checkpoint, GenerationStep.ComponentReset,
                            info.name, checkpoint.prefabPath, ctx.sourcePrefabPath, ApplyBranch.None, ctx.isInPlaceSave);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UI Generator] Step4 ComponentReset 异常: {FormatError(ex)}");
                        SafeUnload(ctx.target, ctx.needsUnload);
                        MarkStepFailed<TInfo>(checkpoint, FormatError(ex));
                        return;
                    }
                }
                else
                {
                    // 续跑:重新 Reset(幂等,保证字段绑定到当前子节点)
                    var comp = ctx.target.GetComponent(ctx.type);
                    if (comp != null) InvokeReset(comp);
                }

                // -------------------- Step 5: PrefabSaved --------------------
                // 幂等:先检查 prefab 是否已存在,存在则直接加载到引用,跳过 SaveAsPrefabAsset
                // 失败原因:磁盘满、权限、PrefabUtility 内部错误
                if (checkpoint.completedStep < GenerationStep.PrefabSaved)
                {
                    try
                    {
                        ctx.prefabAsset = TrySavePrefab(ctx.target, ctx.type, ctx.needsUnload, checkpoint.prefabPath, info, ctx.isInPlaceSave);
                        if (ctx.prefabAsset == null)
                        {
                            var err = $"SaveAsPrefabAsset 失败 {checkpoint.prefabPath}, 详见上方日志";
                            Debug.LogError($"[UI Generator] {err}");
                            SafeUnload(ctx.target, ctx.needsUnload);
                            MarkStepFailed<TInfo>(checkpoint, err);
                            return;
                        }
                        MarkStepComplete<TInfo>(checkpoint, GenerationStep.PrefabSaved,
                            info.name, checkpoint.prefabPath, ctx.sourcePrefabPath, ApplyBranch.None, ctx.isInPlaceSave);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UI Generator] Step5 PrefabSaved 异常: {FormatError(ex)}");
                        SafeUnload(ctx.target, ctx.needsUnload);
                        MarkStepFailed<TInfo>(checkpoint, FormatError(ex));
                        return;
                    }
                }
                else
                {
                    // 续跑:原地保存路径,没有 prefabFilePath 文件,直接用 target 作为 prefabAsset
                    if (ctx.isInPlaceSave)
                    {
                        ctx.prefabAsset = ctx.target;
                        Debug.Log($"[UI Generator] 续跑: 原地保存路径, prefabAsset = target");
                    }
                    else
                    {
                        // 续跑:加载已存在的 prefab(幂等)
                        ctx.prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(checkpoint.prefabPath);
                        if (ctx.prefabAsset == null)
                        {
                            // prefab 已被外部删了:留断点无意义,直接清掉让用户重新跑
                            var err = $"续跑时找不到已保存的预制体 {checkpoint.prefabPath}, 已被外部删除";
                            SafeUnload(ctx.target, ctx.needsUnload);
                            DiscardCheckpointAndInfo<TInfo>(key, err);
                            return;
                        }
                    }
                }

                // 选中预制体:即使在 Step6 之前失败, 至少能看到产物
                Selection.activeObject = ctx.prefabAsset;
                Debug.Log($"[UI Generator] 已生成{info.name}的脚本并保存预制体 {checkpoint.prefabPath}");

                // -------------------- Step 6: PrefabApplied --------------------
                // 不幂等,4 个分支:PrefabInstance / SourcePrefabReplace / PrefabAsset / SceneObject
                // 续跑时按 checkpoint.applyBranch 选择策略
                if (checkpoint.completedStep < GenerationStep.PrefabApplied)
                {
                    try
                    {
                        if (!TryApplyPrefab(ctx.target, ctx.prefabAsset, ctx.type, info, ctx.needsUnload, ctx.sourcePrefabPath, ctx.isRecreated, ctx.isInPlaceSave, ctx.inPlaceAssetPath, out ctx.applyBranch))
                        {
                            var err = "ApplyPrefab 失败, 详见上方日志";
                            Debug.LogError($"[UI Generator] {err}");
                            // Step6 失败时,资源可能已部分破坏,不再 Unload(留给用户排查)
                            MarkStepFailed<TInfo>(checkpoint, err);
                            return;
                        }
                        MarkStepComplete<TInfo>(checkpoint, GenerationStep.PrefabApplied,
                            info.name, checkpoint.prefabPath, ctx.sourcePrefabPath, ctx.applyBranch, ctx.isInPlaceSave);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UI Generator] Step6 PrefabApplied 异常: {FormatError(ex)}");
                        // Step6 失败通常意味着破坏性操作已部分执行, 不 Unload
                        MarkStepFailed<TInfo>(checkpoint, FormatError(ex));
                        return;
                    }
                }
                else
                {
                    // 续跑:按 ctx.applyBranch 做幂等保护,避免重复 DestroyImmediate 等破坏性操作
                    ctx.applyBranch = checkpoint.applyBranch;
                    Debug.Log($"[UI Generator] Step6 续跑(分支 {ctx.applyBranch}), 跳过重复 Apply");
                }

                // -------------------- Step 7: Cleanup & Complete --------------------
                // 幂等:Unload 重复调用安全(Unity 内部会忽略已释放的临时副本)
                try
                {
                    // 续跑时可能 ctx.needsUnload 仍然 true(临时副本未释放)
                    if (ctx.needsUnload && ctx.target != null)
                    {
                        // 续跑场景下 ctx.target 可能已被 DestroyImmediate(Step6 某些分支会销毁 ctx.target)
                        // 用 try-catch 兜底,避免重复 Unload 抛 ArgumentException
                        try { PrefabUtility.UnloadPrefabContents(ctx.target.transform.root.gameObject); }
                        catch { /* 已释放,忽略 */ }
                    }
                    Selection.activeObject = ctx.prefabAsset;

                    // 全部完成,清理 Info + Checkpoint
                    PrefsHelper.DeleteKey<TInfo>(key);
                    DeleteCheckpoint<TInfo>();
                    Debug.Log($"[UI Generator] {info.name} 生成流程完成 ✅");
                }
                catch (Exception ex)
                {
                    // 清理失败不应阻塞"已成功"的状态:仅记录日志
                    Debug.LogError($"[UI Generator] Step7 Cleanup 异常: {FormatError(ex)}");
                }
            }
            catch (Exception ex)
            {
                // 兜底:任何未捕获的异常都记录到 Checkpoint,绝不抛出
                // 关键:必须 SafeUnload,避免 LoadPrefabContents 临时副本泄漏
                Debug.LogError($"[UI Generator] CompleteGeneration 未知异常: {FormatError(ex)}");
                SafeUnload(ctx.target, ctx.needsUnload);
                MarkStepFailed<TInfo>(checkpoint, FormatError(ex));
            }
        }

        // ============== Step 1 子方法: 4 级回退找 target ==============

        // 与原版完全等价, 但改为 out 参数形式, 便于异常分支处理
        // contentsRoot: 来自 LoadPrefabContents 的临时副本根, 续跑时需要 Unload
        // 返回 false 表示找不到
        private static bool FindTarget(GenerationInfoBase info, out GameObject target, out string sourcePrefabPath, out GameObject contentsRoot)
        {
            target = null;
            sourcePrefabPath = null;
            contentsRoot = null;

            // 通过 GlobalObjectId 恢复 target: 跨域重载 / 跨 Editor 会话稳定
            if (!string.IsNullOrEmpty(info.targetGlobalObjectId)
                && GlobalObjectId.TryParse(info.targetGlobalObjectId, out var targetGlobalId))
            {
                target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(targetGlobalId) as GameObject;
            }

            // 回退1: 通过 InstanceId 恢复（同一编辑会话内有效, 域重载后失效, 但作为保险尝试）
            if (target == null && info.targetInstanceId != 0)
            {
                target = EditorUtility.InstanceIDToObject(info.targetInstanceId) as GameObject;
            }

            // 回退2: 通过资产路径 + 层级路径恢复（预制体资产内的子对象）
            if (target == null)
            {
                target = RecoverTargetFromAsset(info);
                if (target != null)
                {
                    sourcePrefabPath = info.targetAssetPath;
                    contentsRoot = target.transform.root.gameObject;
                }
            }

            // 回退3: 通过名称在预制体资产内搜索
            if (target == null && !string.IsNullOrEmpty(info.targetAssetPath) && !string.IsNullOrEmpty(info.targetName))
            {
                var rootAsset = AssetDatabase.LoadAssetAtPath<GameObject>(info.targetAssetPath);
                if (rootAsset != null)
                {
                    if (rootAsset.name == info.targetName)
                    {
                        contentsRoot = PrefabUtility.LoadPrefabContents(info.targetAssetPath);
                        target = contentsRoot;
                    }
                    else
                    {
                        foreach (var t in rootAsset.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.name == info.targetName)
                            {
                                contentsRoot = PrefabUtility.LoadPrefabContents(info.targetAssetPath);
                                foreach (var ct in contentsRoot.GetComponentsInChildren<Transform>(true))
                                {
                                    if (ct.name == info.targetName)
                                    {
                                        target = ct.gameObject;
                                        sourcePrefabPath = info.targetAssetPath;
                                        break;
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
            }

            return target != null;
        }

        // 检测 target 是不是 prefab asset 根(用户直接在 prefab 上编辑生成)
        // 命中条件:用户编辑的 prefab asset(info.targetAssetPath)与生成路径(info.prefabFolder/info.name.prefab)相同
        // 此时 Step5 跳过生成 prefabFilePath,Step6 原地写回原资产(避免"删除原 prefabFilePath + 创建新文件" 的破坏性操作)
        // 注:FindTarget 把 target 加载为 LoadPrefabContents 临时副本,AssetDatabase.GetAssetPath(target) 返回空,所以用 info 路径 + 生成路径比对来判定
        private static bool DetectInPlaceSave(GenerationInfoBase info, GameObject target, out string assetPath)
        {
            assetPath = null;
            if (target == null || info == null) return false;

            // 必须有目标 asset 路径(场景对象没有 asset 路径,不在原地写回范围)
            if (string.IsNullOrEmpty(info.targetAssetPath)) return false;

            // 用户的源 prefab 路径 == 输出目标路径 => 同一个文件,原地写回
            var prefabFilePath = $"{info.prefabFolder}/{info.name}.prefab";
            if (!string.Equals(info.targetAssetPath, prefabFilePath, StringComparison.OrdinalIgnoreCase))
                return false;

            assetPath = info.targetAssetPath;
            return true;
        }

        // ============== Step 2 子方法: 反射查找生成的 Type ==============

        private static Type ResolveGeneratedType(GenerationInfoBase info)
        {
            // 类型命名空间跟随输出位置所在程序集变化, 不再硬编码 "UI."
            var typeName = $"{GetNamespaceForPath(info.scriptsFolder)}.{info.name}";
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }

        // ============== Step 3 子方法: AddComponent (幂等) ==============

        private static bool TryAttachComponent(GameObject target, Type type, out Component component)
        {
            // 幂等:已挂载则直接返回,避免重复 AddComponent 触发 OnEnable 副作用
            component = target.GetComponent(type);
            if (component != null) return true;
            component = target.AddComponent(type);
            return component != null;
        }

        // ============== Step 5 子方法: SaveAsPrefabAsset (幂等) ==============

        // 续跑时 prefab 可能已存在,直接加载到引用
        // isInPlaceSave=true 时,跳过 SaveAsPrefabAsset 到 prefabFilePath,返回 target 自身作为"原地写回"标记
        // 原地写回时 Step6 通过判断 prefabAsset == target 走原资产路径,不再生成 prefabFilePath 文件
        private static GameObject TrySavePrefab(GameObject target, Type type, bool needsUnload, string prefabFilePath, GenerationInfoBase info, bool isInPlaceSave)
        {
            // 原地保存:用户直接在 prefab asset 上编辑生成
            // 跳过生成 prefabFilePath(用户在 Project 窗口选中 / Prefab Mode 内编辑根,目标就是当前 prefab)
            // Step6 会用 target 当前状态直接 SaveAsPrefabAsset 写回 inPlaceAssetPath
            if (isInPlaceSave)
            {
                Debug.Log($"[UI Generator] 原地保存: target 自身是 prefab asset 根, 跳过生成 {prefabFilePath}, Step6 写回原资产");
                return target;
            }

            // 续跑场景:prefab 已存在,直接加载(此时会跳过 SaveAsPrefabAsset)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabFilePath) != null)
            {
                Debug.Log($"[UI Generator] Prefab {prefabFilePath} 已存在, 跳过 SaveAsPrefabAsset");
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabFilePath);
            }

            // 创建预制体: 来自 LoadPrefabContents 的子对象无法直接 SaveAsPrefabAsset
            // 需先克隆到场景物体, 脱离预制体编辑上下文后再保存
            GameObject saveTarget = target;
            if (needsUnload)
            {
                saveTarget = UnityEngine.Object.Instantiate(target);
                saveTarget.name = target.name;
                // 克隆的组件字段仍指向原对象的子节点, 需通过 Reset 重新绑定到克隆的子节点
                var clonedComp = saveTarget.GetComponent(type);
                if (clonedComp != null)
                    InvokeReset(clonedComp);
            }
            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(saveTarget, prefabFilePath);
            if (saveTarget != target)
                UnityEngine.Object.DestroyImmediate(saveTarget);
            return prefabAsset;
        }

        // ============== Step 6 子方法: ApplyPrefab (4 个分支) ==============

        // 成功返回 true 并通过 out applyBranch 标识走的分支
        // isRecreated: true 表示 target 是 RecreateTarget 重建的空占位,Step 6 走宽松分支(允许无 Canvas 的根节点占位)
        // isInPlaceSave: true 表示 target 自身是 prefab asset 根,prefabAsset==target 时 Step6 跳过 prefabFilePath 生成,直接 SaveAsPrefabAsset 写回 inPlaceAssetPath
        private static bool TryApplyPrefab(GameObject target, GameObject prefabAsset, Type type, GenerationInfoBase info, bool needsUnload, string sourcePrefabPath, bool isRecreated, bool isInPlaceSave, string inPlaceAssetPath, out ApplyBranch applyBranch)
        {
            applyBranch = ApplyBranch.None;

            // 分支 1: 预制体实例 (no sourcePrefabPath, IsPartOfPrefabInstance)
            if (sourcePrefabPath == null && PrefabUtility.IsPartOfPrefabInstance(target))
            {
                applyBranch = ApplyBranch.PrefabInstance;
                var prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
                if (!string.IsNullOrEmpty(prefabAssetPath))
                {
                    PrefabUtility.ApplyPrefabInstance(target, InteractionMode.UserAction);
                    Debug.LogError($"[UI Generator] target 是预制体实例, 已 Apply 修改到 {prefabAssetPath} 而非替换场景中的物体");
                    return true;
                }
                return false;
            }

            // 分支 2: 源预制体内替换 (sourcePrefabPath != null)
            if (sourcePrefabPath != null)
            {
                applyBranch = ApplyBranch.SourcePrefabReplace;
                var sourceRoot = target.transform.root.gameObject;
                var originalParent = target.transform.parent;
                var originalSiblingIndex = target.transform.GetSiblingIndex();

                // 在源预制体内替换: 销毁原子对象 → 实例化新预制体 → 恢复位置
                UnityEngine.Object.DestroyImmediate(target);
                var newChild = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                if (newChild == null) return false;
                newChild.transform.SetParent(originalParent, false);
                newChild.transform.SetSiblingIndex(originalSiblingIndex);

                // 持久化修改回源预制体资产并释放临时副本
                PrefabUtility.SaveAsPrefabAsset(sourceRoot, sourcePrefabPath);
                PrefabUtility.UnloadPrefabContents(sourceRoot);

                Selection.activeObject = prefabAsset;
                Debug.Log($"[UI Generator] 已在源预制体 {sourcePrefabPath} 中将 {info.name} 替换为预制体实例");
                return true;
            }

            // 分支 3: 预制体资产本身 (target 自身是 prefab asset)
            var assetPath = AssetDatabase.GetAssetPath(target.transform.root.gameObject);
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab"))
            {
                applyBranch = ApplyBranch.PrefabAsset;
                var prefabFilePath = $"{info.prefabFolder}/{info.name}.prefab";

                // 原地写回:target 自身是 prefab asset 根(Project 窗口选中 / Prefab Mode 内)
                // Step5 已跳过生成 prefabFilePath(prefabAsset == target),这里只 SaveAsPrefabAsset 写回原资产
                // 避免"删除 prefabFilePath 新文件 + 替换原资产" 的双重操作
                if (isInPlaceSave && prefabAsset == target)
                {
                    var writePath = inPlaceAssetPath ?? assetPath;
                    var savedAsset = PrefabUtility.SaveAsPrefabAsset(target.transform.root.gameObject, writePath);
                    if (savedAsset == null) return false;
                    Selection.activeObject = savedAsset;
                    Debug.Log($"[UI Generator] 原地保存: 已在预制体资产 {writePath} 上添加组件 {info.name} 并写回(未生成 {prefabFilePath})");
                    return true;
                }

                // SaveAsPrefabAsset(saveTarget, prefabFilePath) 已在上方保存了新预制体
                // 若原资产路径与生成路径不同, 需额外将组件修改保存回原资产
                if (!string.Equals(assetPath, prefabFilePath, StringComparison.OrdinalIgnoreCase))
                    PrefabUtility.SaveAsPrefabAsset(target.transform.root.gameObject, assetPath);

                Selection.activeObject = prefabAsset;
                Debug.Log($"[UI Generator] 已在预制体资产 {assetPath} 上添加组件 {info.name}");
                return true;
            }

            // 分支 4: 场景物体
            applyBranch = ApplyBranch.SceneObject;
            var sceneOriginalParent = target.transform.parent;
            var sceneOriginalPosition = target.transform.position;
            var sceneOriginalRotation = target.transform.rotation;
            var sceneOriginalScale = target.transform.localScale;
            var sceneOriginalSiblingIndex = target.transform.GetSiblingIndex();

            // 占位重建场景:parent==null 且无 Canvas 也允许通过,直接在场景根实例化 prefab
            // 正常场景下 parent==null + 无 Canvas 仍判失败,避免在用户没准备的根节点乱放
            if (sceneOriginalParent == null && !target.HasComponent<Canvas>() && !isRecreated)
                return false;

            // 在原位置实例化预制体
            var newInstance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            if (newInstance == null) return false;
            // 恢复Transform信息
            newInstance.transform.SetParent(sceneOriginalParent);
            newInstance.transform.position = sceneOriginalPosition;
            newInstance.transform.rotation = sceneOriginalRotation;
            newInstance.transform.localScale = sceneOriginalScale;
            newInstance.transform.SetSiblingIndex(sceneOriginalSiblingIndex);
            // 选中新实例化的物体
            Selection.activeGameObject = newInstance;

            // 删除场景中的原物体 (target 在此处被销毁, 续跑场景下会失败,需在续跑时跳过该分支)
            UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("[UI Generator] 已将场景中的原物体替换为预制体实例");
            return true;
        }

        // ============== Step 安全工具 ==============

        // 步骤失败时安全释放临时副本(target 来自 LoadPrefabContents 时)
        // 不抛异常,失败也吞掉
        private static void SafeUnload(GameObject target, bool needsUnload)
        {
            if (!needsUnload || target == null) return;
            try { PrefabUtility.UnloadPrefabContents(target.transform.root.gameObject); }
            catch { /* 静默 */ }
        }

        // 通过反射同步调用私有的 Reset 方法, 避免 Invoke 延迟到下一帧导致 SaveAsPrefabAsset 序列化空字段
        private static void InvokeReset(Component component)
        {
            var method = component.GetType().GetMethod("Reset", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            method?.Invoke(component, null);
        }

        // 目标已丢失时,自动重建一个空的占位物体,让生成流程能从头继续
        // - 场景对象(targetAssetPath 为空):在活动场景里建一个空 GameObject
        // - 预制体资产:在原路径建一个空 prefab
        // 返回值:
        //   - 场景对象:返回占位 GameObject,直接当 target 用(无需 Unload)
        //   - 预制体:返回占位 prefab asset(后续需 LoadPrefabContents 拿临时副本,标记 needsUnload)
        // 返回 null 表示无法重建(无活动场景 / 路径非法 / 写入失败 / 名称为空)
        // addRectTransform: UI Panel 占位需要 RectTransform 才能挂 PanelBase;Item 不加
        private static GameObject RecreateTarget(GenerationInfoBase info, bool addRectTransform)
        {
            // info.name 是 UI 类名权威(GeneratePanel/Item 时设入),targetName 是 GameObject 旧名,改名后会过期
            var name = !string.IsNullOrEmpty(info.name) ? info.name : info.targetName;
            if (string.IsNullOrEmpty(name))
                return null;

            if (string.IsNullOrEmpty(info.targetAssetPath))
            {
                // 场景对象
                var activeScene = SceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                {
                    Debug.LogWarning("[UI Generator] 没有打开的活动场景, 无法为场景对象建占位");
                    return null;
                }
                var go = addRectTransform
                    ? new GameObject(name, typeof(RectTransform))
                    : new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, activeScene);
                return go;
            }

            // 预制体资产:在原路径建一个空 prefab
            var dir = Path.GetDirectoryName(info.targetAssetPath);
            if (!string.IsNullOrEmpty(dir))
            {
                try { IOHelper.InsureExist(dir, false); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UI Generator] 创建目录 {dir} 失败: {e.Message}");
                    return null;
                }
            }
            var tempGo = addRectTransform
                ? new GameObject(name, typeof(RectTransform))
                : new GameObject(name);
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(tempGo, info.targetAssetPath);
                return prefab;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UI Generator] 在 {info.targetAssetPath} 创建占位 prefab 失败: {e.Message}");
                return null;
            }
            finally
            {
                if (tempGo != null) UnityEngine.Object.DestroyImmediate(tempGo);
            }
        }

        // FindTarget 失败时尝试 RecreateTarget 自动建占位, 让生成流程能从头继续
        // 成功: ctx.target/sourcePrefabPath/needsUnload/isRecreated 都接好, Info 已持久化
        // 失败: 返回 false, Info + Checkpoint 已被 DiscardCheckpointAndInfo 清掉, 调用方应直接 return
        // isResume: 续跑场景下, 调用方在外部把 in-memory checkpoint 同步成 None 让后续 step 全部重跑
        // isUIPanel: 传给 RecreateTarget 决定是否补 RectTransform(Panel 是 UI, Item 不是)
        private static bool TryRecoverOrRecreate<TInfo>(
            TInfo info, GenerationContext ctx, string key,
            bool isResume, bool isUIPanel) where TInfo : GenerationInfoBase
        {
            var err = isResume
                ? $"续跑时无法找到目标对象 (name={info.name}), 目标可能已被移动/删除"
                : $"无法通过任何方式找到目标对象 (name={info.name}, asset={info.targetAssetPath})";

            var placeHolder = RecreateTarget(info, isUIPanel);
            if (placeHolder == null)
            {
                Debug.LogWarning($"[UI Generator] {err}。无法创建占位, 断点已自动清理。");
                DiscardCheckpointAndInfo<TInfo>(key, err);
                return false;
            }

            // 重建成功: 场景对象直接用, 预制体加载临时副本
            if (string.IsNullOrEmpty(info.targetAssetPath))
            {
                ctx.target = placeHolder;
                ctx.sourcePrefabPath = null;
                ctx.needsUnload = false;
            }
            else
            {
                var contentsRoot = PrefabUtility.LoadPrefabContents(info.targetAssetPath);
                ctx.target = contentsRoot;
                ctx.sourcePrefabPath = info.targetAssetPath;
                ctx.needsUnload = true;
            }
            ctx.isRecreated = true;

            // 持久化新 target, 下次域重载能找到它
            info.targetGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(placeHolder).ToString();
            info.targetInstanceId = placeHolder.GetInstanceID();
            info.targetName = placeHolder.name;
            PrefsHelper.Set<TInfo>(key, info);

            Debug.LogWarning(
                $"[UI Generator] {err}。已自动创建空占位 {placeHolder.name}, " +
                (isResume ? "流程将从 Step 1 重新执行" : "请补回标记的 UI 组件后再次执行生成") + "。");
            return true;
        }

        // 根据输出位置所在程序集决定生成脚本的命名空间:
        // - 若目录向上能找到 .asmdef, 使用 "{rootNamespace}.UI" (rootNamespace 缺失时回退到 name)
        // - 若一路上没有 .asmdef, 使用 "UI"
        // 注意: 跨程序集的 partial class 不合法, 调用方应保证 .Generate.cs 与 .cs 输出到同一程序集
        private static string GetNamespaceForPath(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return "UI";

            var dir = Path.GetFullPath(folder);
            while (!string.IsNullOrEmpty(dir))
            {
                string[] asmdefFiles;
                try { asmdefFiles = Directory.GetFiles(dir, "*.asmdef", SearchOption.TopDirectoryOnly); }
                catch { asmdefFiles = Array.Empty<string>(); }

                if (asmdefFiles.Length > 0)
                {
                    try
                    {
                        var json = File.ReadAllText(asmdefFiles[0]);
                        var data = JsonUtility.FromJson<AsmdefRoot>(json);
                        var ns = !string.IsNullOrEmpty(data?.rootNamespace) ? data.rootNamespace : data?.name;
                        if (!string.IsNullOrEmpty(ns))
                            return $"{ns}.UI";
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[UI Generator] 解析 {asmdefFiles[0]} 失败: {e.Message}");
                    }
                    return "UI";
                }

                var parent = Directory.GetParent(dir);
                dir = parent?.FullName;
            }
            return "UI";
        }

        [Serializable]
        private class AsmdefRoot
        {
            public string name;
            public string rootNamespace;
        }

        private static void GenerateScripts(string path, List<(string type, string objectName, string fieldName)> components, string scriptName, string baseType)
        {
            using var sb = ZString.CreateStringBuilder();
            // 默认与 .Generate.cs 同目录; 若用户已移动 .cs, 跟随到 .cs 当前位置
            var dir = path;
            // 检测逻辑脚本是否已被移动
            var existingLogicPath = FindAssetPathByName($"{scriptName}.cs");
            if (existingLogicPath != null)
            {
                dir = Path.GetDirectoryName(existingLogicPath);
                Debug.Log($"[UI Generator] 检测到 {scriptName}.cs 已在 {existingLogicPath}, 将覆盖更新");
            }
            var logicPath = Path.Combine(dir, $"{scriptName}.cs");
            // 根据各自输出位置所在程序集决定命名空间; 缺省程序集时回退到 "UI"
            var generateNs = GetNamespaceForPath(path);
            var logicNs = GetNamespaceForPath(dir);
            // -------------------------- 固定不变的代码 --------------------------
            // 组件事件监听注册
            var callbackBuilder = ZString.CreateStringBuilder();
            callbackBuilder.AppendLine("        #region - Callback -");
            var registerBuilder = ZString.CreateStringBuilder();
            registerBuilder.AppendLine("        public override void RegisterEvents()");
            registerBuilder.AppendLine("        {");
            registerBuilder.AppendLine("            RegisterCustomEvents();");
            registerBuilder.AppendLine("            // 组件固定监听, 不在Deregister中移除, 避免组件重复注册监听");
            registerBuilder.AppendLine("            if(reigsteredControlsListener)");
            registerBuilder.AppendLine("                return;");
            GenerateButtonsCallback(components, ref callbackBuilder, ref registerBuilder);
            GenerateToggleCallback(components, ref callbackBuilder, ref registerBuilder);
            GenerateSliderCallback(components, ref callbackBuilder, ref registerBuilder);
            GenerateInputFieldCallback(components, ref callbackBuilder, ref registerBuilder);
            registerBuilder.AppendLine("            reigsteredControlsListener = true;");
            registerBuilder.AppendLine("        }");
            registerBuilder.AppendLine("        public override void DeregisterEvents()");
            registerBuilder.AppendLine("        {");
            registerBuilder.AppendLine("            DeregisterCustomEvents();");
            registerBuilder.AppendLine("        }");
            callbackBuilder.AppendLine("        #endregion");

            sb.AppendLine($"//CreateTime: {DateTime.Now}");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using Lin.Runtime.UI;");
            sb.AppendLine("using UnityEngine.UI;");
            sb.AppendLine("using Lin.Runtime.Helper;");
            sb.AppendLine("using Sirenix.OdinInspector;");
            sb.AppendLine("using TMPro;");
            sb.AppendLine();
            sb.AppendLine($"namespace {generateNs}");
            sb.AppendLine("{");
            sb.AppendLine($"    //Description: 不要在这个脚本里进行逻辑编写, 在 {logicPath} 中编写具体逻辑");
            sb.AppendLine($"    public partial class {scriptName}");
            sb.AppendLine("    {");

            // 添加组件字段和属性
            foreach (var (type, _, name) in components)
            {
                var fieldName = char.ToLowerInvariant(name[0]) + name.Substring(1) + type;
                var propertyName = char.ToUpperInvariant(name[0]) + name.Substring(1) + type;
                sb.AppendLine($"        [SerializeField, BoxGroup(CONTROLS_NAME)] private {type} {fieldName};");
                sb.AppendLine($"        public {type} {propertyName} => {fieldName};");
            }
            sb.AppendLine($"        private bool reigsteredControlsListener;");

            // 添加Reset
            sb.AppendLine();
            sb.AppendLine("        [Button, BoxGroup(CONTROLS_NAME)]");
            sb.AppendLine("        private void QuickSet()");
            sb.AppendLine("        {");
            foreach (var (type, _, name) in components)
            {
                var fieldName = char.ToLowerInvariant(name[0]) + name.Substring(1) + type;
                sb.AppendLine($"            {fieldName} = gameObject.GetComponentInChildren<{type}>(\"[{type}] {name.FirstCharacterUpper()}\");");
            }
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private void Reset() => QuickSet();");
            sb.AppendLine();
            // 组件事件监听
            sb.AppendLine(registerBuilder);
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(Path.Combine(path, $"{scriptName}.Generate.cs"), sb.ToString());

            // -------------------------- 可编辑的代码 --------------------------
            if (!File.Exists(logicPath))
            {
                sb.Clear();
                IOHelper.InsureExist(dir, false, false);
                sb.AppendLine($"//CreateTime: {DateTime.Now}");
                sb.AppendLine("using Lin.Runtime.UI;");
                sb.AppendLine("using Lin.Runtime.Helper;");
                sb.AppendLine("using System;");
                sb.AppendLine();
                sb.AppendLine($"namespace {logicNs}");
                sb.AppendLine("{");
                sb.AppendLine($"    public partial class {scriptName} : {baseType}");
                sb.AppendLine("    {");
                sb.AppendLine("        private void RegisterCustomEvents()");
                sb.AppendLine("        {");
                sb.AppendLine("            // 自行补充需要注册的事件监听");
                sb.AppendLine("        }");
                sb.AppendLine("        private void DeregisterCustomEvents()");
                sb.AppendLine("        {");
                sb.AppendLine("            // 添加监听后记得添加注销监听");
                sb.AppendLine("        }");
                sb.AppendLine(callbackBuilder);
                sb.AppendLine("    }");
                sb.AppendLine("}");

                File.WriteAllText(Path.Combine(dir, $"{scriptName}.cs"), sb.ToString());
            }

            registerBuilder.Dispose();
            callbackBuilder.Dispose();
        }

        private static void GenerateButtonsCallback(List<(string type, string objectName, string fieldName)> components, ref Utf16ValueStringBuilder callbackBuilder, ref Utf16ValueStringBuilder registerBuilder)
        {
            var enumerables = components.AsValueEnumerable().Where(c => c.type.Equals("Button"));
            if (!enumerables.Any())
                return;

            registerBuilder.AppendLine();
            registerBuilder.AppendLine("            // Buttons OnClick");

            foreach (var component in components.AsValueEnumerable().Where(c => c.type.Equals("Button")))
            {
                var callbackName = $"On{component.fieldName.FirstCharacterUpper()}ButtonClick";

                registerBuilder.AppendLine($"            {component.fieldName.FirstCharacterLower()}{component.type}.AddOnClickListener({callbackName});");

                callbackBuilder.AppendLine($"        private void {callbackName}()");
                callbackBuilder.AppendLine("        {");
                callbackBuilder.AppendLine("            throw new NotImplementedException();");
                callbackBuilder.AppendLine("        }");
                callbackBuilder.AppendLine();
            }
        }

        private static void GenerateToggleCallback(List<(string type, string objectName, string fieldName)> components, ref Utf16ValueStringBuilder callbackBuilder, ref Utf16ValueStringBuilder registerBuilder)
        {
            var enumerables = components.AsValueEnumerable().Where(c => c.type.Equals("Toggle"));
            if (!enumerables.Any())
                return;

            registerBuilder.AppendLine();
            registerBuilder.AppendLine("            // Toggles OnValueChanged");

            foreach (var component in enumerables)
            {
                var callbackName = $"On{component.fieldName.FirstCharacterUpper()}ToggleValueChanged";
                registerBuilder.AppendLine($"            {component.fieldName.FirstCharacterLower()}{component.type}.AddOnValueChangedListener({callbackName});");
                callbackBuilder.AppendLine($"        private void {callbackName}(bool isOn)");
                callbackBuilder.AppendLine("        {");
                callbackBuilder.AppendLine("            throw new NotImplementedException();");
                callbackBuilder.AppendLine("        }");
                callbackBuilder.AppendLine();
            }
        }

        private static void GenerateSliderCallback(List<(string type, string objectName, string fieldName)> components, ref Utf16ValueStringBuilder callbackBuilder, ref Utf16ValueStringBuilder registerBuilder)
        {
            var enumerables = components.AsValueEnumerable().Where(c => c.type.Equals("Slider")); if (!enumerables.Any())
                return;

            registerBuilder.AppendLine();
            registerBuilder.AppendLine("            // Sliders OnValueChanged");

            foreach (var component in components.AsValueEnumerable().Where(c => c.type.Equals("Slider")))
            {
                var callbackName = $"On{component.fieldName.FirstCharacterUpper()}SliderValueChanged";
                registerBuilder.AppendLine($"            {component.fieldName.FirstCharacterLower()}{component.type}.AddOnValueChangedListener({callbackName});");
                callbackBuilder.AppendLine($"        private void {callbackName}(float value)");
                callbackBuilder.AppendLine("        {");
                callbackBuilder.AppendLine("            throw new NotImplementedException();");
                callbackBuilder.AppendLine("        }");
                callbackBuilder.AppendLine();
            }
        }

        private static void GenerateInputFieldCallback(List<(string type, string objectName, string fieldName)> components, ref Utf16ValueStringBuilder callbackBuilder, ref Utf16ValueStringBuilder registerBuilder)
        {
            // InputField 与 TMP_InputField 的通用回调绑定
            var enumerables = components.AsValueEnumerable().Where(c => c.type.Equals("InputField") || c.type.Equals("TMP_InputField"));
            if (!enumerables.Any())
                return;

            registerBuilder.AppendLine();
            registerBuilder.AppendLine("            // InputFields OnValueChanged & OnEndEdit");

            foreach (var component in enumerables)
            {
                var fieldPrefix = component.fieldName.FirstCharacterUpper();
                var callbackValueChanged = ZString.Concat("On", fieldPrefix, "InputValueChanged");
                var callbackEndEdit = ZString.Concat("On", fieldPrefix, "InputEndEdit");

                // 统一注册两种输入框的值改变与结束编辑回调
                registerBuilder.AppendLine(ZString.Concat("            ", component.fieldName.ToLower(), component.type, ".AddOnValueChangedListener(", callbackValueChanged, ");"));
                registerBuilder.AppendLine(ZString.Concat("            ", component.fieldName.ToLower(), component.type, ".AddOnEndEditListener(", callbackEndEdit, ");"));

                // 生成对应的回调函数（string 参数统一处理）
                callbackBuilder.AppendLine(ZString.Concat("        private void ", callbackValueChanged, "(string text)"));
                callbackBuilder.AppendLine("        {");
                callbackBuilder.AppendLine("            throw new NotImplementedException();");
                callbackBuilder.AppendLine("        }");
                callbackBuilder.AppendLine();

                callbackBuilder.AppendLine(ZString.Concat("        private void ", callbackEndEdit, "(string text)"));
                callbackBuilder.AppendLine("        {");
                callbackBuilder.AppendLine("            throw new NotImplementedException();");
                callbackBuilder.AppendLine("        }");
                callbackBuilder.AppendLine();
            }
        }

        private static List<(string type, string objectName, string fieldName)> CollectMarkedComponents(GameObject root)
        {
            var components = new List<(string type, string objectName, string fieldName)>();
            var transforms = root.GetComponentsInChildren<Transform>(true);

            foreach (var transform in transforms)
            {
                var name = transform.name;
                if (!name.StartsWith("[")) continue;

                var endIndex = name.IndexOf(']');
                if (endIndex == -1) continue;

                var type = name.Substring(1, endIndex - 1);
                var componentName = name.Substring(endIndex + 2); // Skip "] "

                // 处理组件名称，移除特殊字符
                var fieldName = new string(componentName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
                if (string.IsNullOrEmpty(fieldName) || char.IsDigit(fieldName[0]))
                    fieldName = "_" + fieldName;

                components.Add((type, componentName, fieldName));
            }

            return components;
        }

        #endregion

        #region - Panel -

        public static void GeneratePanelScripts(GameObject target)
        {
            // 用户主动重新触发生成: 旧的 Checkpoint 必然携带过时的 prefabPath / sourcePrefabPath,
            // 必须在入口处清掉, 避免 [DidReloadScripts] 续跑时仍按旧路径写到已被移动的旧位置
            DeleteCheckpoint<UIGenerationInfo>();

            var folders = InsureFoldersExist();

            var panelName = target.name;
            if (!panelName.EndsWith("Panel"))
                panelName += "Panel";

            // 检测已有文件是否被移动, 跟随到当前位置
            var existingScriptPath = FindAssetPathByName($"{panelName}.Generate.cs");
            if (existingScriptPath != null)
            {
                folders.scriptsFolder = Path.GetDirectoryName(existingScriptPath);
                Debug.Log($"[UI Generator] 检测到 {panelName}.Generate.cs 已在 {existingScriptPath}, 将覆盖更新");
            }
            var existingPrefabPath = FindAssetPathByName($"{panelName}.prefab");
            if (existingPrefabPath != null)
            {
                folders.prefabsFolder = Path.GetDirectoryName(existingPrefabPath);
                Debug.Log($"[UI Generator] 检测到 {panelName}.prefab 已在 {existingPrefabPath}, 将覆盖更新");
            }

            var components = CollectMarkedComponents(target);

            // 生成Panel脚本
            GeneratePanel(folders.scriptsFolder, panelName, components);

            // 刷新资源以确保新生成的脚本被编译
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            // 保存生成UI所需的信息
            var info = new UIGenerationInfo
            {
                name = panelName,
                prefabFolder = folders.prefabsFolder,
                scriptsFolder = folders.scriptsFolder,
                components = components
            };
            StoreTargetInfo(target, info);
            PrefsHelper.Set(UIGenerationInfo.KEY, info);

            AssetDatabase.Refresh();
        }

        [Serializable]
        private class UIGenerationInfo : GenerationInfoBase
        {
            public const string KEY = nameof(UIGenerationInfo);
        }

        [DidReloadScripts]
        private static void CompleteUIGeneration() => CompleteGeneration<UIGenerationInfo>(UIGenerationInfo.KEY, isUIPanel: true);

        private static void GeneratePanel(string path, string panelName, List<(string type, string objectName, string fieldName)> components)
            => GenerateScripts(path, components, panelName, nameof(PanelBase));

        #endregion

        #region - Item -

        [Serializable]
        private class ItemGenerationInfo : GenerationInfoBase
        {
            public const string KEY = nameof(ItemGenerationInfo);
        }

        public static void GenerateItemScripts(GameObject selectedObject)
        {
            // 用户主动重新触发生成: 旧的 Checkpoint 必然携带过时的 prefabPath / sourcePrefabPath,
            // 必须在入口处清掉, 避免 [DidReloadScripts] 续跑时仍按旧路径写到已被移动的旧位置
            DeleteCheckpoint<ItemGenerationInfo>();

            var folders = InsureFoldersExist();

            // 检测已有文件是否被移动, 跟随到当前位置
            var itemName = selectedObject.name;
            var existingScriptPath = FindAssetPathByName($"{itemName}.Generate.cs");
            if (existingScriptPath != null)
            {
                folders.scriptsFolder = Path.GetDirectoryName(existingScriptPath);
                Debug.Log($"[UI Generator] 检测到 {itemName}.Generate.cs 已在 {existingScriptPath}, 将覆盖更新");
            }
            var existingPrefabPath = FindAssetPathByName($"{itemName}.prefab");
            if (existingPrefabPath != null)
            {
                folders.prefabsFolder = Path.GetDirectoryName(existingPrefabPath);
                Debug.Log($"[UI Generator] 检测到 {itemName}.prefab 已在 {existingPrefabPath}, 将覆盖更新");
            }

            var components = CollectMarkedComponents(selectedObject);

            // 生成Panel脚本
            GenerateItem(folders.scriptsFolder, components, selectedObject.name);

            // 刷新资源以确保新生成的脚本被编译
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            // 保存生成UI所需的信息
            var info = new ItemGenerationInfo
            {
                prefabFolder = folders.prefabsFolder,
                scriptsFolder = folders.scriptsFolder,
                components = components,
                name = selectedObject.name,
            };
            StoreTargetInfo(selectedObject, info);
            PrefsHelper.Set(ItemGenerationInfo.KEY, info);

            AssetDatabase.Refresh();
        }

        private static void GenerateItem(string path, List<(string type, string objectName, string fieldName)> components, string scriptName)
            => GenerateScripts(path, components, scriptName, nameof(UIBehaviour));

        [DidReloadScripts]
        private static void CompleteItemGeneration() => CompleteGeneration<ItemGenerationInfo>(ItemGenerationInfo.KEY, isUIPanel: false);

        #endregion

        #region - Checkpoint & Resume -

        /// <summary>
        /// UI 生成断点续传:阶段二(CompleteGeneration)拆成 7 步,每步独立捕获异常并记录进度。
        /// 失败后通过 PrefsHelper 序列化到 chk_{InfoType} 键,下次 [DidReloadScripts] 或手动菜单可从断点续跑。
        /// </summary>
        public enum GenerationStep
        {
            /// <summary> 阶段一完成,阶段二未启动 </summary>
            None = 0,
            /// <summary> Step1: 4 级回退已找到 target 引用 </summary>
            TargetRecovered,
            /// <summary> Step2: 反射找到生成的 Type </summary>
            TypeResolved,
            /// <summary> Step3: AddComponent 成功 </summary>
            ComponentAttached,
            /// <summary> Step4: Reset 绑定子节点成功 </summary>
            ComponentReset,
            /// <summary> Step5: SaveAsPrefabAsset 写盘成功 </summary>
            PrefabSaved,
            /// <summary> Step6: Apply 到 source/替换场景物体成功 </summary>
            PrefabApplied,
            /// <summary> Step7: 释放临时副本,选中新预制体,完成 </summary>
            Completed
        }

        /// <summary>
        /// 断点持久化记录,与 UIGenerationInfo / ItemGenerationInfo 1:1 关联(通过 prefabName + prefabPath)。
        /// 写入策略:阶段二每步开始前 MarkStepStart,成功后 MarkStepComplete,失败时 MarkStepFailed 写 lastError。
        /// 清除时机:阶段二全部 Completed 后,或用户主动 Discard。
        /// </summary>
        [Serializable]
        public class GenerationCheckpoint
        {
            /// <summary> 关联的 Info 类型 key(UIGenerationInfo / ItemGenerationInfo),用于续跑时反查 Info </summary>
            public string infoKey;

            /// <summary> UI 名称,日志/菜单显示用 </summary>
            public string prefabName;

            /// <summary> 新预制体写入路径,Step5 幂等检查使用 </summary>
            public string prefabPath;

            /// <summary> Step6 用:源预制体资产路径(LoadPrefabContents 触发的回退),非空时 Step6 走"源预制体内替换"分支 </summary>
            public string sourcePrefabPath;

            /// <summary> Step6 用:本次 Apply 走的是哪个分支,续跑时按分支做幂等保护 </summary>
            public ApplyBranch applyBranch;

            /// <summary> 上次成功的最后一步(初始为 None,Completed 表示流程结束) </summary>
            public GenerationStep completedStep;

            /// <summary> Step1 持久化:target 是 prefab asset 根时为 true,续跑时由 Step5/6 还原原地写回行为 </summary>
            public bool isInPlaceSave;

            /// <summary> 上次失败时的异常/原因,日志展示用 </summary>
            public string lastError;

            /// <summary> 最近一次更新的 ISO8601 时间戳,排查用 </summary>
            public string updatedAt;
        }

        /// <summary>
        /// Step6 走的是哪个 Apply 分支,类型安全比 string 好,且续跑时按此选择幂等策略。
        /// </summary>
        public enum ApplyBranch
        {
            /// <summary> 未指定或流程未走到 Step6 </summary>
            None = 0,
            /// <summary> 预制体实例(无 sourcePrefabPath,IsPartOfPrefabInstance)→ ApplyPrefabInstance </summary>
            PrefabInstance,
            /// <summary> 源预制体内替换(sourcePrefabPath != null)→ DestroyImmediate + InstantiatePrefab </summary>
            SourcePrefabReplace,
            /// <summary> 预制体资产本身(target 自身是 prefab asset)→ SaveAsPrefabAsset </summary>
            PrefabAsset,
            /// <summary> 场景物体 → InstantiatePrefab + DestroyImmediate </summary>
            SceneObject
        }

        /// <summary>
        /// 阶段二 7 步执行期间的共享上下文,封装跨步骤传递的可变状态。
        /// 抽到类的目的:消除 CompleteGeneration 主体 6 个共享变量在 try/catch 间分散声明的视觉噪声,
        /// catch 中 SafeUnload(ctx.target, ctx.needsUnload) 签名也更短。
        /// </summary>
        private class GenerationContext
        {
            /// <summary> Step1 FindTarget 输出,Step3-5 操作的 GameObject;Step6 后可能被销毁 </summary>
            public GameObject target;

            /// <summary> Step2 ResolveType 输出,Step3-6 反射操作用的 Type </summary>
            public Type type;

            /// <summary> Step5 SavePrefab 输出,Step6 Apply + Step7 选中用 </summary>
            public GameObject prefabAsset;

            /// <summary> Step1 推断:是否需要 Step7 UnloadPrefabContents(LoadPrefabContents 触发的临时副本) </summary>
            public bool needsUnload;

            /// <summary> Step1 输出:源预制体资产路径,Step6 走 SourcePrefabReplace 分支的判断依据 </summary>
            public string sourcePrefabPath;

            /// <summary> Step6 输出:本次 Apply 走的分支,Checkpoint 持久化以做幂等保护 </summary>
            public ApplyBranch applyBranch;

            /// <summary> Step1 输出:true 表示 target 是 RecreateTarget 重建的空占位,Step6 走"宽松"分支(允许无 Canvas 的根节点占位) </summary>
            public bool isRecreated;

            /// <summary> Step1 输出:true 表示 target 自身就是 prefab asset 根(Project 窗口选中 / Prefab Mode 内),Step5 跳过生成 prefabFilePath,Step6 直接写回原资产 </summary>
            public bool isInPlaceSave;

            /// <summary> Step1 输出:isInPlaceSave 为 true 时,原 prefab asset 路径,供 Step6 SaveAsPrefabAsset 写回 </summary>
            public string inPlaceAssetPath;
        }

        // Checkpoint 用的 PrefsHelper key 前缀,与 Info key 分文件存储便于独立清理
        private const string CHECKPOINT_KEY_PREFIX = "chk_";

        // ============== Step 子方法: 6 个 ==============

        // 生成 chk_{TypeName} 形式的 key,与 PrefsArchive<T> 文件名严格对应
        private static string CheckpointKey<TInfo>() => CHECKPOINT_KEY_PREFIX + typeof(TInfo).Name;

        private static void SaveCheckpoint<TInfo>(GenerationCheckpoint checkpoint)
        {
            if (checkpoint == null) return;
            checkpoint.updatedAt = DateTime.UtcNow.ToString("o");
            PrefsHelper.Set<GenerationCheckpoint>(CheckpointKey<TInfo>(), checkpoint);
        }

        private static GenerationCheckpoint LoadCheckpoint<TInfo>()
        {
            try { return PrefsHelper.Get<GenerationCheckpoint>(CheckpointKey<TInfo>()); }
            catch { return null; }
        }

        private static void DeleteCheckpoint<TInfo>()
        {
            try { PrefsHelper.DeleteKey<GenerationCheckpoint>(CheckpointKey<TInfo>()); }
            catch { /* 静默,清理动作不应阻塞主流程 */ }
        }

        // 目标已"明确丢失"(预制体被删 / 场景对象不再存在)时,清理 Info + Checkpoint 并提示用户重新触发
        // 之前只 MarkStepFailed 会让断点永远残留,每次域重载都刷同样的错误
        private static void DiscardCheckpointAndInfo<TInfo>(string key, string reason)
        {
            Debug.LogWarning($"[UI Generator] {reason}。断点与 Info 已自动清理, 请重新选中物体后再次执行生成。");
            PrefsHelper.DeleteKey<TInfo>(key);
            DeleteCheckpoint<TInfo>();
        }

        /// <summary>
        /// Step 成功时:提升 completedStep,清空 lastError(只保留最近一次失败信息)。
        /// isInPlaceSave 由调用方传入(ctx.isInPlaceSave),用于续跑时识别"原地写回原资产"路径。
        /// </summary>
        private static void MarkStepComplete<TInfo>(GenerationCheckpoint checkpoint, GenerationStep step, string prefabName, string prefabPath, string sourcePrefabPath, ApplyBranch applyBranch, bool isInPlaceSave)
        {
            checkpoint.completedStep = step;
            checkpoint.prefabName = prefabName;
            checkpoint.prefabPath = prefabPath;
            checkpoint.sourcePrefabPath = sourcePrefabPath;
            checkpoint.applyBranch = applyBranch;
            checkpoint.isInPlaceSave = isInPlaceSave;
            checkpoint.lastError = null;
            SaveCheckpoint<TInfo>(checkpoint);
        }

        /// <summary>
        /// Step 失败时:保持 completedStep(上次成功点),记录 lastError 与时间戳,让 [DidReloadScripts] 钩子 / 状态条能感知。
        /// </summary>
        private static void MarkStepFailed<TInfo>(GenerationCheckpoint checkpoint, string error)
        {
            checkpoint.lastError = error;
            SaveCheckpoint<TInfo>(checkpoint);
        }

        /// <summary>
        /// 解析 lastError:兼容 Exception 与普通字符串,避免 .Message 触发空引用。
        /// </summary>
        private static string FormatError(Exception ex) => ex == null ? "未知错误" : $"{ex.GetType().Name}: {ex.Message}";

        #endregion

        #region - Resume / Discard Menus & Status Hint -

        // 启动/重编译后扫描所有 Info 类型对应的 Checkpoint,只要有残留就提示用户
        // 静态构造时机:InitializeOnLoad 在 Editor 启动 / 域重载后触发
        [InitializeOnLoad]
        private static class GenerationStatusHint
        {
            static GenerationStatusHint()
            {
                // 延迟到 Editor 完全 ready 再扫描,避免域重载未完成时 PrefsHelper 抛异常
                EditorApplication.delayCall += ScanAndNotify;
            }

            private static void ScanAndNotify()
            {
                var pending = FindPendingCheckpoints();
                if (pending.Count == 0) return;

                var names = string.Join(", ", pending.ConvertAll(p => p.prefabName));
                Debug.LogError($"[UI Generator] 检测到 {pending.Count} 个未完成生成任务: {names}。" +
                    "可在 Lin/UI/Resume Last 续跑,或 Discard All Checkpoints 放弃。");

                // 通过 SceneView.ShowNotification 浮层提示,不阻塞域重载,不打断用户思路
                // 3 秒自动消失,用户后续仍可点菜单
                try
                {
                    if (SceneView.sceneViews.Count > 0)
                    {
                        var sv = SceneView.sceneViews[0] as SceneView;
                        sv?.ShowNotification(
                            new GUIContent($"[UI Generator] 有 {pending.Count} 个未完成生成\n点击 Lin/UI 菜单续跑"),
                            3f);
                    }
                }
                catch { /* Notification 失败不影响主流程 */ }
            }
        }

        /// <summary>
        /// 扫描所有已知的 Info 类型对应的 Checkpoint 残留,用于状态条/Resume 菜单 enable 判定。
        /// </summary>
        private static List<GenerationCheckpoint> FindPendingCheckpoints()
        {
            var result = new List<GenerationCheckpoint>();
            var c1 = LoadCheckpoint<UIGenerationInfo>();
            var c2 = LoadCheckpoint<ItemGenerationInfo>();
            if (c1 != null && c1.completedStep != GenerationStep.Completed) result.Add(c1);
            if (c2 != null && c2.completedStep != GenerationStep.Completed) result.Add(c2);
            return result;
        }

        // 菜单 1:续跑所有 Checkpoint(优先 Panel,再 Item)
        // enable 仅在有 Checkpoint 时返回 true,菜单项自动变灰
        [MenuItem("Lin/UI/Resume Last Generation", true)]
        private static bool ResumeLastValidate() => FindPendingCheckpoints().Count > 0;

        [MenuItem("Lin/UI/Resume Last Generation", priority = 100)]
        private static void ResumeLast()
        {
            var pending = FindPendingCheckpoints();
            if (pending.Count == 0)
            {
                Debug.Log("[UI Generator] 没有需要续跑的生成任务");
                return;
            }

            foreach (var cp in pending)
            {
                Debug.Log($"[UI Generator] 续跑 {cp.prefabName} (上次中断在 {cp.completedStep}, 错误: {cp.lastError ?? "无"})");

                // 按 Info 类型分派到对应入口
                if (cp.infoKey == UIGenerationInfo.KEY)
                    CompleteUIGeneration();
                else if (cp.infoKey == ItemGenerationInfo.KEY)
                    CompleteItemGeneration();
            }
        }

        // 菜单 2:清空所有 Checkpoint(强制从头开始)
        // 用户在以下场景使用:改了 prefab 路径、组件列表大改、后悔了
        [MenuItem("Lin/UI/Discard All Checkpoints", true)]
        private static bool DiscardCheckpointsValidate() => FindPendingCheckpoints().Count > 0;

        [MenuItem("Lin/UI/Discard All Checkpoints", priority = 101)]
        private static void DiscardAllCheckpoints()
        {
            // 必须同时清 Info: [DidReloadScripts] 钩子看到 Info 残留仍会跑完整流程,
            // 单清 Checkpoint 只会让流程回到 Step 1 然后报"找不到目标对象"再来一次
            DeleteCheckpoint<UIGenerationInfo>();
            PrefsHelper.DeleteKey<UIGenerationInfo>(UIGenerationInfo.KEY);
            DeleteCheckpoint<ItemGenerationInfo>();
            PrefsHelper.DeleteKey<ItemGenerationInfo>(ItemGenerationInfo.KEY);
            Debug.Log("[UI Generator] 已清空所有 Checkpoint 与 Info 残留");
        }

        #endregion
    }
}
