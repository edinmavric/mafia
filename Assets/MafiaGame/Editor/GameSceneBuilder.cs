using System.Collections.Generic;
using System.IO;
using MafiaGame.Presentation.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MafiaGame.EditorTools
{
    /// <summary>
    /// Creates the match scene from the menu instead of by hand. Authoring a scene file by hand is
    /// error-prone and unreviewable, so the scene is produced by Unity's own serializer and then
    /// registered in Build Settings — Netcode refuses to load a scene that is not in that list.
    ///
    /// The scene is intentionally almost empty: one object with <see cref="MatchEnvironment"/>, which
    /// builds the placeholder floor/table/seats at runtime. No camera and no light, because the scene
    /// is loaded additively over the lobby scene which already has both.
    ///
    /// Running it again is safe and is the way to repair an existing scene: it never replaces the
    /// scene file, it only fills in what is missing (the placeholder material, the Build Settings
    /// entry). Once the owner has put their own content in the scene, that content is left alone.
    /// </summary>
    public static class GameSceneBuilder
    {
        public const string GameScenePath = "Assets/MafiaGame/Content/Scenes/Game.unity";

        /// <summary>
        /// A real material asset, not a runtime-created one: primitives are born with the built-in
        /// pipeline's magenta-in-URP material, and a player has no Editor-only default to fall back
        /// on. Shipping the asset also guarantees its shader is in the build.
        /// </summary>
        private const string MaterialPath = "Assets/MafiaGame/Content/Materials/Placeholder.mat";

        private const string LitShaderName = "Universal Render Pipeline/Lit";

        [MenuItem("MafiaGame/Create Game scene", priority = 40)]
        public static void CreateGameScene()
        {
            Material material = EnsureMaterial();
            if (material == null)
            {
                return;
            }

            if (!File.Exists(GameScenePath) && !CreateSceneFile())
            {
                return;
            }

            RepairScene(material);
            RegisterInBuildSettings();
            Debug.Log($"[GameSceneBuilder] {GameScenePath} is ready and registered in Build Settings.");
        }

        /// <summary>Creates the scene with a single environment object. Returns false on failure.</summary>
        private static bool CreateSceneFile()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GameScenePath));

            // Additive so the scene the owner is working in is not closed under them.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.MoveGameObjectToScene(
                new GameObject("MatchEnvironment", typeof(MatchEnvironment)), scene);
            SceneManager.MoveGameObjectToScene(
                new GameObject("MatchScreen", typeof(MatchScreenView)), scene);

            if (!EditorSceneManager.SaveScene(scene, GameScenePath))
            {
                Debug.LogError($"[GameSceneBuilder] Could not save {GameScenePath}.");
                return false;
            }

            EditorSceneManager.CloseScene(scene, removeScene: true);
            AssetDatabase.Refresh();
            return true;
        }

        /// <summary>
        /// Fills in whatever an existing scene is missing: the placeholder material on the
        /// environment, and the match screen itself. Run every time so a scene created by an earlier
        /// version is brought up to date instead of having to be deleted and rebuilt by hand.
        /// Anything the owner has added is left alone.
        /// </summary>
        private static void RepairScene(Material material)
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            bool changed = false;

            MatchEnvironment environment = Find<MatchEnvironment>(scene);
            if (environment == null)
            {
                environment = NewObject<MatchEnvironment>(scene, "MatchEnvironment");
                changed = true;
            }

            var serialized = new SerializedObject(environment);
            SerializedProperty property = serialized.FindProperty("_partMaterial");
            if (property != null && property.objectReferenceValue != material)
            {
                property.objectReferenceValue = material;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (Find<MatchScreenView>(scene) == null)
            {
                NewObject<MatchScreenView>(scene, "MatchScreen");
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            EditorSceneManager.CloseScene(scene, removeScene: true);
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(includeInactive: true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static T NewObject<T>(Scene scene, string name) where T : Component
        {
            var go = new GameObject(name, typeof(T));
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.GetComponent<T>();
        }

        private static Material EnsureMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Shader lit = Shader.Find(LitShaderName);
            if (lit == null)
            {
                Debug.LogError(
                    $"[GameSceneBuilder] Shader '{LitShaderName}' not found. Is this project on URP?");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            var material = new Material(lit) { name = "Placeholder" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// Adds the scene to Build Settings if it is missing. Netcode's scene management only accepts
        /// scenes from that list, so a scene created but not registered would fail at match start.
        /// </summary>
        private static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene existing in scenes)
            {
                if (existing.path == GameScenePath)
                {
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(GameScenePath, enabled: true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
