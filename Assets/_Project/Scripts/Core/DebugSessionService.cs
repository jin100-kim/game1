using System;

namespace EJR.Game.Core
{
    public static class DebugSessionService
    {
        private const string UnlockCode = "admin";
        private static string s_inputBuffer = string.Empty;

        public static event Action<bool> Changed;

        public static bool IsUnlocked { get; private set; }
        public static bool IsOverlayOpen { get; private set; }
        public static bool IsMonsterLabTimePaused { get; private set; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            IsUnlocked = false;
            IsOverlayOpen = false;
            IsMonsterLabTimePaused = false;
            s_inputBuffer = string.Empty;
            Changed = null;
        }

        public static bool CaptureTypedInput(string typed)
        {
            if (string.IsNullOrEmpty(typed) || IsUnlocked)
            {
                return false;
            }

            for (var i = 0; i < typed.Length; i++)
            {
                var character = typed[i];
                if (character == '\b')
                {
                    if (s_inputBuffer.Length > 0)
                    {
                        s_inputBuffer = s_inputBuffer.Substring(0, s_inputBuffer.Length - 1);
                    }

                    continue;
                }

                if (!char.IsLetter(character))
                {
                    if (!char.IsWhiteSpace(character))
                    {
                        s_inputBuffer = string.Empty;
                    }

                    continue;
                }

                s_inputBuffer += char.ToLowerInvariant(character);
                if (s_inputBuffer.Length > UnlockCode.Length)
                {
                    s_inputBuffer = s_inputBuffer.Substring(s_inputBuffer.Length - UnlockCode.Length);
                }

                if (!string.Equals(s_inputBuffer, UnlockCode, StringComparison.Ordinal))
                {
                    continue;
                }

                Unlock();
                s_inputBuffer = string.Empty;
                return true;
            }

            return false;
        }

        public static void Unlock()
        {
            if (IsUnlocked)
            {
                return;
            }

            IsUnlocked = true;
            Changed?.Invoke(true);
        }

        public static void ToggleOverlay()
        {
            SetOverlayOpen(!IsOverlayOpen);
        }

        public static void SetOverlayOpen(bool open)
        {
            IsOverlayOpen = open;
        }

        public static void SetMonsterLabTimePaused(bool paused)
        {
            IsMonsterLabTimePaused = paused;
        }
    }
}
