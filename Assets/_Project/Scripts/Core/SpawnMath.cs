using UnityEngine;

namespace EJR.Game.Core
{
    public static class SpawnMath
    {
        public static float CalculateSpawnInterval(float elapsedSeconds, float initialInterval, float minimumInterval, float rampSeconds)
        {
            if (rampSeconds <= 0f)
            {
                return minimumInterval;
            }

            var t = Mathf.Clamp01(elapsedSeconds / rampSeconds);
            return Mathf.Lerp(initialInterval, minimumInterval, t);
        }
    }
}
