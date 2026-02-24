using EJR.Game.Gameplay;
using UnityEngine;

namespace EJR.Game.Core
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRunController()
        {
            if (Object.FindFirstObjectByType<RunStateController>() != null)
            {
                return;
            }

            var root = new GameObject("GameRoot");
            root.AddComponent<RunStateController>();
        }
    }
}
