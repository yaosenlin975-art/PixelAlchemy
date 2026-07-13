using Cysharp.Threading.Tasks;
using Lin.Runtime.Helper;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.IO;

namespace Lin.Runtime.Notice
{
    public class WeixinWorkNoticer : NoticerBase
    {
        protected override string uri => "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=";
        protected string uploadUri => "https://qyapi.weixin.qq.com/cgi-bin/webhook/upload_media?key=";

        private static WeixinWorkNoticer instance;
        public static WeixinWorkNoticer GetInstance()
        {
            instance ??= new WeixinWorkNoticer();
            return instance;
        }

        public async override UniTask Message(string message)
        {
#if TEST || UNITY_EDITOR
            var config = GlobalConfig_SO.GetInstance();
            var postUri = uri + config.wxToken;
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
            this.Debug(request.downloadHandler.text);
#endif
            await UniTask.CompletedTask;
        }

        public async UniTask File(string filePath)
        {
#if TEST || UNITY_EDITOR
            if (!System.IO.File.Exists(filePath))
                return;

            //上传
            var config = GlobalConfig_SO.GetInstance();
            var postUri = uploadUri + config.wxToken + "&type=file";

            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("media", System.IO.File.ReadAllBytes(filePath), Path.GetFileName(filePath), "application/octet-stream")
            };

            using var uploadRequest = UnityWebRequest.Post(postUri, form);
            await uploadRequest.SendWebRequest();

            if (uploadRequest.result != UnityWebRequest.Result.Success)
            {
                this.Error($"文件上传失败：{uploadRequest.error}");
                return;
            }

            var response = JObject.Parse(uploadRequest.downloadHandler.text);
            if (response["errcode"].ToObject<int>() != 0)
            {
                this.Error($"文件上传失败：{uploadRequest.error}");
                return;
            }
            System.IO.File.Delete(filePath);

            //通知
            var mediaID = response["media_id"]?.ToString();
            postUri = uri + config.wxToken;
            var requestData = new JObject
            {
                ["msgtype"] = "file",
                ["file"] = new JObject
                {
                    ["media_id"] = mediaID
                }
            };

            using var sendRequest = UnityWebRequest.Post(postUri, requestData.ToString(), "application/json");
            await sendRequest.SendWebRequest();
#endif
            await UniTask.CompletedTask;
        }
    }
}