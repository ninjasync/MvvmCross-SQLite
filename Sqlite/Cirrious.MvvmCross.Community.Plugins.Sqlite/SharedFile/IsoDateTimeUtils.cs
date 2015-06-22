#define DONT_HAVE_DATETIMEOFFSET

using System;
using System.Text;


namespace Community.Sqlite
{
    // based on Newtonsoft.Json DateTimeUtils.

    /// <summary>
    /// Specifies how to treat the time value when converting between string and <see cref="DateTime"/>.
    /// </summary>
    public enum DateTimeZoneHandling
    {
        /// <summary>
        /// Treat as local time. If the <see cref="DateTime"/> object represents a Coordinated Universal Time (UTC), it is converted to the local time.
        /// </summary>
        Local = 0,

        /// <summary>
        /// Treat as a UTC. If the <see cref="DateTime"/> object represents a local time, it is converted to a UTC.
        /// </summary>
        Utc = 1,

        /// <summary>
        /// Treat as a local time if a <see cref="DateTime"/> is being converted to a string.
        /// If a string is being converted to <see cref="DateTime"/>, convert to a local time if a time zone is specified.
        /// </summary>
        Unspecified = 2,

        /// <summary>
        /// Time zone information should be preserved when converting.
        /// </summary>
        RoundtripKind = 3
    }
    
    internal static class IsoDateTimeUtils
    {
        private const int DaysPer100Years = 36524;
        private const int DaysPer400Years = 146097;
        private const int DaysPer4Years = 1461;
        private const int DaysPerYear = 365;
        private const long TicksPerDay = 864000000000L;
        private static readonly int[] DaysToMonth365;
        private static readonly int[] DaysToMonth366;

        static IsoDateTimeUtils()
        {
            DaysToMonth365 = new[] { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365 };
            DaysToMonth366 = new[] { 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 366 };
        }

        public static TimeSpan GetUtcOffset(DateTime d)
        {
#if NET20 
            return TimeZone.CurrentTimeZone.GetUtcOffset(d);
#else
            return TimeZoneInfo.Local.GetUtcOffset(d);
#endif
        }

        internal static bool TryParseDateIso(string text/*, DateParseHandling dateParseHandling*/, DateTimeZoneHandling dateTimeZoneHandling, out object dt)
        {
            DateTimeParser dateTimeParser = new DateTimeParser();
            if (!dateTimeParser.Parse(text))
            {
                dt = null;
                return false;
            }

            DateTime d = new DateTime(dateTimeParser.Year, dateTimeParser.Month, dateTimeParser.Day, dateTimeParser.Hour, dateTimeParser.Minute, dateTimeParser.Second);
            d = d.AddTicks(dateTimeParser.Fraction);

#if !NET20 && !DONT_HAVE_DATETIMEOFFSET
            if (dateParseHandling == DateParseHandling.DateTimeOffset)
            {
                TimeSpan offset;

                switch (dateTimeParser.Zone)
                {
                    case ParserTimeZone.Utc:
                        offset = new TimeSpan(0L);
                        break;
                    case ParserTimeZone.LocalWestOfUtc:
                        offset = new TimeSpan(-dateTimeParser.ZoneHour, -dateTimeParser.ZoneMinute, 0);
                        break;
                    case ParserTimeZone.LocalEastOfUtc:
                        offset = new TimeSpan(dateTimeParser.ZoneHour, dateTimeParser.ZoneMinute, 0);
                        break;
                    default:
                        offset = TimeZoneInfo.Local.GetUtcOffset(d);
                        break;
                }

                long ticks = d.Ticks - offset.Ticks;
                if (ticks < 0 || ticks > 3155378975999999999)
                {
                    dt = null;
                    return false;
                }

                dt = new DateTimeOffset(d, offset);
                return true;
            }
            else
#endif
            {
                long ticks;

                switch (dateTimeParser.Zone)
                {
                    case ParserTimeZone.Utc:
                        d = new DateTime(d.Ticks, DateTimeKind.Utc);
                        break;

                    case ParserTimeZone.LocalWestOfUtc:
                        {
                            TimeSpan offset = new TimeSpan(dateTimeParser.ZoneHour, dateTimeParser.ZoneMinute, 0);
                            ticks = d.Ticks + offset.Ticks;
                            if (ticks <= DateTime.MaxValue.Ticks)
                            {
                                d = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
                            }
                            else
                            {
                                ticks += GetUtcOffset(d).Ticks;
                                if (ticks > DateTime.MaxValue.Ticks)
                                    ticks = DateTime.MaxValue.Ticks;

                                d = new DateTime(ticks, DateTimeKind.Local);
                            }
                            break;
                        }
                    case ParserTimeZone.LocalEastOfUtc:
                        {
                            TimeSpan offset = new TimeSpan(dateTimeParser.ZoneHour, dateTimeParser.ZoneMinute, 0);
                            ticks = d.Ticks - offset.Ticks;
                            if (ticks >= DateTime.MinValue.Ticks)
                            {
                                d = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
                            }
                            else
                            {
                                ticks += GetUtcOffset(d).Ticks;
                                if (ticks < DateTime.MinValue.Ticks)
                                    ticks = DateTime.MinValue.Ticks;

                                d = new DateTime(ticks, DateTimeKind.Local);
                            }
                            break;
                        }
                }

                dt = EnsureDateTime(d, dateTimeZoneHandling);
                return true;
            }
        }

        internal static DateTime EnsureDateTime(DateTime value, DateTimeZoneHandling timeZone)
        {
            switch (timeZone)
            {
                case DateTimeZoneHandling.Local:
                    value = SwitchToLocalTime(value);
                    break;
                case DateTimeZoneHandling.Utc:
                    value = SwitchToUtcTime(value);
                    break;
                case DateTimeZoneHandling.Unspecified:
                    value = new DateTime(value.Ticks, DateTimeKind.Unspecified);
                    break;
                case DateTimeZoneHandling.RoundtripKind:
                    break;
                default:
                    throw new ArgumentException("Invalid date time handling value.");
            }

            return value;
        }

        private static DateTime SwitchToLocalTime(DateTime value)
        {
            switch (value.Kind)
            {
                case DateTimeKind.Unspecified:
                    return new DateTime(value.Ticks, DateTimeKind.Local);

                case DateTimeKind.Utc:
                    return value.ToLocalTime();

                case DateTimeKind.Local:
                    return value;
            }
            return value;
        }

        private static DateTime SwitchToUtcTime(DateTime value)
        {
            switch (value.Kind)
            {
                case DateTimeKind.Unspecified:
                    return new DateTime(value.Ticks, DateTimeKind.Utc);

                case DateTimeKind.Utc:
                    return value;

                case DateTimeKind.Local:
                    return value.ToUniversalTime();
            }
            return value;
        }

        internal static string ToIsoString(this DateTime dt)
        {
            char[] c = new char[64];
            int pos = WriteDefaultIsoDate(c, 0, dt);

            switch (dt.Kind)
            {
                case DateTimeKind.Local:
                    pos = WriteDateTimeOffset(c, pos, GetUtcOffset(dt) /*, format*/);
                    break;
                case DateTimeKind.Utc:
                    c[pos++] = 'Z';
                    break;
            }

            return new string(c, 0, pos);
        }

        internal static int WriteDefaultIsoDate(char[] chars, int start, DateTime dt)
        {
            int length = 19;

            int year;
            int month;
            int day;
            GetDateValues(dt, out year, out month, out day);

            CopyIntToCharArray(chars, start, year, 4);
            chars[start + 4] = '-';
            CopyIntToCharArray(chars, start + 5, month, 2);
            chars[start + 7] = '-';
            CopyIntToCharArray(chars, start + 8, day, 2);
            chars[start + 10] = 'T';
            CopyIntToCharArray(chars, start + 11, dt.Hour, 2);
            chars[start + 13] = ':';
            CopyIntToCharArray(chars, start + 14, dt.Minute, 2);
            chars[start + 16] = ':';
            CopyIntToCharArray(chars, start + 17, dt.Second, 2);

            int fraction = (int)(dt.Ticks % 10000000L);

            if (fraction != 0)
            {
                int digits = 7;
                while ((fraction % 10) == 0)
                {
                    digits--;
                    fraction /= 10;
                }

                chars[start + 19] = '.';
                CopyIntToCharArray(chars, start + 20, fraction, digits);

                length += digits + 1;
            }

            return start + length;
        }

        private static void CopyIntToCharArray(char[] chars, int start, int value, int digits)
        {
            while (digits-- != 0)
            {
                chars[start + digits] = (char)((value % 10) + 48);
                value /= 10;
            }
        }

        internal static int WriteDateTimeOffset(char[] chars, int start, TimeSpan offset/*, DateFormatHandling format*/)
        {
            chars[start++] = (offset.Ticks >= 0L) ? '+' : '-';

            int absHours = Math.Abs(offset.Hours);
            CopyIntToCharArray(chars, start, absHours, 2);
            start += 2;

            //if (format == DateFormatHandling.IsoDateFormat)
                chars[start++] = ':';

            int absMinutes = Math.Abs(offset.Minutes);
            CopyIntToCharArray(chars, start, absMinutes, 2);
            start += 2;

            return start;
        }

        private static void GetDateValues(DateTime td, out int year, out int month, out int day)
        {
            long ticks = td.Ticks;
            // n = number of days since 1/1/0001
            int n = (int)(ticks / TicksPerDay);
            // y400 = number of whole 400-year periods since 1/1/0001
            int y400 = n / DaysPer400Years;
            // n = day number within 400-year period
            n -= y400 * DaysPer400Years;
            // y100 = number of whole 100-year periods within 400-year period
            int y100 = n / DaysPer100Years;
            // Last 100-year period has an extra day, so decrement result if 4
            if (y100 == 4)
                y100 = 3;
            // n = day number within 100-year period
            n -= y100 * DaysPer100Years;
            // y4 = number of whole 4-year periods within 100-year period
            int y4 = n / DaysPer4Years;
            // n = day number within 4-year period
            n -= y4 * DaysPer4Years;
            // y1 = number of whole years within 4-year period
            int y1 = n / DaysPerYear;
            // Last year has an extra day, so decrement result if 4
            if (y1 == 4)
                y1 = 3;

            year = y400 * 400 + y100 * 100 + y4 * 4 + y1 + 1;

            // n = day number within year
            n -= y1 * DaysPerYear;

            // Leap year calculation looks different from IsLeapYear since y1, y4,
            // and y100 are relative to year 1, not year 0
            bool leapYear = y1 == 3 && (y4 != 24 || y100 == 3);
            int[] days = leapYear ? DaysToMonth366 : DaysToMonth365;
            // All months have less than 32 days, so n >> 5 is a good conservative
            // estimate for the month
            int m = n >> 5 + 1;
            // m = 1-based month number
            while (n >= days[m])
            {
                m++;
            }

            month = m;

            // Return 1-based day-of-month
            day = n - days[m - 1] + 1;
        }

        internal enum ParserTimeZone
        {
            Unspecified = 0,
            Utc = 1,
            LocalWestOfUtc = 2,
            LocalEastOfUtc = 3
        }

        internal struct DateTimeParser
        {
            static DateTimeParser()
            {
                Power10 = new[] { -1, 10, 100, 1000, 10000, 100000, 1000000 };

                Lzyyyy = "yyyy".Length;
                Lzyyyy_ = "yyyy-".Length;
                Lzyyyy_MM = "yyyy-MM".Length;
                Lzyyyy_MM_ = "yyyy-MM-".Length;
                Lzyyyy_MM_dd = "yyyy-MM-dd".Length;
                Lzyyyy_MM_ddT = "yyyy-MM-ddT".Length;
                LzHH = "HH".Length;
                LzHH_ = "HH:".Length;
                LzHH_mm = "HH:mm".Length;
                LzHH_mm_ = "HH:mm:".Length;
                LzHH_mm_ss = "HH:mm:ss".Length;
                Lz_ = "-".Length;
                Lz_zz = "-zz".Length;
            }

            public int Year;
            public int Month;
            public int Day;
            public int Hour;
            public int Minute;
            public int Second;
            public int Fraction;
            public int ZoneHour;
            public int ZoneMinute;
            public ParserTimeZone Zone;

            private string _text;
            private int _length;

            private static readonly int[] Power10;

            private static readonly int Lzyyyy;
            private static readonly int Lzyyyy_;
            private static readonly int Lzyyyy_MM;
            private static readonly int Lzyyyy_MM_;
            private static readonly int Lzyyyy_MM_dd;
            private static readonly int Lzyyyy_MM_ddT;
            private static readonly int LzHH;
            private static readonly int LzHH_;
            private static readonly int LzHH_mm;
            private static readonly int LzHH_mm_;
            private static readonly int LzHH_mm_ss;
            private static readonly int Lz_;
            private static readonly int Lz_zz;

            private const short MaxFractionDigits = 7;

            public bool Parse(string text)
            {
                _text = text;
                _length = text.Length;

                if (ParseDate(0) && ParseChar(Lzyyyy_MM_dd, 'T') && ParseTimeAndZoneAndWhitespace(Lzyyyy_MM_ddT))
                    return true;

                return false;
            }

            private bool ParseDate(int start)
            {
                return (Parse4Digit(start, out Year)
                        && 1 <= Year
                        && ParseChar(start + Lzyyyy, '-')
                        && Parse2Digit(start + Lzyyyy_, out Month)
                        && 1 <= Month
                        && Month <= 12
                        && ParseChar(start + Lzyyyy_MM, '-')
                        && Parse2Digit(start + Lzyyyy_MM_, out Day)
                        && 1 <= Day
                        && Day <= DateTime.DaysInMonth(Year, Month));
            }

            private bool ParseTimeAndZoneAndWhitespace(int start)
            {
                return (ParseTime(ref start) && ParseZone(start));
            }

            private bool ParseTime(ref int start)
            {
                if (!(Parse2Digit(start, out Hour)
                      && Hour < 24
                      && ParseChar(start + LzHH, ':')
                      && Parse2Digit(start + LzHH_, out Minute)
                      && Minute < 60
                      && ParseChar(start + LzHH_mm, ':')
                      && Parse2Digit(start + LzHH_mm_, out Second)
                      && Second < 60))
                {
                    return false;
                }

                start += LzHH_mm_ss;
                if (ParseChar(start, '.'))
                {
                    Fraction = 0;
                    int numberOfDigits = 0;

                    while (++start < _length && numberOfDigits < MaxFractionDigits)
                    {
                        int digit = _text[start] - '0';
                        if (digit < 0 || digit > 9)
                            break;

                        Fraction = (Fraction * 10) + digit;

                        numberOfDigits++;
                    }

                    if (numberOfDigits < MaxFractionDigits)
                    {
                        if (numberOfDigits == 0)
                            return false;

                        Fraction *= Power10[MaxFractionDigits - numberOfDigits];
                    }
                }
                return true;
            }

            private bool ParseZone(int start)
            {
                if (start < _length)
                {
                    char ch = _text[start];
                    if (ch == 'Z' || ch == 'z')
                    {
                        Zone = ParserTimeZone.Utc;
                        start++;
                    }
                    else
                    {
                        if (start + 2 < _length
                            && Parse2Digit(start + Lz_, out ZoneHour)
                            && ZoneHour <= 99)
                        {
                            switch (ch)
                            {
                                case '-':
                                    Zone = ParserTimeZone.LocalWestOfUtc;
                                    start += Lz_zz;
                                    break;

                                case '+':
                                    Zone = ParserTimeZone.LocalEastOfUtc;
                                    start += Lz_zz;
                                    break;
                            }
                        }

                        if (start < _length)
                        {
                            if (ParseChar(start, ':'))
                            {
                                start += 1;

                                if (start + 1 < _length
                                    && Parse2Digit(start, out ZoneMinute)
                                    && ZoneMinute <= 99)
                                {
                                    start += 2;
                                }
                            }
                            else
                            {
                                if (start + 1 < _length
                                    && Parse2Digit(start, out ZoneMinute)
                                    && ZoneMinute <= 99)
                                {
                                    start += 2;
                                }
                            }
                        }
                    }
                }

                return (start == _length);
            }

            private bool Parse4Digit(int start, out int num)
            {
                if (start + 3 < _length)
                {
                    int digit1 = _text[start] - '0';
                    int digit2 = _text[start + 1] - '0';
                    int digit3 = _text[start + 2] - '0';
                    int digit4 = _text[start + 3] - '0';
                    if (0 <= digit1 && digit1 < 10
                        && 0 <= digit2 && digit2 < 10
                        && 0 <= digit3 && digit3 < 10
                        && 0 <= digit4 && digit4 < 10)
                    {
                        num = (((((digit1 * 10) + digit2) * 10) + digit3) * 10) + digit4;
                        return true;
                    }
                }
                num = 0;
                return false;
            }

            private bool Parse2Digit(int start, out int num)
            {
                if (start + 1 < _length)
                {
                    int digit1 = _text[start] - '0';
                    int digit2 = _text[start + 1] - '0';
                    if (0 <= digit1 && digit1 < 10
                        && 0 <= digit2 && digit2 < 10)
                    {
                        num = (digit1 * 10) + digit2;
                        return true;
                    }
                }
                num = 0;
                return false;
            }

            private bool ParseChar(int start, char ch)
            {
                return (start < _length && _text[start] == ch);
            }
        }
    }
}
