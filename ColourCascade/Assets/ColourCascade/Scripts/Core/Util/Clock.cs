using System;

namespace ColourCascade.Core
{
    /// <summary>Time source abstraction so challenge rotation and saves can be tested, and so an
    /// authoritative server clock can replace the device clock later.</summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    public sealed class FixedClock : IClock
    {
        public DateTime Now;

        public FixedClock(DateTime utc)
        {
            Now = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        }

        public DateTime UtcNow => Now;

        public void Advance(TimeSpan by)
        {
            Now = Now.Add(by);
        }
    }

    public static class NumberFormat
    {
        /// <summary>1250 -> "1,250" regardless of device locale.</summary>
        public static string Thousands(long value)
        {
            return value.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Rounds to a "nice" display number: 50s below 2,000, 100s below 10,000, 250s above.</summary>
        public static int RoundNice(int value)
        {
            int step = value < 2000 ? 50 : value < 10000 ? 100 : 250;
            int r = (value + step / 2) / step * step;
            return r < step ? step : r;
        }

        public static string Duration(TimeSpan t)
        {
            if (t.TotalSeconds < 0) t = TimeSpan.Zero;
            if (t.TotalDays >= 1) return string.Format("{0}d {1}h", (int)t.TotalDays, t.Hours);
            if (t.TotalHours >= 1) return string.Format("{0}h {1}m", (int)t.TotalHours, t.Minutes);
            return string.Format("{0}m {1}s", t.Minutes, t.Seconds);
        }
    }
}
