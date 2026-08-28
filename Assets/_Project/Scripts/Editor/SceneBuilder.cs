using ServerGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ServerGame.EditorTools
{
    /// <summary>Genera la escena principal. No es imprescindible (GameBootstrap se crea
    /// solo al entrar en Play), pero deja el proyecto listo para compilar una build.</summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        public static string MainScenePath => ScenePath;

        [MenuItem("Server Game/Crear escena principal", false, 0)]
        public static void CreateMainScene()
        {
            if (!EditorUtility.DisplayDialog("Server Game",
                    "Se va a crear (o sobrescribir) la escena:\n" + ScenePath,
                    "Crear", "Cancelar"))
            {
                return;
            }

            CreateMainSceneSilent();

            EditorUtility.DisplayDialog("Server Game",
                "Escena creada y añadida a Build Settings.\n\nPulsa Play para jugar.", "Vale");
        }

        /// <summary>Misma creación de escena, sin diálogos: apta para modo batch y CI.</summary>
        public static void CreateMainSceneSilent()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.043f, 0.055f, 0.078f, 1f);
            camera.orthographic = true;
            camera.cullingMask = 0;
            cameraGo.AddComponent<AudioListener>();

            var bootstrap = new GameObject("[Server Game]");
            bootstrap.AddComponent<GameBootstrap>();

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            AddSceneToBuildSettings();
        }

        [MenuItem("Server Game/Crear asset de configuración", false, 20)]
        public static void CreateConfigAsset()
        {
            const string folder = "Assets/_Project/Settings";
            const string path = folder + "/GameConfig.asset";

            System.IO.Directory.CreateDirectory(folder);

            if (AssetDatabase.LoadAssetAtPath<GameConfig>(path) != null)
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
                EditorGUIUtility.PingObject(Selection.activeObject);
                return;
            }

            var config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != ScenePath) continue;
                scenes[i].enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
