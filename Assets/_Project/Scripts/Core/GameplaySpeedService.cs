using UnityEngine;

namespace EJR.Game.Core
{
    public static class GameplaySpeedService
    {
        private const float DefaultFixedDeltaTime = 0.02f;
        private const float MinGameplaySpeed = 1f;
        private const float MaxGameplaySpeed = 4f;

        private static float s_gameplaySpeedMultiplier = 1f;

        public static float GameplaySpeedMultiplier => s_gameplaySpeedMultiplier;

        public static void SetGameplaySpeedMultiplier(float multiplier)
        {
            s_gameplaySpeedMultiplier = Mathf.Clamp(multiplier, MinGameplaySpeed, MaxGameplaySpeed);
        }

        public static void ResetGameplaySpeedMultiplier()
        {
            s_gameplaySpeedMultiplier = 1f;
        }

        public static void ApplyGameplayTimeState(bool paused)
        {
            Time.timeScale = paused ? 0f : s_gameplaySpeedMultiplier;
            Time.fixedDeltaTime = DefaultFixedDeltaTime * s_gameplaySpeedMultiplier;
        }

        public static void ApplyMenuTimeState()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = DefaultFixedDeltaTime;
        }
    }
}
