/*
┌────────────────────────────┐
│　Description: Importer拓展方法
└────────────────────────────┘
*/
using Lin.Editor.Asset;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEditor;
using AssetImporter = UnityEditor.AssetImporter;

namespace Lin.Editor.Helper
{
    public static class ImporterHelper
    {
        #region - UserData -

        public static string GetUserData(this AssetImporter self, string key)
        {
            string json = self.userData;
            if (string.IsNullOrEmpty(json))
                return string.Empty;

            try
            {
                JObject jObject = JObject.Parse(json);
                if (jObject.TryGetValue(key, out var result))
                    return result.ToString();
            }
            catch (System.Exception)
            {
                self.userData = string.Empty;
                self.SaveAndReimport();
            }

            return string.Empty;
        }

        public static T GetUserData<T>(this AssetImporter self, string key)
        {
            string json = self.GetUserData(key);
            if (string.IsNullOrEmpty(json))
                return default;

            return JsonConvert.DeserializeObject<T>(json);
        }

        public static void SetUserData<T>(this AssetImporter self, string key, T value, bool saveAndReimport = false) => self.SetUserData(key, CleanJson(JsonConvert.SerializeObject(value)), saveAndReimport);

        public static void SetUserData(this AssetImporter self, string key, string value, bool saveAndReimport = false)
        {
            string json = self.userData;
            JObject jObject;
            try
            {
                jObject = JObject.Parse(json);
            }
            catch (System.Exception)
            {
                jObject = new JObject();
            }
            if (string.IsNullOrEmpty(value))
                jObject.Remove(key);
            else if (jObject.ContainsKey(key))
                jObject[key] = value;
            else
                jObject.Add(key, value);

            self.userData = CleanJson(jObject.ToString());

            if (saveAndReimport)
                self.SaveAndReimport();
        }

        private static string CleanJson(string json)
        {
            return json.Replace(" ", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty).Replace("\t", string.Empty);
        }

        #endregion

        #region - Tag -

        private const string TAGS_KEY = nameof(TAGS_KEY);

        public static void AddUserTag(this AssetImporter self, string tag, bool saveAndReimport = false)
        {
            var tags = self.GetUserData<List<string>>(TAGS_KEY) ?? new List<string>();
            if (tags.Contains(tag))
                return;

            tags.Add(tag);
            self.SetUserData(TAGS_KEY, tags, saveAndReimport);
        }

        public static void RemoveUserTag(this AssetImporter self, string tag, bool saveAndReimport = false)
        {
            var tags = self.GetUserData<List<string>>(TAGS_KEY) ?? new List<string>();
            if (!tags.Contains(tag))
                return;

            tags.Remove(tag);
            self.SetUserData(TAGS_KEY, tags, saveAndReimport);
        }

        public static bool ContainsUserTag(this AssetImporter self, string tag)
        {
            var tags = self.GetUserTags();
            if (tags is null)
                return false;

            return tags.Contains(tag);
        }

        public static List<string> GetUserTags(this AssetImporter self) => self.GetUserData<List<string>>(TAGS_KEY);

        #endregion

        #region - Description -

        public const string ASSET_DESCRIPTION_KEY = nameof(ASSET_DESCRIPTION_KEY);

        public static void SetDescription(this AssetImporter self, AssetSummary summary) => AssetSummaryArchiver.GetInstance().SetDescription(self, summary);

        public static AssetSummary GetDescription(this AssetImporter self) => AssetSummaryArchiver.GetInstance().Get(self);

        public static void RemoveDescription(this AssetImporter self) => AssetSummaryArchiver.GetInstance().RemoveDescription(self);

        #endregion

        public static string GetAssetGUID(this AssetImporter self) => AssetDatabase.AssetPathToGUID(self.assetPath);

        public static AssetImporter FromGUID(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return AssetImporter.GetAtPath(path);
        }

        #region - URI -

        public const string ASSET_URIS_KEY = nameof(ASSET_URIS_KEY);

        public static void SetUri(this AssetImporter self, string key, string uri)
        {
            var map = self.GetUris();
            if (map.ContainsKey(key))
                map[key] = uri;
            else
                map.Add(key, uri);

            self.SetUserData(ASSET_URIS_KEY, JsonConvert.SerializeObject(map), true);
        }

        public static void RemoveUri(this AssetImporter self, string key)
        {
            var map = self.GetUris();
            if (map.Remove(key))
                self.SetUserData(ASSET_URIS_KEY, JsonConvert.SerializeObject(map), true);
        }


        public static Dictionary<string, string> GetUris(this AssetImporter self) => JsonConvert.DeserializeObject<Dictionary<string, string>>(self.GetUserData(ASSET_URIS_KEY)) ?? new Dictionary<string, string>();

        #endregion
    }
}