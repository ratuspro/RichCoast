using System.IO;
using RichCoast.App;
using RichCoast.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RichCoast.EditorTools
{
    /// <summary>
    /// Deterministic authoring of the data assets + the one scene the game needs, so the project
    /// can be (re)built headlessly (<c>-executeMethod RichCoast.EditorTools.SceneBuilder.BuildAll</c>)
    /// and nothing depends on hand-edited scene state. Existing tuned assets are never overwritten.
    /// </summary>
    public static class SceneBuilder
    {
        const string DataDir = "Assets/Game/Data";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("RichCoast/Build Data Assets + Main Scene")]
        public static void BuildAll()
        {
            var ladder = EnsureAsset<TierLadderSO>($"{DataDir}/TierLadder.asset");
            var progression = EnsureAsset<ProgressionSO>($"{DataDir}/Progression.asset");
            var feel = EnsureAsset<GameFeelSO>($"{DataDir}/GameFeel.asset");
            BuildMainScene(ladder, progression, feel);
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] data assets + Main scene ready");
        }

        static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"[SceneBuilder] created {path}");
            return asset;
        }

        static void BuildMainScene(TierLadderSO ladder, ProgressionSO progression, GameFeelSO feel)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 12f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            var urpCam = cam.GetUniversalAdditionalCameraData();
            urpCam.renderPostProcessing = false;

            var game = new GameObject("Game");
            var boot = game.AddComponent<GameBootstrap>();
            boot.tierLadder = ladder;
            boot.progression = progression;
            boot.feel = feel;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[SceneBuilder] wrote {ScenePath}");
        }
    }
}
