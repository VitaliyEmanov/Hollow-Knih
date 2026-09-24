#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AshenWick.EditorTools
{
    /// <summary>
    /// Makes the project playable right after opening it:
    /// ensures Assets/Scenes/Main.unity exists, is in Build Settings and is opened.
    /// The game itself is bootstrapped from code (see AshenWick.Boot), so the scene can stay empty.
    /// </summary>
    [InitializeOnLoad]
    public static class AshenWickSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string OpenedKey = "AshenWick.SceneOpenedOnce";

        static AshenWickSetup()
        {
            EditorApplication.delayCall += EnsureScene;
        }

        [MenuItem("Ashen Wick/Open Main Scene")]
        public static void OpenMain()
        {
            EnsureScene();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Ashen Wick/Erase Save Data")]
        public static void EraseSave()
        {
            PlayerPrefs.DeleteKey("AshenWick.Save.v1");
            PlayerPrefs.Save();
            Debug.Log("Ashen Wick: save data erased.");
        }

        static void EnsureScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }

            bool inBuild = false;
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path == ScenePath) inBuild = true;
            if (!inBuild)
            {
                var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }

            if (!SessionState.GetBool(OpenedKey, false))
            {
                SessionState.SetBool(OpenedKey, true);
                var active = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                    EditorSceneManager.OpenScene(ScenePath);
            }

            PlayerSettings.companyName = "Team Raspberry";
            PlayerSettings.productName = "Hollow Knight - Ash";
        }
    }
}
#endif
