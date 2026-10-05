using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEditor.Localization;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Milestone build (WebGL).
    ///
    /// - <see cref="SetLocalizationPreload"/>: marks every string table of "Localization Table" as preloaded.
    ///   Texts read from code use <c>LocText.Now</c>/<c>LocText.Get</c> (non-blocking; <c>GetLocalizedString()</c> throws in WebGL).
    ///   In WebGL the tables are loaded asynchronously - without preloading those calls return the key or an empty
    ///   string on the first frames. With preloading, Localization initialisation waits for the tables.
    /// - <see cref="BuildWebGL"/>: sets the preload flags, then builds all enabled scenes of the Build Settings to
    ///   Builds/WebGL (git-ignored). Switches the active build target to WebGL if it is not already.
    ///   Addressables content (localization tables, locales) is built explicitly for WebGL right before the player:
    ///   the project does not build it with the player, and a catalog left over from the Windows target points to
    ///   StandaloneWindows64 bundles - the browser then finds no locale ("SelectedLocale is null") and every text fails.
    ///
    /// Menu: Tools / Food Production / ...
    /// </summary>
    public static class MilestoneBuild
    {
        private const string TableCollectionName = "Localization Table";
        private const string OutputPath = "Builds/WebGL";

        [MenuItem("Tools/Food Production/Set Localization Preload")]
        public static void SetLocalizationPreload()
        {
            int count = ApplyPreload();
            SetupUi.Dialog("Set Localization Preload",
                count > 0 ? $"{count} Tabellen von \"{TableCollectionName}\" werden jetzt vorgeladen." : $"Tabellen-Sammlung \"{TableCollectionName}\" nicht gefunden.", "OK");
        }

        private const string PendingBuildKey = "fps.milestone.pendingWebGLBuild";

        /// <summary>After the domain reload of a platform switch: continue a requested build.</summary>
        [InitializeOnLoadMethod]
        private static void ResumePendingBuild()
        {
            if (SessionState.GetBool(PendingBuildKey, false))
            {
                EditorApplication.delayCall += () =>
                {
                    if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                    {
                        return; // switch still running - the next reload calls this again
                    }
                    SessionState.SetBool(PendingBuildKey, false);
                    RunBuild();
                };
            }
        }

        /// <summary>
        /// Takes long (first build about an hour) and blocks the editor meanwhile - ask the team before starting it.
        /// </summary>
        [MenuItem("Tools/Food Production/Build WebGL (Milestone)")]
        public static void BuildWebGL()
        {
            ApplyPreload();

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                SetupUi.Dialog("Build WebGL", "WebGL Build Support ist für diese Unity-Version nicht installiert (Unity Hub → Installs → Add modules).", "OK");
                return;
            }

            // Platform switch first (async, with domain reload), the build itself afterwards from delayCall - so the
            // menu / automation command returns at once and the result is logged instead of shown in a modal dialog.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                SessionState.SetBool(PendingBuildKey, true);
                Debug.Log("[Build WebGL] Switching active build target to WebGL - the build starts after the reimport.");
                EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                return;
            }

            // EditorApplication.update instead of delayCall: delayCall is one multicast delegate - if any other
            // subscriber throws, the rest (this build) is silently dropped. That happened here.
            EditorApplication.update += RunBuildOnNextUpdate;
        }

        private static void RunBuildOnNextUpdate()
        {
            EditorApplication.update -= RunBuildOnNextUpdate;
            RunBuild();
        }

        private static void RunBuild()
        {
            Debug.Log("[Build WebGL] Build started.");

            // Localization lives in Addressables - build its content for the active target (WebGL) first.
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult content);
            if (!string.IsNullOrEmpty(content.Error))
            {
                Debug.LogError("[Build WebGL] Addressables content build failed: " + content.Error);
                return;
            }
            Debug.Log($"[Build WebGL] Addressables content built ({content.AssetBundleBuildResults.Count} bundles).");
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            string errors = string.Join("\n", report.steps
                .SelectMany(step => step.messages)
                .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                .Select(m => m.content)
                .Take(10));

            string message = $"Ergebnis: {summary.result}, Dauer {summary.totalTime:mm\\:ss}, Größe {summary.totalSize / (1024f * 1024f):0.0} MB, " +
                             $"Fehler {summary.totalErrors}, Warnungen {summary.totalWarnings}\nAusgabe: {Path.GetFullPath(OutputPath)}" +
                             (errors.Length > 0 ? "\n" + errors : string.Empty);
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[Build WebGL] " + message);
            }
            else
            {
                Debug.LogError("[Build WebGL] " + message);
            }
        }

        private static int ApplyPreload()
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(TableCollectionName);
            if (collection == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var table in collection.StringTables)
            {
                if (!LocalizationEditorSettings.GetPreloadTableFlag(table))
                {
                    LocalizationEditorSettings.SetPreloadTableFlag(table, true, true);
                }
                count++;
            }
            AssetDatabase.SaveAssets();
            return count;
        }
    }
}
