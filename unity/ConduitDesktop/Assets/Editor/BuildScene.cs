// Creates the playable scene from the CLI (no manual Editor steps). Invoke:
//   Unity -batchmode -quit -projectPath <proj> -executeMethod Conduit.Editor.BuildScene.Create
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Conduit.Desktop;

namespace Conduit.Editor
{
    public static class BuildScene
    {
        [MenuItem("Conduit/Create Duel Scene")]
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.10f, 0.13f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            var duelGo = new GameObject("ConduitDuel");
            duelGo.AddComponent<DuelBootstrap>();

            Directory.CreateDirectory("Assets/Scenes");
            const string path = "Assets/Scenes/Duel.unity";
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            AssetDatabase.SaveAssets();

            // proof: log the saved scene's contents
            foreach (var go in scene.GetRootGameObjects())
            {
                var comps = string.Join(", ", System.Array.ConvertAll(go.GetComponents<Component>(), c => c ? c.GetType().Name : "null"));
                Debug.Log($"CONDUIT_SCENE_OBJ {go.name}: {comps}");
            }
            Debug.Log($"CONDUIT_SCENE saved {path}; buildScenes={EditorBuildSettings.scenes.Length}");
        }
    }
}
