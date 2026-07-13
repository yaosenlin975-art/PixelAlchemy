/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using Lin.Runtime.DesignPattern.Singleton;
using Lin.Runtime.Helper;
using Lin.Runtime.Manager;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lin.Runtime.Tool
{
    public class MissingShaderVariantCollector : MonoSingleton<MissingShaderVariantCollector>
    {
        /// <summary>
        /// key: 当前场景路径, value: [key: shader名, value: 缺失的变体组合]
        /// </summary>
        private Dictionary<string, Dictionary<string, List<List<string>>>> missingVariants;

        protected override void Init()
        {
            base.Init();

            missingVariants = PrefsHelper.Get(nameof(missingVariants), () => new Dictionary<string, Dictionary<string, List<List<string>>>>());
            Application.logMessageReceived += OnLogMessageReceived;

            StartCoroutine(AutoSave());
        }

        protected override void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
            base.OnDestroy();
        }

        private IEnumerator AutoSave()
        {
            while (true)
            {
                yield return CoroutineCache.WaitForSeconds(60);
                if (missingVariants != null && missingVariants.Count > 0)
                    PrefsHelper.Set(nameof(missingVariants), missingVariants);
            }
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error)
                return;

            string shaderName = null;
            string keywordsStr = null;

            // Format 1: Shader 'ShaderName' variant 'Keywords' not found
            var match = Regex.Match(condition, @"Shader '(.+)' variant '(.+)' not found");
            if (match.Success)
            {
                shaderName = match.Groups[1].Value;
                keywordsStr = match.Groups[2].Value;
            }
            else
            {
                // Format 2: Shader ShaderName, subshader 0, pass 1, stage all: variant Keywords not found.
                match = Regex.Match(condition, @"Shader (.+), subshader \d+, pass \d+, stage \w+: variant (.+) not found\.");
                if (match.Success)
                {
                    shaderName = match.Groups[1].Value;
                    keywordsStr = match.Groups[2].Value;
                }
            }

            if (!string.IsNullOrEmpty(shaderName))
            {
                var keywords = new List<string>(keywordsStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

                string sceneLocation = SceneManager.GetActiveScene().path;

                if (!missingVariants.TryGetValue(sceneLocation, out var shaderMap))
                {
                    shaderMap = new Dictionary<string, List<List<string>>>();
                    missingVariants[sceneLocation] = shaderMap;
                }

                if (!shaderMap.TryGetValue(shaderName, out var variantList))
                {
                    variantList = new List<List<string>>();
                    shaderMap[shaderName] = variantList;
                }

                // 简单去重：检查是否已经存在相同的变体组合
                bool exists = false;
                foreach (var v in variantList)
                {
                    if (v.Count == keywords.Count)
                    {
                        bool matchKeywords = true;
                        // 假设Unity报错的关键字顺序是固定的，或者我们简单比较内容
                        var set1 = new HashSet<string>(v);
                        foreach (var k in keywords)
                        {
                            if (!set1.Contains(k))
                            {
                                matchKeywords = false;
                                break;
                            }
                        }
                        if (matchKeywords)
                        {
                            exists = true;
                            break;
                        }
                    }
                }

                if (!exists)
                    variantList.Add(keywords);
            }
        }

        public string GetVariantsJson()
        {
            if (missingVariants == null || missingVariants.Count == 0)
                return string.Empty;

            return JsonConvert.SerializeObject(missingVariants);
        }
    }
}
