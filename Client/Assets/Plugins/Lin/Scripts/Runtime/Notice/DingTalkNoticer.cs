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
#if TEST || UNITY_EDITOR
using System;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;
#endif

namespace Lin.Runtime.Notice
{
    public class DingTalkNoticer : NoticerBase
    {
        //0:token  1:timestamp   2:sign
        protected override string uri => "https://oapi.dingtalk.com/robot/send?access_token={0}&timestamp={1}&sign={2}";

        private static DingTalkNoticer instance;
        public static DingTalkNoticer GetInstance()
        {
            instance ??= new DingTalkNoticer();
            return instance;
        }

        public override async UniTask Message(string message)
        {
#if TEST || UNITY_EDITOR
            var config = GlobalConfig_SO.GetInstance();

            //计算签名
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string stringToSign = $"{timestamp}\n{config.dingTalkSecret}";
            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(config.dingTalkSecret));
            byte[] signData = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(stringToSign));
            string base64String = Convert.ToBase64String(signData);
            var sign = UnityWebRequest.EscapeURL(base64String);

            string postUri = string.Format(uri, config.dingTalkToken, timestamp, sign);
            var requestData = new JObject
            {
                ["msgtype"] = "text",
                ["text"] = new JObject
                {
                    ["content"] = message
                }
            };
            using var request = UnityWebRequest.Post(postUri, requestData.ToString(), "application/json");
            await request.SendWebRequest();
#else
            await UniTask.CompletedTask;
#endif
        }
    }
}