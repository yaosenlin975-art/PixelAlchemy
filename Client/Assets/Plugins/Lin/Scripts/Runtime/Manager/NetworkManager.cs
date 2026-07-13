/*
┌────────────────────────────┐
│　Description: 图集管理
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: SpriteManager
└──────────────┘
*/
using Lin.Runtime.DesignPattern.Singleton;

namespace Lin.Runtime.Manager
{
    public class NetworkManager : Singleton<NetworkManager>
    {
        private int rpcID;
        private object idLocker = new object();

        public int GetRpcID()
        {
            lock (idLocker)
            {
                rpcID++;
                return rpcID;
            }
        }
    }
}
