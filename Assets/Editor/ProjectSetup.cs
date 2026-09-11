using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RichCoast.EditorTools
{
    /// <summary>
    /// Idempotent project configuration, runnable headlessly
    /// (<c>Tools/setup-project.sh</c> → <c>-executeMethod RichCoast.EditorTools.ProjectSetup.Apply</c>)
    /// or from the menu. Everything here is a project-wide decision recorded in ProjectSettings;
    /// gameplay tuning lives in ScriptableObjects, not here.
    /// </summary>
    public static class ProjectSetup
    {
        const string UrpAssetPath = "Assets/Settings/UniversalRP.asset";
        const string BundleId = "com.richcoast.game";

        [MenuItem("RichCoast/Apply Project Setup")]
        public static void Apply()
        {
            WireRenderPipeline();
            ConfigurePlayer();
            ConfigureAndroid();
            ConfigureIos();
            ConfigurePhysics2D();
            EnsureFolders();
            ImportTmpEssentials();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] applied");
        }

        /// <summary>The headless entry point: settings, then the data assets + Main scene.</summary>
        public static void ApplyAndBuild()
        {
            Apply();
            SceneBuilder.BuildAll();
        }

        /// <summary>
        /// TextMeshPro needs its "Essential Resources" (default font + settings + shaders) unpacked
        /// into Assets/. The editor's importer is async and never lands in a -quit batch run, so
        /// <c>Tools/import-tmp-essentials.py</c> unpacks the .unitypackage directly; this only
        /// verifies (and, in an open editor, falls back to the interactive importer).
        /// </summary>
        static void ImportTmpEssentials()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>("Assets/TextMesh Pro/Resources/TMP Settings.asset") != null)
            {
                Debug.Log("[ProjectSetup] TextMeshPro essential resources present");
                return;
            }
            if (Application.isBatchMode)
            {
                Debug.LogError("[ProjectSetup] TextMeshPro essentials missing — run Tools/import-tmp-essentials.py");
                return;
            }
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            Debug.Log("[ProjectSetup] importing TextMeshPro essential resources (async)");
        }

        static void WireRenderPipeline()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urp == null)
            {
                Debug.LogError($"[ProjectSetup] URP asset missing at {UrpAssetPath}");
                return;
            }
            GraphicsSettings.defaultRenderPipeline = urp;
            int active = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(active, false);
            Debug.Log("[ProjectSetup] URP (2D renderer) set as default + every quality level");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "RichCoast";
            PlayerSettings.productName = "RichCoast";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            Debug.Log("[ProjectSetup] player: portrait-only, linear colour, bundle id set");
        }

        static void ConfigureAndroid()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            Debug.Log("[ProjectSetup] android: IL2CPP, ARM64, minSdk 26");
        }

        static void ConfigureIos()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;
        }

        /// <summary>
        /// Box2D defaults tuned for a stacked ball puzzle: more solver iterations so piles of
        /// circles settle without jitter, and a slightly faster fixed step for touch responsiveness.
        /// </summary>
        static void ConfigurePhysics2D()
        {
            Physics2D.velocityIterations = 12;
            Physics2D.positionIterations = 6;
            Physics2D.gravity = new Vector2(0f, -9.81f);
            Time.fixedDeltaTime = 1f / 60f;
            Debug.Log("[ProjectSetup] physics2D: 12/6 iterations, 60Hz fixed step");
        }

        static void EnsureFolders()
        {
            foreach (var dir in new[] { "Assets/Scenes", "Assets/Game", "Assets/Tests" })
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    var parent = Path.GetDirectoryName(dir)?.Replace('\\', '/');
                    AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
                }
            }
        }

        /// <summary>
        /// Switches the active build target to Android (a reimport; do it early, once).
        /// Kept separate from <see cref="Apply"/> so the cheap settings pass can be re-run freely.
        /// </summary>
        [MenuItem("RichCoast/Switch Active Target To Android")]
        public static void SwitchToAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                Debug.Log("[ProjectSetup] already targeting Android");
                return;
            }
            var ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log($"[ProjectSetup] switch to Android: {(ok ? "ok" : "FAILED (module installed?)")}");
        }
    }
}
