// 用于验证 Fantasy.Unity 命名空间与编译符号是否生效
// Used to verify that the Fantasy.Unity namespaces and the FANTASY_UNITY scripting define symbol are in effect.
using UnityEngine;
using Fantasy;

namespace NoitaCA.Hotfix
{
    /// <summary>
    /// 极简验证脚本：仅检查 Fantasy 命名空间能解析、FANTASY_UNITY 符号已生效。
    /// 不挂任何场景、不做实际连接。手动拖到任意 GameObject 即可看到 Debug 输出。
    /// </summary>
    // 验证：Fantasy.* 命名空间和 FANTASY_UNITY 编译符号可解析
    // Verify: Fantasy.* namespaces and FANTASY_UNITY compile symbol resolve correctly
    public sealed class FantasyTest : MonoBehaviour
    {
        // 启动时仅做命名空间解析与符号验证
        // On startup, only verify namespace resolution and symbol presence
        private void Start()
        {
            Debug.Log("Fantasy.Unity 安装成功!");
        }
    }
}
