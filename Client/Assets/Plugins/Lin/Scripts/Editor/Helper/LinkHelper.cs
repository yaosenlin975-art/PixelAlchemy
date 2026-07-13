/*
┌────────────────────────────┐
│　Description：
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：LinkHelper
└──────────────┘
*/
#if HybridCLR
using HybridCLR.Editor.Settings;
using Lin.Runtime.Helper;
using System;
#endif
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using ZLinq;

namespace Lin.Editor.Helper
{
    public static class LinkHelper
    {
        //TODO: 合并时检测程序集是否存在, 不存在则直接从link中删除并打印
        public static void Combine(params string[] links)
        {
            List<string> targets = new List<string>(links)
            {
                EditorConst.LINK_PATH
            };
            var combine = MergeLinkXmlFiles(targets);
            MergeDuplicateAssemblies(combine);
            combine.Save(EditorConst.LINK_PATH);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 检测程序集是否存在，不存在则从列表中删除并打印日志
        /// </summary>
        /// <param name="assemblyElements">程序集元素列表</param>
        private static void CheckAndRemoveNonExistentAssemblies(List<XElement> assemblyElements)
        {
            var toRemove = new List<XElement>();

            foreach (var assembly in assemblyElements)
            {
                var fullnameAttribute = assembly.Attribute("fullname");
                if (fullnameAttribute != null)
                {
                    string assemblyName = fullnameAttribute.Value;

                    // 检查程序集是否存在于项目中
                    var assObject = System.Reflection.Assembly.Load(assemblyName);
                    if (assObject == null)
                    {
                        toRemove.Add(assembly);
                        Debug.LogWarning($"[LinkHelper] 程序集 '{assemblyName}' 不存在于项目中，已从 link.xml 中移除");
                    }
                }
            }

            // 移除不存在的程序集
            foreach (var assembly in toRemove)
            {
                assemblyElements.Remove(assembly);
            }
        }

        //合并多个XML文件
        private static XDocument MergeLinkXmlFiles(IEnumerable<string> filePaths)
        {
            XDocument mergedDocument = new XDocument(new XElement("linker"));
            HashSet<string> uniqueElements = new HashSet<string>();

            foreach (var filePath in filePaths)
            {
                if (File.Exists(filePath))
                {
                    XDocument document = LoadXML(filePath);
                    foreach (var element in document.Root.Elements())
                    {
                        string elementString = element.ToString();
                        if (uniqueElements.Add(elementString))
                            mergedDocument.Root.Add(element);
                    }
                }
                else
                    Debug.LogError($"文件 {filePath} 不存在");
            }

            return mergedDocument;
        }

        private static XDocument LoadXML(string filePath)
        {
            XDocument document = XDocument.Load(filePath);
            //对assembly信息进行处理
            foreach (var assemblyNode in document.Root.Elements("assembly"))
            {
                var fullnameAttribute = assemblyNode.Attribute("fullname");
                if (fullnameAttribute != null)
                {
                    string fullnameValue = fullnameAttribute.Value;
                    int commaIndex = fullnameValue.IndexOf(',');
                    if (commaIndex != -1)
                    {
                        string newFullnameValue = fullnameValue.Substring(0, commaIndex).Trim();
                        fullnameAttribute.Value = newFullnameValue;
                    }
                }
            }
            return document;
        }

        //对重复的assembly节点进行合并
        private static void MergeDuplicateAssemblies(XDocument document)
        {
            var assemblyGroups = document.Root.Elements("assembly")
                .GroupBy(a => a.Attribute("fullname").Value);

            var newAssemblyElements = new List<XElement>();

            foreach (var group in assemblyGroups)
            {
                if (group.Count() > 1)
                {
                    var mergedAssembly = new XElement("assembly", new XAttribute("fullname", group.Key));
                    var typeSet = new HashSet<string>();

                    foreach (var assembly in group)
                    {
                        foreach (var type in assembly.Elements("type"))
                        {
                            string typeString = type.ToString();
                            if (typeSet.Add(typeString))
                            {
                                mergedAssembly.Add(type);
                            }
                        }
                    }

                    newAssemblyElements.Add(mergedAssembly);
                }
                else
                    newAssemblyElements.Add(group.First());
            }

#if HybridCLR
            var hybridSettings = HybridCLRSettings.LoadOrCreate();
            foreach (var hotfix in hybridSettings.hotUpdateAssemblyDefinitions)
            {
                var toRemove = newAssemblyElements.Find(e => e.Attribute("fullname").Value.Equals(hotfix.name));
                if (toRemove is not null)
                    newAssemblyElements.Remove(toRemove);
            }
#endif
            newAssemblyElements.RemoveAll(e => e.Attribute("fullname").Value.Contains("UnityEditor"));

            // 检测程序集是否存在，不存在则从link中删除并打印日志
            CheckAndRemoveNonExistentAssemblies(newAssemblyElements);

            document.Root.Elements("assembly").Remove();
            document.Root.Add(newAssemblyElements);
        }

        //读取link中记录的所有assembly
        public static HashSet<string> GetAssemblies()
        {
            HashSet<string> result = new HashSet<string>();
            if (File.Exists(EditorConst.LINK_PATH))
            {
                XDocument document = XDocument.Load(EditorConst.LINK_PATH);
                foreach (var assemblyNode in document.Root.Elements("assembly"))
                {
                    var fullnameAttribute = assemblyNode.Attribute("fullname");
                    if (fullnameAttribute != null)
                        result.Add(fullnameAttribute.Value);
                }
            }

            return result;
        }

        //记录当前特征
        private const string LINK_MD5_KEY = nameof(LINK_MD5_KEY);

        public static void SaveMD5()
        {
            var md5 = CalculateMD5();
            PrefsHelper.Set(LINK_MD5_KEY, md5);
        }

        private static string CalculateMD5()
        {
            if (File.Exists(EditorConst.LINK_PATH))
                return HashHelper.FileMD5(EditorConst.LINK_PATH);

            return string.Empty;
        }

        //是否应该重新打包App
        public static bool ShouldBuildApplication()
        {
            var calculate = CalculateMD5();
            var archive = PrefsHelper.Get(LINK_MD5_KEY, string.Empty);
            return calculate != archive;
        }
    }
}
