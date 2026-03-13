using EJR.Game.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EJR.Game.Core
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRunController()
        {
            if (!ShouldBootstrapGameplayScene())
            {
                return;
            }

            if (Object.FindFirstObjectByType<RunStateController>() != null)
            {
                return;
            }

            var root = new GameObject("GameRoot");
            root.AddComponent<RunStateController>();
        }

        private static bool ShouldBootstrapGameplayScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                return false;
            }

            return Object.FindFirstObjectByType<GameplaySceneMarker>() != null;
        }
    }
}
