#if BUQI_HEADLESS
using System.Globalization;

namespace GameFramework
{
    internal static class Utility
    {
        internal static class Text
        {
            public static string Format(string format, params object[] arguments)
            {
                return string.Format(CultureInfo.InvariantCulture, format, arguments);
            }
        }
    }
}
#endif
