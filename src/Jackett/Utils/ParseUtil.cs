﻿using System.Globalization;

namespace Jackett.Utils
{
    public static class ParseUtil
    {
        public static float CoerceFloat(string str)
        {
            return float.Parse(str.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        public static int CoerceInt(string str)
        {
            return int.Parse(str.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        public static long CoerceLong(string str)
        {
            return long.Parse(str.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture);
        }


        public static bool TryCoerceFloat(string str, out float result)
        {
            return float.TryParse(str.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }

        public static bool TryCoerceInt(string str, out int result)
        {
            return int.TryParse(str.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }

        public static bool TryCoerceLong(string str, out long result)
        {
            return long.TryParse(str.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }

    }
}
