using System.Collections.Generic;
using System.IO;
using EJR.Game.Gameplay;
using EJR.Game.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EJR.Game.Editor
{
    public static class MultiplayerAssetBuilder
    {
        private const string PlayerPrefabPath = "Assets/Resources/Prefabs/MultiplayerPlayer.prefab";
        private const string SharedEnemyPrefabPath = "Assets/Resources/Prefabs/MultiplayerSharedEnemy.prefab";
        private const string SharedProjectilePrefabPath = "Assets/Resources/Prefabs/MultiplayerSharedProjectile.prefab";
        private const string SharedExperienceOrbPrefabPath = "Assets/Resources/Prefabs/MultiplayerSharedExperienceOrb.prefab";
        private const string MultiplayerScenePath = "Assets/Scenes/MultiplayerScene.unity";
        private const string MenuPath = "Tools/Multiplayer/Generate Multiplayer Assets";

        [MenuItem(MenuPath)]
        public static void GenerateAssets()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Prefabs");
            EnsureFolder("Assets/Scenes");

            CreateOrUpdatePlayerPrefab();
            CreateOrUpdateSharedEnemyPrefab();
            CreateOrUpdateSharedProjectilePrefab();
            CreateOrUpdateSharedExperienceOrbPrefab();
            CreateOrUpdateMultiplayerScene();
            AddSceneToBuildSettings(MultiplayerScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Multiplayer assets generated.");
        }

        private static void CreateOrUpdatePlayerPrefab()
        {
            var root = new GameObject("MultiplayerPlayer");
            try
            {
                root.AddComponent<SpriteRenderer>();
                root.AddComponent<NetworkObject>();

                var networkTransform = root.AddComponent<NetworkTransform>();
                networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
                networkTransform.SyncPositionX = true;
                networkTransform.SyncPositionY = true;
                networkTransform.SyncPositionZ = false;
                networkTransform.SyncRotAngleX = false;
                networkTransform.SyncRotAngleY = false;
                networkTransform.SyncRotAngleZ = false;
                networkTransform.SyncScaleX = false;
                networkTransform.SyncScaleY = false;
                networkTransform.SyncScaleZ = false;
                networkTransform.Interpolate = true;

                root.AddComponent<PlayerMover>();
                root.AddComponent<PlayerSpriteAnimator>();
                root.AddComponent<MultiplayerPlayerActor>();
                root.AddComponent<MultiplayerPlayerCombatant>();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateOrUpdateSharedEnemyPrefab()
        {
            var root = new GameObject("MultiplayerSharedEnemy");
            try
            {
                root.AddComponent<NetworkObject>();

                var networkTransform = root.AddComponent<NetworkTransform>();
                networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
                networkTransform.SyncPositionX = true;
                networkTransform.SyncPositionY = true;
                networkTransform.SyncPositionZ = false;
                networkTransform.SyncRotAngleX = false;
                networkTransform.SyncRotAngleY = false;
                networkTransform.SyncRotAngleZ = false;
                networkTransform.SyncScaleX = false;
                networkTransform.SyncScaleY = false;
                networkTransform.SyncScaleZ = false;
                networkTransform.Interpolate = true;

                root.AddComponent<EnemyController>();
                root.AddComponent<MultiplayerSharedEnemyActor>();

                PrefabUtility.SaveAsPrefabAsset(root, SharedEnemyPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateOrUpdateSharedProjectilePrefab()
        {
            var root = new GameObject("MultiplayerSharedProjectile");
            try
            {
                root.AddComponent<SpriteRenderer>();
                root.AddComponent<NetworkObject>();

                var networkTransform = root.AddComponent<NetworkTransform>();
                networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
                networkTransform.SyncPositionX = true;
                networkTransform.SyncPositionY = true;
                networkTransform.SyncPositionZ = false;
                networkTransform.SyncRotAngleX = false;
                networkTransform.SyncRotAngleY = false;
                networkTransform.SyncRotAngleZ = false;
                networkTransform.SyncScaleX = false;
                networkTransform.SyncScaleY = false;
                networkTransform.SyncScaleZ = false;
                networkTransform.Interpolate = true;

                root.AddComponent<MultiplayerSharedProjectileActor>();

                PrefabUtility.SaveAsPrefabAsset(root, SharedProjectilePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateOrUpdateSharedExperienceOrbPrefab()
        {
            var root = new GameObject("MultiplayerSharedExperienceOrb");
            try
            {
                root.AddComponent<SpriteRenderer>();
                root.AddComponent<NetworkObject>();

                var networkTransform = root.AddComponent<NetworkTransform>();
                networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
                networkTransform.SyncPositionX = true;
                networkTransform.SyncPositionY = true;
                networkTransform.SyncPositionZ = false;
                networkTransform.SyncRotAngleX = false;
                networkTransform.SyncRotAngleY = false;
                networkTransform.SyncRotAngleZ = false;
                networkTransform.SyncScaleX = false;
                networkTransform.SyncScaleY = false;
                networkTransform.SyncScaleZ = false;
                networkTransform.Interpolate = true;

                root.AddComponent<MultiplayerSharedExperienceOrbActor>();

                PrefabUtility.SaveAsPrefabAsset(root, SharedExperienceOrbPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateOrUpdateMultiplayerScene()
        {
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            try
            {
                var root = new GameObject("MultiplayerSceneRoot");
                root.AddComponent<NetworkObject>();
                root.AddComponent<MultiplayerCoopController>();
                root.AddComponent<MultiplayerGameController>();
                EditorSceneManager.SaveScene(scene, MultiplayerScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previousScene.IsValid() && !string.IsNullOrWhiteSpace(previousScene.path))
                {
                    EditorSceneManager.OpenScene(previousScene.path, OpenSceneMode.Single);
                }
            }
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var existingScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (var i = 0; i < existingScenes.Count; i++)
            {
                if (existingScenes[i].path == scenePath)
                {
                    existingScenes[i] = new EditorBuildSettingsScene(scenePath, true);
                    EditorBuildSettings.scenes = existingScenes.ToArray();
                    return;
                }
            }

            existingScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = existingScenes.ToArray();
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            Directory.CreateDirectory(fullPath);
            AssetDatabase.Refresh();
        }
    }
}
