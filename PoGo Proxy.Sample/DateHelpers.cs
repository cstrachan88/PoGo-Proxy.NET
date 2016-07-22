using System;

namespace PoGo_Proxy.Sample
{
    public static class DateHelpers
    {
        public static DateTime FromUnixTime(long unixTimeMilli)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTimeMilli);
        }
    }
}
