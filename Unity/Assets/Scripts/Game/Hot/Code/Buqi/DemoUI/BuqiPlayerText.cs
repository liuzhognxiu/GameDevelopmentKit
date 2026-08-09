using System;
using Game.Hot.Buqi.Battle;

namespace Game.Hot.Buqi.DemoUI
{
    public static class BuqiPlayerText
    {
        public static string Localize(string key, string sourceFallback, string defaultText)
        {
            string fallback = Sanitize(sourceFallback, defaultText);
            if (string.IsNullOrWhiteSpace(key))
                return fallback;

            try
            {
                return Sanitize(GameEntry.Localization.GetString(key), fallback);
            }
            catch
            {
                return fallback;
            }
        }

        public static string Sanitize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOf("<NoKey>", StringComparison.Ordinal) >= 0)
                return fallback;
            if (ContainsHan(value) || !ContainsLatin(value))
                return value;
            return fallback;
        }

        public static string Error(string value)
        {
            return Sanitize(value, "操作未完成，请重试。");
        }

        public static string Format(string format, params object[] args)
        {
            return BuqiText.Format(format, args);
        }

        private static bool ContainsHan(string value)
        {
            foreach (char character in value)
            {
                if (character >= '\u3400' && character <= '\u9fff')
                    return true;
            }
            return false;
        }

        private static bool ContainsLatin(string value)
        {
            foreach (char character in value)
            {
                if ((character >= 'A' && character <= 'Z') ||
                    (character >= 'a' && character <= 'z'))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
