/*
┌────────────────────────────┐
│　Description: 红点管理器
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: RedDotManager
└──────────────┘
*/
using Lin.Runtime.DesignPattern.Singleton;
using System.Collections.Generic;

namespace Lin.Runtime.UI.RedDot
{
    public class RedDotManager : Singleton<RedDotManager>
    {
        Dictionary<string, RedDotNode> nodes;

        public RedDotManager()
        {
            nodes = new Dictionary<string, RedDotNode>();
        }

        public RedDotNode GetNode(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            nodes.TryGetValue(name, out RedDotNode node);
            return node;
        }

        public void Register(RedDotNode node, string parentName = null)
        {
            if (nodes.ContainsKey(node.name))
                nodes[node.name] = node;
            else
                nodes.Add(node.name, node);

            var parent = GetNode(parentName);
            parent?.RegistNode(node);
        }

        public void Unregister(RedDotNode node)
        {
            nodes.Remove(node.name);
        }
    }
}