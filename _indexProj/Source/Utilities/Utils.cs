using System;

namespace Hormones
{
    public static class Utils
    {
        public static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        public static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}