/*
┌────────────────────────────┐
│　Description：Runtime配置
│　Remark：
└────────────────────────────┘
┌──────────────┐
│　ClassName：GlobalConfig_SO
└──────────────┘
*/
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Serialization;
using UnityEngine;
using Lin.Runtime.Notice;
using Lin.Runtime.Helper;
using Lin.Runtime.Attribute;
using Lin.Runtime.DesignPattern.Singleton;
using Cysharp.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif

[AssetPath("GlobeConfig", AssetPathAttribute.ELoaderType.Resources)]
public class GlobalConfig_SO : ScriptableObjectSingleton<GlobalConfig_SO>
{
    #region - HybridCLR -

#if HybridCLR
    public string dllFolder => ZString.Concat(prefabDirectory, "/Assembly");
#endif

    #endregion

    #region - Assets -

    public const string DEFAULT_PACKAGE_NAME = "Game";

    private const string ASSETS_GROUP = "Assets";
    public const string DEFAULT_FILE_PATH = "Assets/Resources/GlobeConfig.asset";
    public const string CONFIG_FILE_NAME = "GlobeConfig";

#if UNITY_EDITOR
    [BoxGroup(ASSETS_GROUP), LabelText("编辑器加载")]
    public bool isEditorMode;

    [OnInspectorGUI, BoxGroup(ASSETS_GROUP)]
    public void OnInspectorGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label($"当前资源模式：{(isEditorMode ? "编辑器加载" : isStandaloneMode ? "StreamingAssets加载" : "CDN加载")}");
        GUILayout.Label($"打包后资源模式：{(isStandaloneMode ? "StreamingAssets加载" : "CDN加载")}");

        if (!isStandaloneMode)
        {
            var active = GetActiveCDN();
            GUILayout.Label($"当前CDN环境：{(active != null ? active.name : "无")}");
        }

#if UNITY_WEBGL
        GUILayout.Label($"WEBGL下无Standalone模式");
#endif
    }
#endif

    [FormerlySerializedAs("isStandalone"), LabelText("StreamingAssets加载")]
    [BoxGroup(ASSETS_GROUP)]
    public bool isStandaloneMode;

    /// <summary>
    /// 单个CDN环境配置：包含环境名称、唯一ID、默认/备用CDN、备注
    /// </summary>
    [Serializable]
    public class CDNConfig
    {
        /// <summary> 环境显示名（如：生产环境、测试环境） </summary>
        [LabelText("环境名称"), Required]
        public string name;

        /// <summary> 环境唯一ID，用于下拉框存储与定位 </summary>
        [HideInInspector]
        public string id;

        /// <summary> 该环境下使用的默认CDN地址 </summary>
        [LabelText("默认CDN"), Required]
        public string defaultCDN;

        /// <summary> 该环境下使用的备用CDN地址（可选） </summary>
        [LabelText("备用CDN")]
        public string fallbackCDN;

        /// <summary> 环境备注（用途、负责人等） </summary>
        [LabelText("描述"), TextArea(2, 4)]
        public string description;
    }

    /// <summary> 多组CDN环境配置列表，支持增删改查 </summary>
    [HideIf(nameof(isStandaloneMode)), BoxGroup(ASSETS_GROUP), LabelText("CDN环境配置")]
    [ListDrawerSettings(
        DraggableItems = true,
        ShowItemCount = true,
        CustomAddFunction = nameof(CreateNewCDNConfig))]
    [OnValueChanged(nameof(RefreshActiveCDN))]
    public List<CDNConfig> cdnConfigs = new List<CDNConfig>();

    /// <summary> 当前激活的CDN环境ID（下拉框存ID，UI显示名称） </summary>
    [HideIf(nameof(isStandaloneMode)), BoxGroup(ASSETS_GROUP), LabelText("当前环境")]
    [ValueDropdown(nameof(GetEnvironmentOptions))]
    [OnValueChanged(nameof(RefreshActiveCDN))]
    public string currentEnvironmentId;

    // 兼容旧字段：由当前激活环境自动同步，禁止在Inspector中编辑
    [HideInInspector, ReadOnly]
    public string defaultCDN;

    [HideInInspector, ReadOnly]
    public string fallbackCDN;

    /// <summary> 运行时由 Updater 写入的可用CDN（资源加载使用） </summary>
    [HideInInspector]
    public string usableCDN;

    /// <summary> 迁移完成标记，避免重复执行 </summary>
    [HideInInspector, NonSerialized]
    private bool _cdnMigrated;

    /// <summary> 资源初始化（包含旧配置迁移、ID查重、当前环境兜底） </summary>
    private void OnEnable()
    {
        if (_cdnMigrated)
            return;

        if (cdnConfigs == null)
            cdnConfigs = new List<CDNConfig>();

        // 从旧 defaultCDN/fallbackCDN 迁移到列表
        if (cdnConfigs.Count == 0)
        {
            cdnConfigs.Add(new CDNConfig
            {
                id = Guid.NewGuid().ToString("N"),
                name = "Production",
                defaultCDN = defaultCDN ?? string.Empty,
                fallbackCDN = fallbackCDN ?? string.Empty,
                description = "从旧配置自动迁移"
            });
        }

        EnsureUniqueIds();
        EnsureCurrentEnvironment();
        RefreshActiveCDN();
        _cdnMigrated = true;
    }

#if UNITY_EDITOR
    /// <summary> 编辑器校验：保证ID唯一、当前环境有效、字段同步 </summary>
    private void OnValidate()
    {
        if (cdnConfigs == null)
            cdnConfigs = new List<CDNConfig>();

        EnsureUniqueIds();
        EnsureCurrentEnvironment();
        RefreshActiveCDN();
    }
#endif

    /// <summary> 获取当前激活的CDN环境（找不到则返回第一个或null） </summary>
    public CDNConfig GetActiveCDN()
    {
        if (cdnConfigs == null || cdnConfigs.Count == 0)
            return null;

        var found = cdnConfigs.FirstOrDefault(c => c != null && c.id == currentEnvironmentId);
        return found != null ? found : cdnConfigs[0];
    }

    /// <summary> 按ID查找CDN配置 </summary>
    public CDNConfig GetCDN(string id)
    {
        if (cdnConfigs == null || string.IsNullOrEmpty(id))
            return null;
        return cdnConfigs.FirstOrDefault(c => c != null && c.id == id);
    }

    /// <summary> 切换到指定ID的CDN环境，成功返回true </summary>
    public bool SwitchEnvironment(string id)
    {
        if (GetCDN(id) == null)
            return false;

        currentEnvironmentId = id;
        RefreshActiveCDN();
        return true;
    }

    /// <summary> 用当前激活的CDN刷新 legacy 字段，供旧调用方继续使用 </summary>
    public void RefreshActiveCDN()
    {
        var active = GetActiveCDN();
        if (active != null)
        {
            defaultCDN = active.defaultCDN ?? string.Empty;
            fallbackCDN = active.fallbackCDN ?? string.Empty;
        }
        else
        {
            defaultCDN = string.Empty;
            fallbackCDN = string.Empty;
        }
    }

    /// <summary> 保证所有CDN配置的ID非空且唯一 </summary>
    private void EnsureUniqueIds()
    {
        if (cdnConfigs == null)
            return;

        var seen = new HashSet<string>();
        foreach (var c in cdnConfigs)
        {
            if (c == null)
                continue;
            if (string.IsNullOrEmpty(c.id) || !seen.Add(c.id))
                c.id = Guid.NewGuid().ToString("N");
            seen.Add(c.id);
        }
    }

    /// <summary> 保证 currentEnvironmentId 指向一个有效配置 </summary>
    private void EnsureCurrentEnvironment()
    {
        if (cdnConfigs == null || cdnConfigs.Count == 0)
        {
            currentEnvironmentId = string.Empty;
            return;
        }

        if (string.IsNullOrEmpty(currentEnvironmentId) ||
            !cdnConfigs.Any(c => c != null && c.id == currentEnvironmentId))
        {
            currentEnvironmentId = cdnConfigs[0].id;
        }
    }

    /// <summary> 下拉框数据源：显示名称，存储ID </summary>
    private ValueDropdownList<string> GetEnvironmentOptions()
    {
        var list = new ValueDropdownList<string>();
        if (cdnConfigs == null)
            return list;

        foreach (var c in cdnConfigs)
        {
            if (c == null)
                continue;
            var label = string.IsNullOrEmpty(c.name) ? c.id : c.name;
            list.Add(label, c.id);
        }
        return list;
    }

    /// <summary> List 新增条目工厂：自动分配ID与默认名 </summary>
    private CDNConfig CreateNewCDNConfig()
    {
        return new CDNConfig
        {
            id = Guid.NewGuid().ToString("N"),
            name = $"Environment {(cdnConfigs?.Count ?? 0) + 1}",
            defaultCDN = string.Empty,
            fallbackCDN = string.Empty,
            description = string.Empty
        };
    }

    #endregion

    #region - Prefab -

    private const string PREFAB_GROUP = "Prefab";
    private const string PREFAB_OUTPUT = "Assets/Prefabs";
    private const string JSON_OUTPUT = "Assets/Prefabs/Configs/Json";

    [FolderPath]
    [BoxGroup(PREFAB_GROUP)]
    [LabelText("预制体文件夹")]
    public string prefabDirectory = PREFAB_OUTPUT;

    [FolderPath]
    [BoxGroup(PREFAB_GROUP)]
    [LabelText("Json文件夹")]
    public string configDirectory = JSON_OUTPUT;

    #endregion

    #region - 通知 -

#if TEST || UNITY_EDITOR

    private const string NOTICER_GROUP = "Noticer";

    public NoticerBase GetNoticer()
    {
        switch (noticerType)
        {
            case ENoticeType.DingTalk:
                return DingTalkNoticer.GetInstance();

            case ENoticeType.Feishu:
                return FeishuNoticer.GetInstance();

            case ENoticeType.WorkWeixin:
            default:
                break;
        }
        return null;
    }

    public enum ENoticeType
    {
        DingTalk,
        Feishu,
        WorkWeixin,
        EMail
    }

    [EnumPaging, BoxGroup(NOTICER_GROUP), LabelText("错误通知")]
    public ENoticeType noticerType;

    //钉钉
    private bool isDingTalk => noticerType == ENoticeType.DingTalk;

    [BoxGroup(NOTICER_GROUP), ShowIf(nameof(isDingTalk)), LabelText("Token")]
    public string dingTalkToken;

    [BoxGroup(NOTICER_GROUP), ShowIf(nameof(isDingTalk)), LabelText("密钥")]
    public string dingTalkSecret;

    //飞书
    private bool isFeishu => noticerType == ENoticeType.Feishu;

    [BoxGroup(NOTICER_GROUP), ShowIf(nameof(isFeishu)), LabelText("Token")]
    public string feishuToken;

    //企业微信
    private bool isWeixinWork => noticerType == ENoticeType.WorkWeixin;

    [BoxGroup(NOTICER_GROUP), ShowIf(nameof(isWeixinWork)), LabelText("Token")]
    public string wxToken;

#endif

    #endregion

    #region - Server -

#if UNITY_EDITOR

    private const string SERVER_GROUP = "Server";

    [BoxGroup(SERVER_GROUP), LabelText("运行服务器")]
    public bool runServer;

    [InitializeOnEnterPlayMode]
    private static void RunServer()
    {
        if (GetInstance().runServer)
            ReflectionHelper.InvokeStaticMethod("Lin.Editor.Command.ServerCommands,Lin.Editor", "Run");
        else
            Log.Debug("Server", "不启动服务器");
    }

#endif

    #endregion
}

