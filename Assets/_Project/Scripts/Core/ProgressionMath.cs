using UnityEngine;

namespace EJR.Game.Core
{
    public static class ProgressionMath
    {
        public static int RequiredExperienceForLevel(int level)
        {
            level = Mathf.Max(level, 1);
            return 5 + ((level - 1) * 3);
        }
    }
}
