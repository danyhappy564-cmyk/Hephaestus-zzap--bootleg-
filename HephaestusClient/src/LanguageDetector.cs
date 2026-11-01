using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HephaestusClient
{
    /// <summary>
    /// Finds the language the game is using and the Windows display language.
    /// The game is read by reflection (no hard reference to Assembly-CSharp), so a game update that renames
    /// these members only means "game language unknown" → Windows language, never a crash.
    /// </summary>
    internal static class LanguageDetector
    {
        private static bool _resolved;
        private static FieldInfo _instanceField;
        private static FieldInfo _appliedCultureField;

        /// <summary>
        /// Language the game has actually applied ("kr", "en", ...), or null while the game hasn't applied one yet.
        /// Uses EFT.LocalizationManager._currentApplicationCulture: empty until the game applies the language from
        /// its settings (the public Culture property is "en" by default even before that, so it can't be trusted).
        /// Reads the static _instance field instead of the Instance property, which would create the manager early.
        /// </summary>
        public static string GameCulture()
        {
            try
            {
                if (!_resolved)
                {
                    _resolved = true;
                    var type = Type.GetType("EFT.LocalizationManager, Assembly-CSharp", false);
                    if (type != null)
                    {
                        _instanceField = type.GetField("_instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        _appliedCultureField = type.GetField("_currentApplicationCulture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                }

                if (_instanceField == null || _appliedCultureField == null)
                {
                    return null;
                }

                var instance = _instanceField.GetValue(null);
                if (instance == null)
                {
                    return null;
                }

                var culture = _appliedCultureField.GetValue(instance) as string;
                return string.IsNullOrEmpty(culture) ? null : culture;
            }
            catch
            {
                return null;
            }
        }

        private static bool? _windowsKorean;

        /// <summary>Windows display language is Korean (checked once).</summary>
        public static bool WindowsKorean()
        {
            if (_windowsKorean.HasValue)
            {
                return _windowsKorean.Value;
            }

            bool korean;
            try
            {
                // LANGID: low 10 bits = primary language, 0x12 = Korean
                korean = (GetUserDefaultUILanguage() & 0x3FF) == 0x12;
            }
            catch
            {
                try
                {
                    korean = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko"
                             || CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ko";
                }
                catch
                {
                    korean = false;
                }
            }

            _windowsKorean = korean;
            return korean;
        }

        [DllImport("kernel32.dll")]
        private static extern ushort GetUserDefaultUILanguage();
    }
}
