#if BUQI_HEADLESS
using System.Globalization;
#endif

namespace Game.Hot.Buqi.Battle
{
    /// <summary>
    /// 共享层字符串格式化适配器。
    /// Unity GameHot 端必须使用 GameFramework.Utility.Text.Format；无头端使用区域无关的 string.Format。
    /// </summary>
    public static class BuqiText
    {
        /// <summary>
        /// 以跨端一致的格式生成诊断文本、chainId 和稳定原因字符串。
        /// 当前最多支持七个参数，正好覆盖 GameFramework.Utility.Text 的泛型重载范围。
        /// </summary>
        public static string Format(string format, params object[] arguments)
        {
#if BUQI_HEADLESS
            return string.Format(CultureInfo.InvariantCulture, format, arguments);
#else
            switch (arguments.Length)
            {
                case 0:
                    return format;
                case 1:
                    return GameFramework.Utility.Text.Format(format, arguments[0]);
                case 2:
                    return GameFramework.Utility.Text.Format(format, arguments[0], arguments[1]);
                case 3:
                    return GameFramework.Utility.Text.Format(format, arguments[0], arguments[1], arguments[2]);
                case 4:
                    return GameFramework.Utility.Text.Format(
                        format, arguments[0], arguments[1], arguments[2], arguments[3]);
                case 5:
                    return GameFramework.Utility.Text.Format(
                        format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4]);
                case 6:
                    return GameFramework.Utility.Text.Format(
                        format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5]);
                case 7:
                    return GameFramework.Utility.Text.Format(
                        format, arguments[0], arguments[1], arguments[2], arguments[3],
                        arguments[4], arguments[5], arguments[6]);
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(arguments), "BuqiText supports at most seven format arguments.");
            }
#endif
        }
    }
}
