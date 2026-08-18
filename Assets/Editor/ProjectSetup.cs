using System.IO;
using RichCoast.Core;
using RichCoast.Data;
using RichCoast.Gameplay;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RichCoast.EditorTools
{
    /// <summary>
    /// Reproducible project setup: the low-end player/quality settings, the tuning assets and the
    /// game scene are all generated from code rather than hand-edited, so the whole project can be
    /// rebuilt from a clean checkout and a CI machine ends up byte-identical to a desk.
    ///
    /// Run from the menu, or headlessly:
    ///   Unity -batchmode -quit -projectPath . -executeMethod RichCoast.EditorTools.ProjectSetup.SetUpAll
    /// </summary>
    public static class ProjectSetup
    {
        private const string DataDir = "Assets/Game/Data";
        private const string ScenesDir = "Assets/Game/Scenes";
        private const string ProgressionAssetPath = DataDir + "/ProgressionConfig.asset";
        private const string TierTableAssetPath = DataDir + "/BallTierTable.asset";
        private const string GameScenePath = ScenesDir + "/Game.unity";
        private const string LegacyScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Rich Coast/Set Up Project")]
        public static void SetUpAll()
        {
            ApplyPlayerSettings();
            ApplyPhysicsLayers();
            CreateDataAssets();
            CreateGameScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[RichCoast] Project setup complete.");
        }

        /// <summary>
        /// The binding half of the perf budget (~2019 budget Android, 60 fps). The runtime half —
        /// frame-rate target, physics step — lives in <see cref="GameRoot"/>.
        /// </summary>
        [MenuItem("Rich Coast/Apply Low-End Player Settings")]
        public static void ApplyPlayerSettings()
        {
            var android = NamedBuildTarget.Android;

            // IL2CPP + ARM64: the only combination Google Play accepts, and materially faster than
            // Mono on the low-end CPUs the game targets.
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetApiCompatibilityLevel(android, ApiCompatibilityLevel.NET_Standard);

            // Medium stripping: meaningful IL2CPP binary//memory savings without the link.xml
            // archaeology High demands. Revisit if the APK budget tightens.
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);

            // Vulkan first with a GLES3 fallback covers the target device range.
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
            {
                GraphicsDeviceType.Vulkan,
                GraphicsDeviceType.OpenGLES3,
            });

            // Locked portrait — the whole layout is authored for it.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Draw under the notch and let Layout's safe-area handling deal with the insets.
            PlayerSettings.Android.renderOutsideSafeArea = true;

            // Render straight to the backbuffer and skip the 32-bit display buffer: both are pure
            // memory-bandwidth cost on a budget GPU, and this game needs neither.
            PlayerSettings.Android.blitType = AndroidBlitType.Never;
            PlayerSettings.use32BitDisplayBuffer = false;

            // Backgrounded means the player switched away; burning CPU there just drains battery.
            PlayerSettings.runInBackground = false;

            Debug.Log("[RichCoast] Applied low-end Android player settings.");
        }

        /// <summary>
        /// Name the zone collision layers. Unity has no runtime API for this, so the names are
        /// written into the tag manager here and <see cref="PhysicsLayers"/> holds the numbers the
        /// game uses; a test asserts the two still agree.
        /// </summary>
        [MenuItem("Rich Coast/Apply Physics Layers")]
        public static void ApplyPhysicsLayers()
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0)
            {
                Debug.LogError("[RichCoast] Could not open TagManager.asset.");
                return;
            }

            var tagManager = new SerializedObject(asset[0]);
            var layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(PhysicsLayers.ZoneA).stringValue = PhysicsLayers.ZoneAName;
            layers.GetArrayElementAtIndex(PhysicsLayers.ZoneB).stringValue = PhysicsLayers.ZoneBName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[RichCoast] Named the zone collision layers.");
        }

        /// <summary>Create the tuning assets if they are missing; never overwrite authored edits.</summary>
        [MenuItem("Rich Coast/Create Missing Data Assets")]
        public static void CreateDataAssets()
        {
            EnsureFolder(DataDir);
            CreateIfMissing<ProgressionConfigSO>(ProgressionAssetPath);
            CreateIfMissing<BallTierTableSO>(TierTableAssetPath);
        }

        /// <summary>
        /// Build the game scene from scratch: a camera framed on the design world and a
        /// <see cref="GameRoot"/> wired to the tuning assets. Regenerating it is safe — the scene
        /// holds no authored content, only what this method puts there.
        /// </summary>
        [MenuItem("Rich Coast/Rebuild Game Scene")]
        public static void CreateGameScene()
        {
            CreateDataAssets();
            EnsureFolder(ScenesDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.949f, 0.906f, 0.835f); // warm paper backdrop
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            // Design space is y-down with the origin at the world's top-left; the camera looks at
            // the A-phase framing, centred on the visible screen height.
            camera.orthographicSize = Layout.DesignScreenHeight * 0.5f;
            cameraGo.transform.position = new Vector3(Layout.Width * 0.5f, -Layout.DesignScreenHeight * 0.5f, -10f);

            var rootGo = new GameObject("Game Root");
            var root = rootGo.AddComponent<GameRoot>();

            var so = new SerializedObject(root);
            so.FindProperty("progressionConfig").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ProgressionConfigSO>(ProgressionAssetPath);
            so.FindProperty("tierTable").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<BallTierTableSO>(TierTableAssetPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, GameScenePath);
            SetBuildScenes(GameScenePath);

            // The URP template's placeholder scene has no content of ours and would otherwise sit
            // in the project forever as a second, misleading entry point.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LegacyScenePath) != null)
            {
                AssetDatabase.DeleteAsset(LegacyScenePath);
                Debug.Log($"[RichCoast] Removed {LegacyScenePath}.");
            }

            Debug.Log($"[RichCoast] Rebuilt {GameScenePath}.");
        }

        /// <summary>
        /// Open the game scene and start playing. Exists so a run can be launched from a terminal
        /// (`-executeMethod RichCoast.EditorTools.ProjectSetup.PlayGame`) as well as from the menu.
        /// </summary>
        [MenuItem("Rich Coast/Play Game")]
        public static void PlayGame()
        {
            if (EditorApplication.isPlaying) return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Make the given scene the one and only scene in the build.</summary>
        private static void SetBuildScenes(string scenePath)
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        }

        private static void CreateIfMissing<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"[RichCoast] Created {path}.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
