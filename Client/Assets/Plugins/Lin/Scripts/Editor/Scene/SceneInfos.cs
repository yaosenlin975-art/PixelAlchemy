/*
┌────────────────────────────┐
│　Description：用分拆窗口中显示项目中的场景
│　Remark：
└────────────────────────────┘
*/

using System;

namespace Lin.Editor.Scene.Spliter
{
    [Serializable]
    public class SceneInfos
    {
        public SceneInfos(string path, bool select = false)
        {
            this.path = path;
            this.select = select;
            name = System.IO.Path.GetFileNameWithoutExtension(path);
        }

        public string path;
        public string name;
        public bool select;
    }
}