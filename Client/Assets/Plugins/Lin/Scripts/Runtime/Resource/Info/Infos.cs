/*
┌────────────────────────────┐
│　Description: 信息体
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: Infos
└──────────────┘
*/
using Lin.Runtime.Interface;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Lin.Runtime.Resource
{
    /// <summary> 安装包版本更新信息 </summary>
    [Serializable]
    public class Infos : IVersion, IDisposable
    {
        public const string PACKAGES_INFOS_KEY = nameof(PACKAGES_INFOS_KEY);

        [LabelText("版本号")] public string version;
        [LabelText("说明"), TextArea] public List<string> infos;
        [HideInInspector] public string timeStamp;

        public string GetVersion() => $"{version}.{timeStamp}";

        public string GetInfos()
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                stringBuilder.Append(i + 1);
                stringBuilder.Append(". ");
                stringBuilder.Append(info);
                if (i < infos.Count - 1)
                    stringBuilder.Append('\n');
            }
            return stringBuilder.ToString();
        }

        public override string ToString() => JsonConvert.SerializeObject(this);

        public void Dispose()
        {
            version = null;
            infos?.Clear();
            infos = null;
            timeStamp = null;
        }

        //TODO:DLC
    }
}