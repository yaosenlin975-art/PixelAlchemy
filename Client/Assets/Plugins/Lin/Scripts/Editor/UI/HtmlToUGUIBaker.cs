using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Lin.Editor.Helper;
using Lin.Runtime.Helper;
using Lin.Runtime.UI;
using System;

namespace Lin.Editor.UI
{
    [System.Serializable]
    public class UIDataNode
    {
        public string name;
        public string type;
        public string dir;
        public float value;
        public bool isChecked;
        public List<string> options;
        public float x;
        public float y;
        public float width;
        public float height;
        public string color;
        public string fontColor;
        public int fontSize;
        public string textAlign;
        public string text;
        public List<UIDataNode> children;
    }

    public class HtmlToUGUIBaker : EditorWindow
    {
        private string uiName = "HomePanel";
        private string rawJsonString = "";
        private Canvas targetCanvas;
        private bool autoGenerate = true;
        private bool isItem = false;
        private string selectedHtmlPath = "";
        private string lastOpenedFolder = "";

        private Vector2 targetResolution = new Vector2(1920, 1080);

        private const float EDGE_THRESHOLD = 0.15f;

        private static readonly Dictionary<string, string> TypeToPrefix = new Dictionary<string, string>
        {
            { "button", "Button" },
            { "text", "Text" },
            { "image", "Image" },
            { "input", "TMP_InputField" },
            { "scroll", "ScrollRect" },
            { "toggle", "Toggle" },
            { "slider", "Slider" },
            { "dropdown", "Dropdown" },
            { "div", "" },
        };

        private string autoBakeStatus = "";
        private volatile string pendingBakeJson = null;
        private volatile bool autoBakeRunning = false;
        private System.DateTime autoBakeStartTime;
        private volatile Thread bakeListenerThread;
        private volatile TcpListener bakeTcpListener;

        [MenuItem("Lin/UI/HTML to UGUI Baker")]
        public static void ShowWindow()
        {
            GetWindow<HtmlToUGUIBaker>("HTML to UGUI Baker");
        }

        private void OnDestroy()
        {
            StopAutoBakeServer();
        }

        private void OnGUI()
        {
            GUILayout.Label("HTML 原型 → UGUI + Lin Generator", EditorStyles.boldLabel);
            GUILayout.Space(8);

            targetCanvas = (Canvas)EditorGUILayout.ObjectField("目标 Canvas", targetCanvas, typeof(Canvas), true);

            uiName = EditorGUILayout.TextField("UI 名称", uiName);
            isItem = EditorGUILayout.Toggle("UIBehaviour (非Panel)", isItem);

            GUILayout.Space(4);
            GUILayout.Label("目标分辨率", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("宽:", GUILayout.Width(24));
            int w = EditorGUILayout.IntField((int)targetResolution.x, GUILayout.Width(80));
            GUILayout.Label("高:", GUILayout.Width(24));
            int h = EditorGUILayout.IntField((int)targetResolution.y, GUILayout.Width(80));
            targetResolution = new Vector2(Mathf.Max(1, w), Mathf.Max(1, h));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("选择 HTML 原型:", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("浏览...", GUILayout.Width(80), GUILayout.Height(24)))
            {
                BrowseHtmlFile();
            }
            GUILayout.Label(string.IsNullOrEmpty(selectedHtmlPath) ? "未选择" : Path.GetFileName(selectedHtmlPath), EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(selectedHtmlPath) && !autoBakeRunning)
            {
                if (GUILayout.Button("重新烘焙", GUILayout.Width(80), GUILayout.Height(24)))
                {
                    AutoBakeFromHtml();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            if (autoBakeRunning)
            {
                Rect r = GUILayoutUtility.GetRect(10, 18);
                EditorGUI.ProgressBar(r, -1f, "正在自动烘焙...");
            }
            else if (!string.IsNullOrEmpty(autoBakeStatus))
            {
                GUILayout.Label(autoBakeStatus, EditorStyles.wordWrappedLabel);
            }

            GUILayout.Space(4);

            if (!string.IsNullOrEmpty(rawJsonString))
            {
                GUILayout.Label($"  已加载 JSON: {rawJsonString.Length} 字符", EditorStyles.miniLabel);
            }
            else if (!autoBakeRunning)
            {
                GUILayout.Label("  未加载 JSON — 选择 HTML 文件后自动烘焙", EditorStyles.miniLabel);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("从剪贴板粘贴 (手动)", GUILayout.Height(20)))
                {
                    string clip = GUIUtility.systemCopyBuffer;
                    if (!string.IsNullOrWhiteSpace(clip) && clip.TrimStart().StartsWith("{"))
                    {
                        rawJsonString = clip;
                        autoBakeStatus = "已从剪贴板粘贴 JSON";
                        Debug.Log("[HtmlToUGUIBaker] 已从剪贴板粘贴 JSON");
                    }
                    else
                    {
                        Debug.LogWarning("[HtmlToUGUIBaker] 剪贴板中没有有效 JSON");
                    }
                }
                GUILayout.EndHorizontal();
            }

            autoGenerate = EditorGUILayout.Toggle("烘焙后自动生成脚本+预制体", autoGenerate);

            GUILayout.Space(12);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("执行烘焙生成", GUILayout.Height(40)))
            {
                ExecuteBake();
            }
            GUI.backgroundColor = Color.white;
        }

        private void BrowseHtmlFile()
        {
            string startDir = string.IsNullOrEmpty(lastOpenedFolder) ? Path.GetFullPath(".") : lastOpenedFolder;
            string path = EditorUtility.OpenFilePanel("选择 HTML 原型文件", startDir, "html");
            if (string.IsNullOrEmpty(path)) return;

            selectedHtmlPath = path.Replace('\\', '/');
            lastOpenedFolder = Path.GetDirectoryName(path);

            string htmlName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(uiName) || uiName == "HomePanel")
            {
                uiName = char.ToUpperInvariant(htmlName[0]) + htmlName.Substring(1);
                if (!uiName.EndsWith("Panel") && !uiName.EndsWith("Popup"))
                    uiName += "Panel";
            }

            AutoBakeFromHtml();
        }

#region Auto Bake

        private void AutoBakeFromHtml()
        {
            if (autoBakeRunning)
                StopAutoBakeServer();

            if (!File.Exists(selectedHtmlPath))
            {
                autoBakeStatus = "HTML 文件不存在";
                return;
            }

            string htmlContent = File.ReadAllText(selectedHtmlPath);

            if (!htmlContent.Contains("data-u-type") || !htmlContent.Contains("data-u-name"))
            {
                autoBakeStatus = "HTML 中未找到 data-u-type/data-u-name 属性";
                string jsonPath = Path.Combine(
                    Path.GetDirectoryName(selectedHtmlPath),
                    Path.GetFileNameWithoutExtension(selectedHtmlPath) + ".json");
                if (File.Exists(jsonPath))
                {
                    rawJsonString = File.ReadAllText(jsonPath);
                    autoBakeStatus += $"，已加载同目录 .json ({rawJsonString.Length} 字符)";
                }
                return;
            }

            int port = FindFreePort();
            string injectedHtml = InjectAutoBakeScript(htmlContent, port);

            pendingBakeJson = null;
            autoBakeRunning = true;
            autoBakeStartTime = System.DateTime.Now;
            autoBakeStatus = "正在自动烘焙...";

            bakeListenerThread = new Thread(() => RunBakeServer(port, injectedHtml));
            bakeListenerThread.IsBackground = true;
            bakeListenerThread.Start();

            EditorApplication.delayCall += () =>
            {
                Application.OpenURL($"http://localhost:{port}/");
                EditorApplication.update += PollAutoBakeResult;
            };
        }

        private void RunBakeServer(int port, string htmlContent)
        {
            try
            {
                bakeTcpListener = new TcpListener(IPAddress.Loopback, port);
                bakeTcpListener.Start();

                while (autoBakeRunning)
                {
                    if (!bakeTcpListener.Pending())
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    using (var client = bakeTcpListener.AcceptTcpClient())
                    {
                        client.ReceiveTimeout = 5000;
                        client.SendTimeout = 5000;
                        using (var stream = client.GetStream())
                        {
                            var readBuffer = new byte[65536];
                            int bytesRead = stream.Read(readBuffer, 0, readBuffer.Length);
                            if (bytesRead == 0) continue;

                            string rawData = Encoding.UTF8.GetString(readBuffer, 0, bytesRead);
                            string[] headerBody = rawData.Split(new[] { "\r\n\r\n" }, 2, StringSplitOptions.None);
                            string requestLine = headerBody[0].Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
                            string[] requestParts = requestLine.Split(' ');
                            string method = requestParts[0];
                            string path = requestParts.Length > 1 ? requestParts[1] : "/";

                            if (method == "GET" && path == "/")
                            {
                                SendHttpResponse(stream, "200 OK", "text/html; charset=utf-8", htmlContent);
                            }
                            else if (method == "POST" && path == "/bake")
                            {
                                string body = headerBody.Length > 1 ? headerBody[1] : "";

                                int contentLength = 0;
                                foreach (string line in headerBody[0].Split(new[] { "\r\n" }, StringSplitOptions.None))
                                {
                                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                                        int.TryParse(line.Substring(15).Trim(), out contentLength);
                                }

                                if (contentLength > 0 && body.Length < contentLength)
                                {
                                    var extraBuf = new byte[contentLength - body.Length];
                                    int extraRead = stream.Read(extraBuf, 0, extraBuf.Length);
                                    body += Encoding.UTF8.GetString(extraBuf, 0, extraRead);
                                }

                                pendingBakeJson = body;
                                SendHttpResponse(stream, "200 OK", "text/plain", "OK");
                                autoBakeRunning = false;
                            }
                            else if (method == "OPTIONS")
                            {
                                SendOptionsResponse(stream);
                            }
                            else
                            {
                                SendHttpResponse(stream, "404 Not Found", "text/plain", "");
                            }
                        }
                    }
                }

                bakeTcpListener.Stop();
            }
            catch (ThreadAbortException) { }
            catch (SocketException) { }
            catch (Exception ex)
            {
                if (autoBakeRunning)
                    Debug.LogWarning($"[HtmlToUGUIBaker] 自动烘焙服务异常: {ex.Message}");
                autoBakeRunning = false;
            }
        }

        private void SendHttpResponse(NetworkStream stream, string status, string contentType, string body)
        {
            var sb = new StringBuilder();
            sb.Append($"HTTP/1.1 {status}\r\n");
            sb.Append($"Content-Type: {contentType}\r\n");
            sb.Append("Access-Control-Allow-Origin: *\r\n");
            sb.Append("Connection: close\r\n");
            sb.Append("\r\n");
            sb.Append(body);
            byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
            stream.Write(data, 0, data.Length);
        }

        private void SendOptionsResponse(NetworkStream stream)
        {
            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 200 OK\r\n");
            sb.Append("Access-Control-Allow-Origin: *\r\n");
            sb.Append("Access-Control-Allow-Methods: POST, GET, OPTIONS\r\n");
            sb.Append("Access-Control-Allow-Headers: Content-Type\r\n");
            sb.Append("Connection: close\r\n");
            sb.Append("\r\n");
            byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
            stream.Write(data, 0, data.Length);
        }

        private void PollAutoBakeResult()
        {
            if (pendingBakeJson != null)
            {
                rawJsonString = pendingBakeJson;
                pendingBakeJson = null;
                autoBakeRunning = false;
                autoBakeStatus = $"自动烘焙完成 ({rawJsonString.Length} 字符)";
                EditorApplication.update -= PollAutoBakeResult;
                Repaint();
                Debug.Log("[HtmlToUGUIBaker] 自动烘焙完成，JSON 已加载");
                return;
            }

            if (!autoBakeRunning)
            {
                if (string.IsNullOrEmpty(rawJsonString))
                    autoBakeStatus = "自动烘焙超时或失败，可手动粘贴 JSON";
                EditorApplication.update -= PollAutoBakeResult;
                Repaint();
                return;
            }

            if ((System.DateTime.Now - autoBakeStartTime).TotalSeconds > 20)
            {
                autoBakeStatus = "自动烘焙超时 (20s)，可手动粘贴 JSON";
                StopAutoBakeServer();
                EditorApplication.update -= PollAutoBakeResult;
                Repaint();
            }
        }

        private void StopAutoBakeServer()
        {
            autoBakeRunning = false;
            try { bakeTcpListener?.Stop(); } catch { }
            try { bakeListenerThread?.Join(1000); } catch { }
            bakeTcpListener = null;
            bakeListenerThread = null;
        }

        private int FindFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private string InjectAutoBakeScript(string html, int port)
        {
            string script = $@"
<script>
(function(){{
    function autoBake(){{
        var all=document.querySelectorAll('[data-u-type][data-u-name]');
        if(!all.length){{console.error('[AutoBake] No data-u-type/data-u-name elements found');return;}}
        function rgb2hex(c){{
            if(!c)return'#FFFFFF00';
            if(c==='transparent'||c==='rgba(0,0,0,0)'||c==='initial'||c==='inherit'||c==='none')return'#FFFFFF00';
            var m=c.match(/^rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)$/);
            if(!m)return'#FFFFFF';
            var r=('0'+parseInt(m[1],10).toString(16)).slice(-2);
            var g=('0'+parseInt(m[2],10).toString(16)).slice(-2);
            var b=('0'+parseInt(m[3],10).toString(16)).slice(-2);
            return'#'+r+g+b;
        }}
        function capture(el){{
            var t=el.getAttribute('data-u-type'),n=el.getAttribute('data-u-name');
            if(!t||!n)return null;
            var r=el.getBoundingClientRect(),s=getComputedStyle(el);
            var d={{
                name:n,type:t,
                dir:el.getAttribute('data-u-dir')||'v',
                value:parseFloat(el.getAttribute('data-u-value'))||0,
                isChecked:el.getAttribute('data-u-checked')==='true',
                options:[],
                x:Math.round(r.left),
                y:Math.round(r.top),
                width:Math.round(r.width),
                height:Math.round(r.height),
                color:rgb2hex(s.backgroundColor),
                fontColor:rgb2hex(s.color),
                fontSize:Math.round(parseFloat(s.fontSize)||14),
                textAlign:s.textAlign||'center',
                text:(el.innerText||'').trim(),
                children:[]
            }};
            if(t==='dropdown'&&el.tagName.toLowerCase()==='select'){{
                el.querySelectorAll('option').forEach(function(o){{d.options.push(o.innerText.trim());}});
            }}
            return d;
        }}
        var map=new Map();
        var nodes=[];
        all.forEach(function(el){{
            var d=capture(el);
            if(d){{map.set(el,d);nodes.push({{el:el,data:d}});}}
        }});
        var roots=[];
        nodes.forEach(function(item){{
            var el=item.el;
            var cur=el.parentElement;
            var found=false;
            while(cur){{
                if(map.has(cur)){{map.get(cur).children.push(item.data);found=true;break;}}
                cur=cur.parentElement;
            }}
            if(!found)roots.push(item.data);
        }});
        var result=roots.length===1?roots[0]:{{
            name:'Root',type:'div',
            dir:'v',value:0,isChecked:false,options:[],
            x:0,y:0,
            width:document.documentElement.clientWidth,
            height:document.documentElement.clientHeight,
            color:'#FFFFFF00',fontColor:'#000000',
            fontSize:14,textAlign:'center',text:'',
            children:roots
        }};
        var j=JSON.stringify(result);
        fetch('/bake',{{
            method:'POST',
            headers:{{'Content-Type':'application/json'}},
            body:j
        }}).then(function(resp){{
            document.title='BakeDone';
            var msg=document.createElement('div');
            msg.textContent='Bake Done, you can close this page';
            msg.style.cssText='position:fixed;top:0;left:0;width:100%;height:100%;display:flex;align-items:center;justify-content:center;background:#28a745;color:#fff;font-size:28px;font-family:sans-serif;z-index:99999;';
            document.body.innerHTML='';
            document.body.appendChild(msg);
        }}).catch(function(e){{
            console.error('[AutoBake] Send failed:',e);
        }});
    }}
    if(document.readyState==='loading'){{
        document.addEventListener('DOMContentLoaded',function(){{setTimeout(autoBake,500);}});
    }}else{{
        setTimeout(autoBake,500);
    }}
}})();
</script>";

            int bodyEndIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (bodyEndIndex >= 0)
                return html.Insert(bodyEndIndex, script);
            return html + script;
        }

        #endregion

        private void ExecuteBake()
        {
            if (targetCanvas == null)
            {
                Debug.LogError("[HtmlToUGUIBaker] 未指定目标 Canvas");
                return;
            }

            if (string.IsNullOrWhiteSpace(rawJsonString))
            {
                Debug.LogError("[HtmlToUGUIBaker] JSON 内容为空");
                return;
            }

            if (string.IsNullOrWhiteSpace(uiName))
            {
                Debug.LogError("[HtmlToUGUIBaker] UI 名称不能为空");
                return;
            }

            ConfigureCanvasScaler(targetCanvas);

            UIDataNode rootNode = JsonUtility.FromJson<UIDataNode>(rawJsonString);
            if (rootNode == null)
            {
                Debug.LogError("[HtmlToUGUIBaker] JSON 解析失败");
                return;
            }

            rootNode.name = uiName;
            float srcPageW = rootNode.width;
            float srcPageH = rootNode.height;

            if (srcPageW <= 0 || srcPageH <= 0)
            {
                Debug.LogError("[HtmlToUGUIBaker] 根节点 width/height 无效，无法计算缩放比例");
                return;
            }

            Debug.Log($"[HtmlToUGUIBaker] 源页面: {srcPageW}x{srcPageH}, 目标分辨率: {targetResolution.x}x{targetResolution.y}");

            GameObject rootGo = CreateUINode(rootNode, targetCanvas.transform, srcPageW, srcPageH);

            Undo.RegisterCreatedObjectUndo(rootGo, "Bake UI Prototype");
            Selection.activeGameObject = rootGo;

            Debug.Log($"[HtmlToUGUIBaker] UGUI 物体创建完成: {rootGo.name}");

            if (autoGenerate)
            {
                MenuExtension.OptimizeBatch(rootGo);

                if (isItem)
                    Generator.GenerateItemScripts(rootGo);
                else
                    Generator.GeneratePanelScripts(rootGo);

                Debug.Log($"[HtmlToUGUIBaker] 已调用 Lin Generator 生成脚本和预制体");
            }
        }

        private void ConfigureCanvasScaler(Canvas canvas)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = targetResolution;
            scaler.matchWidthOrHeight = 0.5f;
        }

        #region Anchor & Scale Helpers

        /// <summary>
        /// 根据组件中心点在源页面中的位置，计算应使用的锚点。
        /// 距离页面边缘在 EDGE_THRESHOLD 以内视为"靠近该边缘"。
        /// HTML 坐标系: 左上角原点, y 轴向下。
        /// </summary>
        private Vector2 ComputeAnchor(float centerX, float centerY, float pageWidth, float pageHeight)
        {
            float normX = centerX / pageWidth;
            float normY = centerY / pageHeight;

            float anchorX, anchorY;

            if (normX <= EDGE_THRESHOLD)
                anchorX = 0f;
            else if (normX >= (1f - EDGE_THRESHOLD))
                anchorX = 1f;
            else
                anchorX = 0.5f;

            if (normY <= EDGE_THRESHOLD)
                anchorY = 1f;  // 靠近页面顶部 → UGUI 的 y=1
            else if (normY >= (1f - EDGE_THRESHOLD))
                anchorY = 0f;  // 靠近页面底部 → UGUI 的 y=0
            else
                anchorY = 0.5f;

            return new Vector2(anchorX, anchorY);
        }

        /// <summary>
        /// 判断组件是否应该拉伸（尺寸接近页面尺寸）。
        /// </summary>
        private bool ShouldStretch(float componentSize, float pageSize)
        {
            return componentSize >= pageSize * (1f - EDGE_THRESHOLD * 2f);
        }

        /// <summary>
        /// 按源/目标分辨率比例缩放一个数值。
        /// </summary>
        private float ScaleValue(float value, float srcSize, float dstSize)
        {
            if (srcSize <= 0f) return value;
            return value * (dstSize / srcSize);
        }

        /// <summary>
        /// 计算 RectTransform 的锚点、pivot、anchoredPosition 和 sizeDelta。
        /// 综合考虑边缘锚定和尺寸缩放。
        /// </summary>
        private void ComputeRectTransform(
            UIDataNode node, float srcPageW, float srcPageH,
            out Vector2 anchoredPosition, out Vector2 sizeDelta,
            out Vector2 anchorMin, out Vector2 anchorMax, out Vector2 pivot)
        {
            float cx = node.x + node.width * 0.5f;
            float cy = node.y + node.height * 0.5f;

            Vector2 anchor = ComputeAnchor(cx, cy, srcPageW, srcPageH);
            bool stretchH = ShouldStretch(node.width, srcPageW);
            bool stretchV = ShouldStretch(node.height, srcPageH);

            float scaledW = ScaleValue(node.width, srcPageW, targetResolution.x);
            float scaledH = ScaleValue(node.height, srcPageH, targetResolution.y);

            if (stretchH && stretchV)
            {
                anchorMin = Vector2.zero;
                anchorMax = Vector2.one;
                pivot = new Vector2(0.5f, 0.5f);

                float leftMargin = ScaleValue(node.x, srcPageW, targetResolution.x);
                float topMargin = ScaleValue(node.y, srcPageH, targetResolution.y);
                float rightMargin = targetResolution.x - ScaleValue(node.x + node.width, srcPageW, targetResolution.x);
                float bottomMargin = targetResolution.y - ScaleValue(node.y + node.height, srcPageH, targetResolution.y);

                anchoredPosition = Vector2.zero;
                sizeDelta = new Vector2(-(leftMargin + rightMargin), -(topMargin + bottomMargin));
            }
            else if (stretchH)
            {
                anchorMin = new Vector2(0f, anchor.y);
                anchorMax = new Vector2(1f, anchor.y);
                pivot = new Vector2(0.5f, anchor.y);

                float leftMargin = ScaleValue(node.x, srcPageW, targetResolution.x);
                float rightMargin = targetResolution.x - ScaleValue(node.x + node.width, srcPageW, targetResolution.x);

                anchoredPosition = new Vector2(0f, ScaleValue(cy, srcPageH, targetResolution.y) - anchor.y * targetResolution.y);
                sizeDelta = new Vector2(-(leftMargin + rightMargin), scaledH);
            }
            else if (stretchV)
            {
                anchorMin = new Vector2(anchor.x, 0f);
                anchorMax = new Vector2(anchor.x, 1f);
                pivot = new Vector2(anchor.x, 0.5f);

                float topMargin = ScaleValue(node.y, srcPageH, targetResolution.y);
                float bottomMargin = targetResolution.y - ScaleValue(node.y + node.height, srcPageH, targetResolution.y);

                anchoredPosition = new Vector2(ScaleValue(cx, srcPageW, targetResolution.x) - anchor.x * targetResolution.x, 0f);
                sizeDelta = new Vector2(scaledW, -(topMargin + bottomMargin));
            }
            else
            {
                anchorMin = anchor;
                anchorMax = anchor;
                pivot = anchor;

                float scaledCenterX = ScaleValue(cx, srcPageW, targetResolution.x);
                float scaledCenterY = ScaleValue(cy, srcPageH, targetResolution.y);

                float anchorPixelX = anchor.x * targetResolution.x;
                float anchorPixelY = anchor.y * targetResolution.y;

                anchoredPosition = new Vector2(
                    scaledCenterX - anchorPixelX,
                    -(scaledCenterY - anchorPixelY)
                );
                sizeDelta = new Vector2(scaledW, scaledH);
            }
        }

        #endregion

        /// <summary>
        /// 创建 UGUI 节点（根节点入口），传入源页面尺寸。
        /// </summary>
        private GameObject CreateUINode(UIDataNode nodeData, Transform parent, float srcPageW, float srcPageH)
        {
            string goName = GetMarkedName(nodeData);
            GameObject go = new GameObject(goName);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();

            ComputeRectTransform(nodeData, srcPageW, srcPageH,
                out Vector2 anchoredPos, out Vector2 sizeD,
                out Vector2 ancMin, out Vector2 ancMax, out Vector2 piv);

            rect.anchorMin = ancMin;
            rect.anchorMax = ancMax;
            rect.pivot = piv;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeD;

            Transform childrenContainer = ApplyComponentByType(go, nodeData);

            if (nodeData.children != null && nodeData.children.Count > 0)
            {
                foreach (var childNode in nodeData.children)
                {
                    CreateUINodeRecursive(childNode, childrenContainer, srcPageW, srcPageH);
                }
            }

            return go;
        }

        /// <summary>
        /// 递归创建子节点，传入源页面尺寸用于缩放计算。
        /// </summary>
        private void CreateUINodeRecursive(UIDataNode nodeData, Transform parent, float srcPageW, float srcPageH)
        {
            string goName = GetMarkedName(nodeData);
            GameObject go = new GameObject(goName);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();

            ComputeRectTransform(nodeData, srcPageW, srcPageH,
                out Vector2 anchoredPos, out Vector2 sizeD,
                out Vector2 ancMin, out Vector2 ancMax, out Vector2 piv);

            rect.anchorMin = ancMin;
            rect.anchorMax = ancMax;
            rect.pivot = piv;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeD;

            Transform childrenContainer = ApplyComponentByType(go, nodeData);

            if (nodeData.children != null && nodeData.children.Count > 0)
            {
                foreach (var childNode in nodeData.children)
                {
                    CreateUINodeRecursive(childNode, childrenContainer, srcPageW, srcPageH);
                }
            }
        }

private string GetMarkedName(UIDataNode nodeData)
        {
            var typeLower = nodeData.type.ToLower();
            var sanitizedName = System.Text.RegularExpressions.Regex.Replace(nodeData.name, @"[^a-zA-Z0-9_\u4e00-\u9fff]", "");
            if (string.IsNullOrEmpty(sanitizedName))
                sanitizedName = nodeData.name;

            if (TypeToPrefix.TryGetValue(typeLower, out var prefix) && !string.IsNullOrEmpty(prefix))
            {
                var pascalName = char.ToUpperInvariant(sanitizedName[0]) + sanitizedName.Substring(1);
                return $"[{prefix}] {pascalName}";
            }
            return sanitizedName;
        }

        private Transform ApplyComponentByType(GameObject go, UIDataNode nodeData)
        {
            Color bgColor = ParseHexColor(nodeData.color, Color.white);
            Color fontColor = ParseHexColor(nodeData.fontColor, Color.black);
            int fontSize = nodeData.fontSize > 0 ? nodeData.fontSize : 24;

            TextAlignmentOptions alignment = ParseTextAlign(nodeData.textAlign);
            bool isMultiLine = nodeData.height > (fontSize * 1.5f);

            switch (nodeData.type.ToLower())
            {
                case "div":
                case "image":
                    Image img = go.AddComponent<Image>();
                    img.color = bgColor;
                    if (img.color.a <= 0.01f) img.raycastTarget = false;
                    return go.transform;

                case "text":
                    TextMeshProUGUI txt = go.AddComponent<TextMeshProUGUI>();
                    txt.text = nodeData.text;
                    txt.color = fontColor;
                    txt.fontSize = fontSize;
                    txt.alignment = alignment;
                    txt.enableWordWrapping = isMultiLine;
                    txt.overflowMode = isMultiLine ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;
                    txt.raycastTarget = false;
                    return go.transform;

                case "button":
                    Image btnImg = go.AddComponent<Image>();
                    btnImg.color = bgColor;
                    Button btn = go.AddComponent<Button>();
                    btn.targetGraphic = btnImg;

                    if (!string.IsNullOrEmpty(nodeData.text))
                    {
                        GameObject btnTxtGo = CreateChildRect(go, "Text (TMP)", Vector2.zero, Vector2.one);
                        TextMeshProUGUI btnTxt = btnTxtGo.AddComponent<TextMeshProUGUI>();
                        btnTxt.text = nodeData.text;
                        btnTxt.color = fontColor;
                        btnTxt.fontSize = fontSize;
                        btnTxt.alignment = alignment;
                        btnTxt.enableWordWrapping = false;
                        btnTxt.overflowMode = TextOverflowModes.Overflow;
                        btnTxt.raycastTarget = false;
                    }
                    return go.transform;

                case "input":
                    Image inputBg = go.AddComponent<Image>();
                    inputBg.color = bgColor;
                    TMP_InputField inputField = go.AddComponent<TMP_InputField>();
                    inputField.targetGraphic = inputBg;

                    GameObject textAreaGo = CreateChildRect(go, "Text Area", Vector2.zero, Vector2.one, new Vector2(10, 5), new Vector2(-10, -5));
                    textAreaGo.AddComponent<RectMask2D>();

                    GameObject phGo = CreateChildRect(textAreaGo, "Placeholder", Vector2.zero, Vector2.one);
                    TextMeshProUGUI phTxt = phGo.AddComponent<TextMeshProUGUI>();
                    phTxt.text = nodeData.text;
                    Color phColor = fontColor;
                    phColor.a = 0.5f;
                    phTxt.color = phColor;
                    phTxt.fontSize = fontSize;
                    phTxt.alignment = alignment;
                    phTxt.enableWordWrapping = false;
                    phTxt.raycastTarget = false;

                    GameObject textGo = CreateChildRect(textAreaGo, "Text", Vector2.zero, Vector2.one);
                    TextMeshProUGUI inTxt = textGo.AddComponent<TextMeshProUGUI>();
                    inTxt.color = fontColor;
                    inTxt.fontSize = fontSize;
                    inTxt.alignment = alignment;
                    inTxt.enableWordWrapping = false;
                    inTxt.raycastTarget = false;

                    inputField.textViewport = textAreaGo.GetComponent<RectTransform>();
                    inputField.textComponent = inTxt;
                    inputField.placeholder = phTxt;
                    return go.transform;

                case "scroll":
                    Image scrollBg = go.AddComponent<Image>();
                    scrollBg.color = bgColor;
                    if (scrollBg.color.a <= 0.01f) scrollBg.raycastTarget = false;

                    ScrollRect scrollRect = go.AddComponent<ScrollRect>();
                    bool isVertical = string.IsNullOrEmpty(nodeData.dir) || nodeData.dir.ToLower() == "v";
                    scrollRect.horizontal = !isVertical;
                    scrollRect.vertical = isVertical;

                    GameObject viewportGo = CreateChildRect(go, "Viewport", Vector2.zero, Vector2.one);
                    viewportGo.AddComponent<RectMask2D>();

                    GameObject contentGo = CreateChildRect(viewportGo, "Content", new Vector2(0, 1), new Vector2(0, 1));
                    RectTransform contentRect = contentGo.GetComponent<RectTransform>();
                    contentRect.pivot = new Vector2(0, 1);
                    contentRect.sizeDelta = new Vector2(nodeData.width, nodeData.height);

                    scrollRect.viewport = viewportGo.GetComponent<RectTransform>();
                    scrollRect.content = contentRect;
                    return contentGo.transform;

                case "toggle":
                    Toggle toggle = go.AddComponent<Toggle>();
                    toggle.isOn = nodeData.isChecked;

                    float boxSize = Mathf.Min(nodeData.height, 30f);
                    GameObject tBgGo = CreateChildRect(go, "Background", new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                    RectTransform tBgRect = tBgGo.GetComponent<RectTransform>();
                    tBgRect.sizeDelta = new Vector2(boxSize, boxSize);
                    tBgRect.anchoredPosition = new Vector2(boxSize / 2, 0);
                    Image tBgImg = tBgGo.AddComponent<Image>();
                    tBgImg.color = Color.white;

                    GameObject checkGo = CreateChildRect(tBgGo, "Checkmark", Vector2.zero, Vector2.one);
                    Image checkImg = checkGo.AddComponent<Image>();
                    checkImg.color = Color.black;

                    RectTransform checkRect = checkGo.GetComponent<RectTransform>();
                    checkRect.offsetMin = new Vector2(4, 4);
                    checkRect.offsetMax = new Vector2(-4, -4);

                    GameObject tLblGo = CreateChildRect(go, "Label", Vector2.zero, Vector2.one);
                    RectTransform tLblRect = tLblGo.GetComponent<RectTransform>();
                    tLblRect.offsetMin = new Vector2(boxSize + 10, 0);
                    TextMeshProUGUI tLblTxt = tLblGo.AddComponent<TextMeshProUGUI>();
                    tLblTxt.text = nodeData.text;
                    tLblTxt.color = fontColor;
                    tLblTxt.fontSize = fontSize;
                    tLblTxt.alignment = TextAlignmentOptions.MidlineLeft;
                    tLblTxt.enableWordWrapping = false;

                    toggle.targetGraphic = tBgImg;
                    toggle.graphic = checkImg;
                    return go.transform;

                case "slider":
                    Slider slider = go.AddComponent<Slider>();
                    slider.value = Mathf.Clamp01(nodeData.value);

                    GameObject sBgGo = CreateChildRect(go, "Background", new Vector2(0, 0.25f), new Vector2(1, 0.75f));
                    Image sBgImg = sBgGo.AddComponent<Image>();
                    sBgImg.color = bgColor;

                    GameObject fillAreaGo = CreateChildRect(go, "Fill Area", Vector2.zero, Vector2.one, new Vector2(5, 0), new Vector2(-15, 0));
                    GameObject fillGo = CreateChildRect(fillAreaGo, "Fill", Vector2.zero, Vector2.one);
                    Image fillImg = fillGo.AddComponent<Image>();
                    fillImg.color = fontColor;

                    GameObject handleAreaGo = CreateChildRect(go, "Handle Slide Area", Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0));
                    GameObject handleGo = CreateChildRect(handleAreaGo, "Handle", Vector2.zero, Vector2.one);
                    RectTransform handleRect = handleGo.GetComponent<RectTransform>();
                    handleRect.sizeDelta = new Vector2(20, 0);
                    Image handleImg = handleGo.AddComponent<Image>();
                    handleImg.color = Color.white;

                    slider.targetGraphic = handleImg;
                    slider.fillRect = fillGo.GetComponent<RectTransform>();
                    slider.handleRect = handleRect;
                    return go.transform;

                case "dropdown":
                    Image dBgImg = go.AddComponent<Image>();
                    dBgImg.color = bgColor;
                    TMP_Dropdown dropdown = go.AddComponent<TMP_Dropdown>();

                    GameObject dLblGo = CreateChildRect(go, "Label", Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-30, 0));
                    TextMeshProUGUI dLblTxt = dLblGo.AddComponent<TextMeshProUGUI>();
                    dLblTxt.color = fontColor;
                    dLblTxt.fontSize = fontSize;
                    dLblTxt.alignment = TextAlignmentOptions.MidlineLeft;
                    dLblTxt.enableWordWrapping = false;

                    GameObject arrowGo = CreateChildRect(go, "Arrow", new Vector2(1, 0.5f), new Vector2(1, 0.5f));
                    RectTransform arrowRect = arrowGo.GetComponent<RectTransform>();
                    arrowRect.sizeDelta = new Vector2(20, 20);
                    arrowRect.anchoredPosition = new Vector2(-15, 0);
                    Image arrowImg = arrowGo.AddComponent<Image>();
                    arrowImg.color = fontColor;

                    GameObject templateGo = CreateChildRect(go, "Template", new Vector2(0, 0), new Vector2(1, 0));
                    RectTransform templateRect = templateGo.GetComponent<RectTransform>();
                    templateRect.pivot = new Vector2(0.5f, 1);
                    templateRect.sizeDelta = new Vector2(0, 150);
                    templateRect.anchoredPosition = new Vector2(0, -2);
                    Image tempImg = templateGo.AddComponent<Image>();
                    tempImg.color = Color.white;
                    ScrollRect tempScroll = templateGo.AddComponent<ScrollRect>();
                    tempScroll.horizontal = false;
                    tempScroll.vertical = true;
                    templateGo.SetActive(false);

                    GameObject dViewportGo = CreateChildRect(templateGo, "Viewport", Vector2.zero, Vector2.one);
                    dViewportGo.AddComponent<Image>().color = Color.white;
                    dViewportGo.AddComponent<Mask>();

                    GameObject dContentGo = CreateChildRect(dViewportGo, "Content", new Vector2(0, 1), new Vector2(1, 1));
                    RectTransform dContentRect = dContentGo.GetComponent<RectTransform>();
                    dContentRect.pivot = new Vector2(0.5f, 1);
                    dContentRect.sizeDelta = new Vector2(0, 28);

                    GameObject itemGo = CreateChildRect(dContentGo, "Item", new Vector2(0, 0.5f), new Vector2(1, 0.5f));
                    RectTransform itemRect = itemGo.GetComponent<RectTransform>();
                    itemRect.sizeDelta = new Vector2(0, 28);
                    Toggle itemToggle = itemGo.AddComponent<Toggle>();

                    GameObject itemBgGo = CreateChildRect(itemGo, "Item Background", Vector2.zero, Vector2.one);
                    Image itemBgImg = itemBgGo.AddComponent<Image>();
                    itemBgImg.color = Color.white;

                    GameObject itemCheckGo = CreateChildRect(itemGo, "Item Checkmark", new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                    RectTransform itemCheckRect = itemCheckGo.GetComponent<RectTransform>();
                    itemCheckRect.sizeDelta = new Vector2(20, 20);
                    itemCheckRect.anchoredPosition = new Vector2(15, 0);
                    Image itemCheckImg = itemCheckGo.AddComponent<Image>();
                    itemCheckImg.color = Color.black;

                    GameObject itemLblGo = CreateChildRect(itemGo, "Item Label", Vector2.zero, Vector2.one, new Vector2(30, 0), new Vector2(-10, 0));
                    TextMeshProUGUI itemLblTxt = itemLblGo.AddComponent<TextMeshProUGUI>();
                    itemLblTxt.color = Color.black;
                    itemLblTxt.fontSize = fontSize;
                    itemLblTxt.alignment = TextAlignmentOptions.MidlineLeft;
                    itemLblTxt.enableWordWrapping = false;

                    itemToggle.targetGraphic = itemBgImg;
                    itemToggle.graphic = itemCheckImg;

                    tempScroll.viewport = dViewportGo.GetComponent<RectTransform>();
                    tempScroll.content = dContentRect;

                    dropdown.targetGraphic = dBgImg;
                    dropdown.template = templateRect;
                    dropdown.captionText = dLblTxt;
                    dropdown.itemText = itemLblTxt;

                    if (nodeData.options != null && nodeData.options.Count > 0)
                    {
                        dropdown.ClearOptions();
                        List<TMP_Dropdown.OptionData> optList = new List<TMP_Dropdown.OptionData>();
                        foreach (var opt in nodeData.options) optList.Add(new TMP_Dropdown.OptionData(opt));
                        dropdown.AddOptions(optList);
                    }

                    return go.transform;

                default:
                    Debug.LogWarning($"[HtmlToUGUIBaker] 未知节点类型: {nodeData.type}");
                    return go.transform;
            }
        }

        private TextAlignmentOptions ParseTextAlign(string alignStr)
        {
            if (string.IsNullOrEmpty(alignStr)) return TextAlignmentOptions.Midline;

            switch (alignStr.ToLower())
            {
                case "left":
                case "start":
                    return TextAlignmentOptions.MidlineLeft;
                case "right":
                case "end":
                    return TextAlignmentOptions.MidlineRight;
                case "center":
                default:
                    return TextAlignmentOptions.Midline;
            }
        }

        private GameObject CreateChildRect(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin ?? Vector2.zero;
            rect.offsetMax = offsetMax ?? Vector2.zero;
            return go;
        }

        private Color ParseHexColor(string hex, Color defaultColor)
        {
            if (string.IsNullOrEmpty(hex)) return defaultColor;
            if (ColorUtility.TryParseHtmlString(hex, out Color color)) return color;
            return defaultColor;
        }
    }
}
