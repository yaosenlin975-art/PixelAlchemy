/*
┌────────────────────────────┐
│　Description：ApkUpdater
│　Remark：软件整包更新
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：ApkUpdater
└──────────────┘
*/
using Cysharp.Text;
using Lin.Runtime.Const;
using Lin.Runtime.Helper;
using Newtonsoft.Json;
using System;

namespace Lin.Runtime.Resource.Updater
{
    public class iOSUpdater : UpdaterBase<iOSVersionDescriptions>
    {
        protected override string RemoteVersionPath => $"iOS/Application/{ResourceConst.VERSION_FILE_NAME}";

        protected override string RemoteVersionInfosPath => $"iOS/Application/{ResourceConst.VERSION_INFOS_FILE_NAME}";

        public iOSUpdater(string defaultServer, string fallbackServer) : base(defaultServer, fallbackServer) { }

        public override void Dispose() { }
    }

    /// <summary> 版本更新具体说明 </summary>
    [Serializable]
    public struct iOSVersionDescriptions
    {
        public string[] descriptions;

        public override string ToString() => JsonConvert.SerializeObject(this);

        public string GetDescriptions()
        {
            using var sb = ZString.CreateStringBuilder();
            for (int i = 0; i < descriptions.Length; i++)
            {
                sb.Append(i + 1);
                sb.Append('.');
                sb.Append(descriptions[i]);
                if (i < descriptions.Length - 1)
                    sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}