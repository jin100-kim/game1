using UnityEngine;

namespace EJR.Game.Core
{
    public static class ProgressionMath
    {
        public static int RequiredExperienceForLevel(int level)
        {
            level = Mathf.Max(level, 1);
            return 6 + ((level - 1) * 4);
        }
    }
}
