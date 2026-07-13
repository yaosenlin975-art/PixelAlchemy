/*
┌────────────────────────────┐
│　Description: Bundle解密类
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: DecryptionServices
└──────────────┘
*/
using YooAsset;

namespace Lin.Runtime.Resource
{
    /// <summary>
    /// 远端资源地址查询服务类
    /// </summary>
    class RemoteServices : IRemoteServices
    {
        private readonly string _defaultHostServer;
        private readonly string _fallbackHostServer;

        public RemoteServices(string defaultHostServer, string fallbackHostServer)
        {
            _defaultHostServer = defaultHostServer;
            _fallbackHostServer = fallbackHostServer;
        }

        string IRemoteServices.GetRemoteMainURL(string fileName) => $"{_defaultHostServer}/{fileName}";

        string IRemoteServices.GetRemoteFallbackURL(string fileName) => $"{_fallbackHostServer}/{fileName}";
    }
}
