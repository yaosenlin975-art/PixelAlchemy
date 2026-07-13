using UnityEditor;
using System.Linq;
using System.IO;
using System;
using UnityEngine;
using Lin.Runtime.Helper;
using Lin.Runtime.Const;

namespace Lin.Editor.BuildTool
{
    public static class BuildTools
    {
        public static string Build(string directory, out bool isSucceeded)
        {
            BuildPlayerOptions opt = new BuildPlayerOptions();
            try
            {
                IOHelper.InsureExist(directory, false);
                opt.scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray();

                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                {
                    opt.locationPathName = $"{directory}/{ResourceConst.APK_NAME}";
                }
                else if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows)
                {
                    opt.locationPathName = directory;
                }
                opt.target = EditorUserBuildSettings.activeBuildTarget;
                opt.options = BuildOptions.None;

                var result = BuildPipeline.BuildPlayer(opt);
                isSucceeded = result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
                return opt.locationPathName;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                isSucceeded = false;
                return null;
            }
            finally
            {
                var name = opt.locationPathName;
                if (name.EndsWith(".apk"))
                {
                    if (!PrefsHelper.Get(BuildApplicationSettings.INSTALL_TO_SIMULATOR_KEY, false))
                        name = Path.GetDirectoryName(name);
                }
                System.Diagnostics.Process.Start(name);
            }
        }
    }
}