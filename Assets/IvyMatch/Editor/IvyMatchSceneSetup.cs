using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IvyMatch.EditorTools
{
    /// <summary>
    /// Ensures Assets/Scenes/IvyMatch.unity exists and is the first scene in Build Settings,
    /// so Play and Android/iOS builds work right after opening the project.
    /// The scene only holds a camera; the dashboard itself is built by IvyMatchBootstrap at runtime.
    /// Menu: IvyMatch > Setup Scene (re-runs it), IvyMatch > Open Scene.
    /// </summary>
    [InitializeOnLoad]
    static class IvyMatchSceneSetup
    {
        const string ScenePath = "Assets/Scenes/IvyMatch.unity";
        const string SessionKey = "IvyMatch.SceneSetupDone";

        static IvyMatchSceneSetup()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += () => Setup(openIfIdle: true);
        }

        [MenuItem("IvyMatch/Setup Scene")]
        static void SetupMenu() => Setup(openIfIdle: true);

        [MenuItem("IvyMatch/Open Scene")]
        static void OpenMenu()
        {
            Setup(openIfIdle: false);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static void Setup(bool openIfIdle)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Scene active = SceneManager.GetActiveScene();
            bool idle = string.IsNullOrEmpty(active.path) && !active.isDirty && SceneManager.sceneCount == 1;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                // Unity cannot add a scene additively next to an unsaved Untitled scene.
                if (string.IsNullOrEmpty(active.path) && !idle)
                {
                    Debug.LogWarning("[IvyMatch] Save or discard the Untitled scene, then run IvyMatch > Setup Scene.");
                    return;
                }
                CreateScene(replaceActive: idle);
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            if (!EditorBuildSettings.scenes.Select(s => s.path).SequenceEqual(scenes.Select(s => s.path))
                || !EditorBuildSettings.scenes[0].enabled)
                EditorBuildSettings.scenes = scenes.ToArray();

            // Swap the empty "Untitled" scene for ours, but never touch a scene the user is working in.
            active = SceneManager.GetActiveScene();
            if (openIfIdle && active.path != ScenePath && string.IsNullOrEmpty(active.path) && !active.isDirty
                && SceneManager.sceneCount == 1)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static void CreateScene(bool replaceActive)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                replaceActive ? NewSceneMode.Single : NewSceneMode.Additive);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0c, 0x0e, 0x12, 0xff);
            cam.cullingMask = 0;
            SceneManager.MoveGameObjectToScene(camGo, scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!replaceActive) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
            Debug.Log("[IvyMatch] Created " + ScenePath + " and added it to Build Settings.");
        }
    }
}
