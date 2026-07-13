/*
┌────────────────────────────┐
│　Description: UWR辅助类
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: UnityWebRequestHelper
└──────────────┘
*/


using System;
using UnityEngine.Networking;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Lin.Runtime.Helper
{
    public static class UnityWebRequestHelper
    {
        /// <summary>
        /// 获取字符串
        /// </summary>
        /// <returns>响应结果的字符串 当请求失败时为Null</returns>
        /// <exception cref="ArgumentNullException">url为空</exception>
        public static async UniTask<string> GetString(string url) => await Get(url) as string;

        /// <summary>
        /// 获取数据流
        /// </summary>
        /// <returns>响应结果的数据流 当请求失败时为Null</returns>
        /// <exception cref="ArgumentNullException">url为空</exception>
        public static async UniTask<byte[]> GetBytes(string url) => await Get(url, ResultType.Bytes) as byte[];

        /// <summary>
        /// 获取AssetBundle
        /// </summary>
        /// <returns>响应下载的AssetBundle 当请求失败时为Null</returns>
        /// <exception cref="ArgumentNullException">url为空</exception>
        public static async UniTask<AssetBundle> GetAssetBundle(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException("url");

            using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(url))
            {
                try
                {
                    await request.SendWebRequest();
                    return (request.downloadHandler as DownloadHandlerAssetBundle).assetBundle;
                }
                catch (Exception e)
                {
                    Debug.LogError($"对 {url} 请求失败。\n{e.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// 获取字符串
        /// </summary>
        /// <param name="url"></param>
        /// <returns>响应结果的字符串 当请求失败时为Null</returns>
        /// <exception cref="ArgumentNullException">url为空</exception>
        public static async UniTask<string> Post4String(string url, WWWForm form) => await Post(url, form, ResultType.String) as string;

        /// <summary>
        /// 获取数据流
        /// </summary>
        /// <returns>响应结果的数据流 当请求失败时为Null</returns>
        /// <exception cref="ArgumentNullException">url为空</exception>
        public static async UniTask<byte[]> Post4SBytes(string url, WWWForm form) => await Post(url, form, ResultType.Bytes) as byte[];

        enum ResultType
        {
            String,
            Bytes
        }

        //Get重复部分代码
        static async UniTask<object> Get(string url, ResultType type = ResultType.String)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException("url");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                try
                {
                    await request.SendWebRequest();
                    var handler = request.downloadHandler;
                    return type == ResultType.String ? handler.text : handler.data;
                }
                catch (Exception e)
                {
                    Debug.LogError($"对 {url} 请求失败。\n{e.Message}");
                    return null;
                }
            }
        }

        //Post重复部分代码
        static async UniTask<object> Post(string url, WWWForm form, ResultType type = ResultType.String)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException("url");

            if (form is null)
                throw new ArgumentNullException("form");

            using (UnityWebRequest request = UnityWebRequest.Post(url, form))
            {
                try
                {
                    await request.SendWebRequest();
                    var handler = request.downloadHandler;
                    return type == ResultType.String ? handler.text : handler.data;
                }
                catch (Exception e)
                {
                    Debug.LogError($"对 {url} 请求失败。\n{e.Message}");
                    return null;
                }
            }
        }
    }
}