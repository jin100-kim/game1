using UnityEngine;

namespace EJR.Game.UI
{
    internal static class RuntimeFontProvider
    {
        private static Font s_defaultFont;

        public static Font GetDefaultFont()
        {
            if (s_defaultFont != null)
            {
                return s_defaultFont;
            }

            try
            {
                s_defaultFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "Arial Unicode MS", "Arial" },
                    16);
            }
            catch
            {
                s_defaultFont = null;
            }

            s_defaultFont ??= Resources.GetBuiltinResource<Font>("Arial.ttf");
            return s_defaultFont;
        }
    }
}
