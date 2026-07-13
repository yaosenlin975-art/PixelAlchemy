/*
┌────────────────────────────┐
│　Description：发送至钉钉
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：DingTalkNoticer
└──────────────┘
*/

using Cysharp.Threading.Tasks;
using Lin.Runtime.Helper;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine.Networking;

namespace Lin.Runtime.Notice
{
    public class FeishuNoticer : NoticerBase
    {
        protected override string uri => "https://open.feishu.cn/open-apis/bot/v2/hook/{0}";

        private static FeishuNoticer instance;
        public static FeishuNoticer GetInstance()
        {
            instance ??= new FeishuNoticer();
            return instance;
        }

        public async override UniTask Message(string message)
        {
#if TEST || UNITY_EDITOR
            var config = GlobalConfig_SO.GetInstance();
            var postUri = string.Format(uri, config.feishuToken);
            var requestData = new JObject
            {
                ["msg_type"] = "text",
                ["content"] = new JObject
                {
                    ["text"] = message
                }
            };
            using var request = UnityWebRequest.Post(postUri, requestData.ToString(), "application/json");
            await request.SendWebRequest();
            this.Debug(request.downloadHandler.text);
#else
            await UniTask.CompletedTask;
#endif
        }

        private static string GenerateSign(long timestamp, string secret)
        {
            // 拼接待签名字符串
            string stringToSign = $"{timestamp}\n{secret}";
            byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(stringToSign);
            byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(secret);

            // 计算HMAC-SHA256
            using (var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(messageBytes);
                // Base64编码
                string base64String = Convert.ToBase64String(hashBytes);
                return UnityWebRequest.EscapeURL(base64String);
            }
        }
    }
}