//
// --------------------------------------------------------------------------
//  Gurux Ltd
//
//
//
// Filename:        $HeadURL$
//
// Version:         $Revision$,
//                  $Date$
//                  $Author$
//
// Copyright (c) Gurux Ltd
//
//---------------------------------------------------------------------------
//
//  DESCRIPTION
//
// This file is a part of Gurux Device Framework.
//
// Gurux Device Framework is Open Source software; you can redistribute it
// and/or modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; version 2 of the License.
// Gurux Device Framework is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License for more details.
//
// More information of Gurux products: https://www.gurux.org
//
// This code is licensed under the GNU General Public License v2.
// Full text may be retrieved at http://www.gnu.org/licenses/gpl-2.0.txt
//---------------------------------------------------------------------------

using Gurux.Scheduling.Enums;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace Gurux.Scheduling
{
    [JsonConverter(typeof(GXDateTimeJsonConverter))]
    public class GXDateTime : IConvertible
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public GXDateTime() : this(DateTime.MinValue, null)
        {

        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public GXDateTime(GXDateTime value) : this(value, null)
        {
            if (value != null)
            {
                Skip = value.Skip;
                Extra = value.Extra;
                ScheduledDayOfWeek = value.ScheduledDayOfWeek;
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public GXDateTime(DateTime value) : this(value, null)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public GXDateTime(DateTime value, TimeZoneInfo? timeZone)
        {
            if (value == DateTime.MinValue)
            {
                Value = DateTimeOffset.MinValue;
            }
            else if (value == DateTime.MaxValue)
            {
                Value = DateTimeOffset.MaxValue;
            }
            else
            {
                if (timeZone != null)
                {
                    Value = new DateTimeOffset(value, timeZone.GetUtcOffset(value));
                }
                else if (value.Kind == DateTimeKind.Utc)
                {
                    Value = new DateTimeOffset(value, TimeZoneInfo.Utc.GetUtcOffset(value));
                }
                else
                {
                    Value = new DateTimeOffset(value, TimeZoneInfo.Local.GetUtcOffset(value));
                }
                Skip |= DateTimeSkips.Ms;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="value">Date time value as a string.</param>
        public GXDateTime(string value)
            : this(value, CultureInfo.CurrentCulture)
        {
        }

        private static bool IsNumeric(char value)
        {
            return value >= '0' && value <= '9';
        }

        private static string GetDateTimeFormat(CultureInfo culture)
        {
            string str = culture.DateTimeFormat.ShortDatePattern + " " + culture.DateTimeFormat.LongTimePattern;
#if !WINDOWS_UWP
            foreach (string it in culture.DateTimeFormat.GetAllDateTimePatterns())
            {
                if (!it.Contains("dddd") && it.Contains(culture.DateTimeFormat.ShortDatePattern) && it.Contains(culture.DateTimeFormat.LongTimePattern))
                {
                    return it;
                }
            }
#endif //!WINDOWS_UWP
            return str;
        }

        /// <summary>
        /// Check is time zone included and return index of time zone.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static int TimeZonePosition(string value)
        {
            if (value.Length > 5)
            {
                int pos = value.Length - 6;
                char sep = value[pos];
                if (sep == '-' || sep == '+')
                {
                    return pos;
                }
            }
            return -1;
        }

        private string ParseLocalizedWeekdaySchedule(string value, CultureInfo culture)
        {
            int space = value.IndexOf(' ');
            if (space == -1)
            {
                return value;
            }
            string[] fields = value.Substring(0, space).Split('/');
            if (fields.Length != 3 || fields[0].Length == 0 || IsNumeric(fields[0][0]) || fields[0][0] == '*')
            {
                return value;
            }
            if (fields[0].Equals("BEGIN", StringComparison.OrdinalIgnoreCase) ||
                fields[0].Equals("END", StringComparison.OrdinalIgnoreCase) ||
                fields[0].Equals("LASTDAY", StringComparison.OrdinalIgnoreCase) ||
                fields[0].Equals("LASTDAY2", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
            int dayOfWeek = -1;
            for (int pos = 0; pos != culture.DateTimeFormat.DayNames.Length; ++pos)
            {
                if (string.Equals(fields[0], culture.DateTimeFormat.DayNames[pos], StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fields[0], culture.DateTimeFormat.AbbreviatedDayNames[pos], StringComparison.OrdinalIgnoreCase))
                {
                    dayOfWeek = pos;
                    break;
                }
            }
            if (dayOfWeek == -1)
            {
                throw new FormatException("Invalid localized day of week.");
            }
            string[] dateFields = culture.DateTimeFormat.ShortDatePattern
                .Split(culture.DateTimeFormat.DateSeparator);
            for (int pos = 0; pos != dateFields.Length; ++pos)
            {
                if (dateFields[pos].Contains('y'))
                {
                    dateFields[pos] = fields[2];
                }
                else if (dateFields[pos].Contains('M'))
                {
                    dateFields[pos] = fields[1];
                }
                else
                {
                    dateFields[pos] = "*";
                }
            }
            ScheduledDayOfWeek = dayOfWeek;
            return string.Join(culture.DateTimeFormat.DateSeparator, dateFields) + value.Substring(space);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="value">Date time value as a string.</param>
        /// <param name="culture">Used culture.</param>
        public GXDateTime(string value, CultureInfo culture)
            : base()
        {
            bool addTimeZone = true;
            CultureInfo? parseCulture = culture;
            if (!string.IsNullOrEmpty(value))
            {
                value = ParseLocalizedWeekdaySchedule(value, culture);
                StringBuilder format = new StringBuilder();
                format.Append(GetDateTimeFormat(culture));
                Remove(format, culture);
                if (value.IndexOf("BEGIN") != -1)
                {
                    Extra |= DateTimeExtraInfo.DstBegin;
                    value = value.Replace("BEGIN", "01");
                }
                if (value.IndexOf("END") != -1)
                {
                    Extra |= DateTimeExtraInfo.DstEnd;
                    value = value.Replace("END", "01");
                }
                if (value.IndexOf("LASTDAY2") != -1)
                {
                    Extra |= DateTimeExtraInfo.LastDay2;
                    value = value.Replace("LASTDAY2", "01");
                }
                if (value.IndexOf("LASTDAY") != -1)
                {
                    Extra |= DateTimeExtraInfo.LastDay;
                    value = value.Replace("LASTDAY", "01");
                }
                String v = value;

                if (value.IndexOf('*') != -1)
                {
                    //Day of week is not supported when date time is give as a string.
                    int lastFormatIndex = -1;
                    int offset = 0;
                    for (int pos = 0; pos < value.Length; ++pos)
                    {
                        char c = value[pos];
                        if (!IsNumeric(c))
                        {
                            if (c == '*')
                            {
                                int cnt = 1;
                                c = format[lastFormatIndex + 1];
                                string val = c == 'y' ? "2" : "1";
                                while (lastFormatIndex + cnt + 1 < format.Length && format[lastFormatIndex + cnt + 1] == c)
                                {
                                    val += "0";
                                    ++cnt;
                                }
                                v = v.Substring(0, pos + offset) + val
                                        + value.Substring(pos + 1);
                                offset += cnt - 1;
                                string tmp = format.ToString();
                                tmp = tmp.Substring(lastFormatIndex + 1, cnt).Trim();
                                if (tmp.StartsWith("y"))
                                {
                                    addTimeZone = false;
                                    Skip |= DateTimeSkips.Year;
                                }
                                else if (tmp == "M" || tmp == "MM" || tmp == "MMM")
                                {
                                    addTimeZone = false;
                                    Skip |= DateTimeSkips.Month;
                                }
                                else if (tmp.Equals("dd") || tmp.Equals("d"))
                                {
                                    addTimeZone = false;
                                    Skip |= DateTimeSkips.Day;
                                }
                                else if (tmp.Equals("h") || tmp.Equals("hh")
                                      || tmp.Equals("HH") || tmp.Equals("H"))
                                {
                                    addTimeZone = false;
                                    Skip |= DateTimeSkips.Hour;
                                    if (format.ToString().IndexOf("tt") != -1 &&
                                        value.IndexOf(culture.DateTimeFormat.AMDesignator, StringComparison.OrdinalIgnoreCase) == -1 &&
                                        value.IndexOf(culture.DateTimeFormat.PMDesignator, StringComparison.OrdinalIgnoreCase) == -1)
                                    {
                                        value += " " + culture.DateTimeFormat.AMDesignator;
                                        v += " " + culture.DateTimeFormat.AMDesignator;
                                    }
                                }
                                else if (tmp.Equals("mm") || tmp.Equals("m"))
                                {
                                    addTimeZone = false;
                                    Skip |= DateTimeSkips.Minute;
                                }
                                else if (tmp.Equals("tt"))
                                {
                                    addTimeZone = false;
                                    Skip |= DateTimeSkips.Hour;
                                    format.Replace("tt", "");
                                }
                                else if (tmp.Equals("ss"))
                                {
                                    Skip |= DateTimeSkips.Second;
                                }
                                else if (tmp.Length != 0 && !tmp.Equals("G"))
                                {
                                    throw new Exception("Invalid date time format.");
                                }
                            }
                            else
                            {
                                lastFormatIndex = format.ToString().IndexOf(c, lastFormatIndex + 1);
                                //Dot is used time separator in some countries.
                                if (lastFormatIndex == -1 && c == ':')
                                {
                                    lastFormatIndex = format.ToString().IndexOf('.', lastFormatIndex + 1);
                                }
                            }
                        }
                    }
                }
                try
                {
                    // If time zone is used.
                    int pos;
                    if (addTimeZone && (pos = TimeZonePosition(value)) != -1)
                    {
                        format.Append("zzz");
                        parseCulture = null;
                    }
                    else if (addTimeZone && value.IndexOf('Z') != -1)
                    {
                        format.Append("zzz");
                        v = v.Replace("Z", "+00:00");
                        parseCulture = null;
                    }
                    else if (parseCulture == CultureInfo.InvariantCulture)
                    {
                        Skip |= DateTimeSkips.Deviation;
                    }
                    if (parseCulture == null)
                    {
                        Value = DateTimeOffset.ParseExact(v, format.ToString(), parseCulture);
                    }
                    else
                    {
                        if (addTimeZone)
                        {
                            v = v.Replace("Z", "+00:00");
                        }
                        else
                        {
                            v = v.Replace("Z", "");
                        }
                        Value = DateTime.ParseExact(v, format.ToString(), parseCulture);
                    }
                    Skip |= DateTimeSkips.Ms;
                }
                catch (Exception)
                {
                    try
                    {
                        //Append seconds if not in the format.
                        if ((Skip & DateTimeSkips.Second) == 0 && format.ToString().IndexOf("ss") == -1)
                        {
                            format.Replace("mm", "mm.ss");
                        }
                        Value = DateTime.ParseExact(v, format.ToString().Trim(), parseCulture);
                        Skip |= DateTimeSkips.Ms;
                    }
                    catch (Exception)
                    {
                        try
                        {
                            //Append ms if not in the format.
                            if ((Skip & DateTimeSkips.Ms) == 0 && format.ToString().IndexOf("fff") == -1)
                            {
                                format.Replace("ss", "ss.fff");
                            }
                            Value = DateTime.ParseExact(v, format.ToString().Trim(), culture);
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public GXDateTime(DateTimeOffset value)
        {
            Value = value;
        }

        /// <summary>
        /// Convert DateTime to GXDateTime.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static implicit operator GXDateTime(DateTime value)
        {
            GXDateTime dt = new GXDateTime(value);
            dt.Skip |= DateTimeSkips.Ms;
            return dt;
        }

        /// <summary>
        /// Convert GXDateTime to DateTime.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static implicit operator DateTime(GXDateTime value)
        {
            if (value != null)
            {
                return value.Value.LocalDateTime;
            }
            return DateTime.MinValue;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public GXDateTime(int year, int month, int day, int hour, int minute, int second, int millisecond)
        {
            if (year < 1 || year == 0xFFFF)
            {
                Skip |= DateTimeSkips.Year;
                //Set year to 4 because there might be leap year.
                //29/02/*" (29th February)
                year = 4;
            }
            if (month == 0xFE)
            {
                Extra |= DateTimeExtraInfo.DstBegin;
            }
            else if (month == 0xFD)
            {
                Extra |= DateTimeExtraInfo.DstEnd;
            }
            if (month < 1 || month > 12)
            {
                Skip |= DateTimeSkips.Month;
                month = 1;
            }

            if (day == 0xFE)
            {
                Extra |= DateTimeExtraInfo.LastDay;
                day = 1;
            }
            else if (day == 0xFD)
            {
                Extra |= DateTimeExtraInfo.LastDay2;
                day = 1;
            }
            else if (day < 1 || day > 31)
            {
                Skip |= DateTimeSkips.Day;
                day = 1;
            }
            if (hour < 0 || hour > 24)
            {
                Skip |= DateTimeSkips.Hour;
                hour = 0;
            }
            if (minute < 0 || minute > 60)
            {
                Skip |= DateTimeSkips.Minute;
                minute = 0;
            }
            if (second < 0 || second > 60)
            {
                Skip |= DateTimeSkips.Second;
                second = 0;
            }
            if (millisecond < 0 || millisecond > 1000)
            {
                Skip |= DateTimeSkips.Ms;
                millisecond = 0;
            }
            try
            {
                if (year == 1 && month == 1 && day == 1 && hour == 0 && minute == 0 && second == 0)
                {
                    Value = new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Utc);
                }
                else
                {
                    Value = new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Local);
                }
            }
            catch
            {
                Value = DateTime.MaxValue;
            }
        }

        /// <summary>
        /// Used date time value.
        /// </summary>
        public DateTimeOffset Value
        {
            get;
            set;
        }

        /// <summary>
        /// Skip selected date time fields.
        /// </summary>
        [DefaultValue(DateTimeSkips.None)]
        public DateTimeSkips Skip
        {
            get;
            set;
        }

        /// <summary>
        /// Date time extra information.
        /// </summary>
        [DefaultValue(DateTimeExtraInfo.None)]
        public DateTimeExtraInfo Extra
        {
            get;
            set;
        }

        private int? ScheduledDayOfWeek
        {
            get;
            set;
        }

        public string ToFormatString()
        {
            return ToFormatString(CultureInfo.CurrentCulture);
        }

        public string ToFormatString(CultureInfo culture)
        {
            return ToFormatString(culture, true);
        }

        public string ToFormatMeterString()
        {
            return ToFormatMeterString(CultureInfo.CurrentCulture);
        }
        public string ToFormatMeterString(CultureInfo culture)
        {
            return ToFormatString(culture, false);
        }

        public string ToFormatString(CultureInfo culture, bool useLocalTime)
        {
            if (Value.DateTime == DateTime.MinValue ||
                Value.DateTime == DateTime.MaxValue)
            {
                return "";
            }
            if (ScheduledDayOfWeek.HasValue)
            {
                GXDateTime value = new GXDateTime(this)
                {
                    ScheduledDayOfWeek = null
                };
                string formatted = value.ToFormatString(culture, useLocalTime);
                int space = formatted.IndexOf(' ');
                if (space != -1)
                {
                    string month = (Skip & DateTimeSkips.Month) != 0 ? "*" : Value.Month.ToString(culture);
                    string year = (Skip & DateTimeSkips.Year) != 0 ? "*" : Value.Year.ToString(culture);
                    return culture.DateTimeFormat.DayNames[ScheduledDayOfWeek.Value] + "/" + month + "/" + year + formatted.Substring(space);
                }
            }
            StringBuilder format = new StringBuilder();
            format.Append(GetDateTimeFormat(culture));
            Remove(format, culture);
            if (!useLocalTime && (Skip & DateTimeSkips.Deviation) == 0)
            {
                format.Append("zzz");
            }
            if ((Extra & DateTimeExtraInfo.DstBegin) != 0)
            {
                format.Replace("MMM", "BEGIN");
                format.Replace("MM", "BEGIN");
                format.Replace("M", "BEGIN");
            }
            else if ((Extra & DateTimeExtraInfo.DstEnd) != 0)
            {
                format.Replace("MMM", "END");
                format.Replace("MM", "END");
                format.Replace("M", "END");
            }
            else if ((Extra & DateTimeExtraInfo.LastDay) != 0)
            {
                format.Replace("dd", "LASTDAY");
                format.Replace("d", "LASTDAY");
            }
            else if ((Extra & DateTimeExtraInfo.LastDay2) != 0)
            {
                format.Replace("dd", "LASTDAY2");
                format.Replace("d", "LASTDAY2");
            }
            if ((Skip & DateTimeSkips.Year) != 0)
            {
                Replace(format, "yyyy");
                Replace(format, "yy");
                Remove(format, "zzz", null);
            }
            if ((Skip & DateTimeSkips.Month) != 0)
            {
                Replace(format, "MMM");
                Replace(format, "MM");
                Replace(format, "M");
                Remove(format, "zzz", null);
            }
            if ((Skip & DateTimeSkips.Day) != 0)
            {
                Replace(format, "dd");
                Replace(format, "d");
                Remove(format, "zzz", null);
            }
            if ((Skip & DateTimeSkips.Hour) != 0)
            {
                Replace(format, "HH");
                Replace(format, "H");
                Replace(format, "hh");
                Replace(format, "h");
                Remove(format, "tt", null);
                Remove(format, "zzz", null);
            }
            if ((Skip & DateTimeSkips.Ms) != 0 || Value.LocalDateTime.Millisecond == 0)
            {
                Replace(format, ".fff");
            }
            else if (format.ToString().IndexOf(".fff") == -1)
            {
                format.Replace("ss", "ss.fff");
            }
            if ((Skip & DateTimeSkips.Second) != 0)
            {
                Replace(format, "ss");
            }
            else if (format.ToString().IndexOf("ss") == -1)
            {
#if !WINDOWS_UWP
                format.Replace("mm", "mm.ss");
#else
                    format.Replace("mm", "mm.ss");
#endif //!WINDOWS_UWP
            }
            if ((Skip & DateTimeSkips.Minute) != 0)
            {
                Replace(format, "mm");
                Replace(format, "m");
                Remove(format, "zzz", null);
            }
            if (useLocalTime)
            {
                return Value.LocalDateTime.ToString(format.ToString().Trim(), culture);
            }
            string ret = Value.ToString(format.ToString().Trim(), culture);
            if (Value.DateTime == Value.UtcDateTime)
            {
                ret = ret + "Z";
            }
            return ret;
        }

        private void Remove(StringBuilder value, String tag, string? sep)
        {
            if (sep != null)
            {
                if (value.ToString().IndexOf(tag + sep) != -1)
                {
                    value.Replace(tag + sep, "");
                    return;
                }
                else if (value.ToString().IndexOf(sep + tag) != -1)
                {
                    value.Replace(sep + tag, "");
                    return;
                }
            }
            value.Replace(tag, "");
        }

        private void Replace(StringBuilder value, string tag)
        {
            value.Replace(tag, "*");
        }

        private void Remove(StringBuilder format, CultureInfo culture)
        {
#if !(NET5_0_OR_GREATER || NET48) || WINDOWS_UWP
            string timeSeparator = ":";
            string dateSeparator = "/";
#else
            string timeSeparator = culture.DateTimeFormat.TimeSeparator;
            string dateSeparator = culture.DateTimeFormat.DateSeparator;
#endif //!(NET5_0_OR_GREATER || NET48) || WINDOWS_UWP
            // Trim
            string tmp = format.ToString();
            format.Length = 0;
            format.Append(tmp.Trim());
        }

        /// <summary>
        /// Date time to string.
        /// </summary>
        /// <returns>Date time as a string.</returns>
        public override string ToString()
        {
            return ToString(CultureInfo.CurrentCulture, true);
        }

        /// <summary>
        /// Date time to string.
        /// </summary>
        /// <returns>Date time as a string.</returns>
        public string ToString(CultureInfo culture)
        {
            return ToString(culture, true);
        }

        /// <summary>
        /// Date time to meter string.
        /// </summary>
        /// <returns>Date time as a string.</returns>
        public string ToMeterString()
        {
            return ToString(CultureInfo.CurrentCulture, false);
        }

        /// <summary>
        /// Date time to meter string.
        /// </summary>
        /// <returns>Date time as a string.</returns>
        public string ToMeterString(CultureInfo culture)
        {
            return ToString(culture, false);
        }

        private string ToString(CultureInfo culture, bool useLocalTime)
        {
            StringBuilder format = new StringBuilder();
            if (Skip != DateTimeSkips.None)
            {
#if !(NET5_0_OR_GREATER || NET48) || WINDOWS_UWP
                string timeSeparator = ":";
                string dateSeparator = "/";
#else
                string timeSeparator = culture.DateTimeFormat.TimeSeparator;
                string dateSeparator = culture.DateTimeFormat.DateSeparator;
#endif //!(NET5_0_OR_GREATER || NET48) || WINDOWS_UWP

                format.Append(GetDateTimeFormat(culture));
                Remove(format, culture);
                if (!useLocalTime)
                {
                    format.Append("zzz");
                }
                if ((Skip & DateTimeSkips.Year) != 0)
                {
                    Remove(format, "yyyy", dateSeparator);
                    Remove(format, "yy", dateSeparator);
                    Remove(format, "zzz", null);
                }
                if ((Skip & DateTimeSkips.Month) != 0)
                {
                    Remove(format, "MM", dateSeparator);
                    Remove(format, "M", dateSeparator);
                    Remove(format, "zzz", null);
                }
                if ((Skip & DateTimeSkips.Day) != 0)
                {
                    Remove(format, "dd", dateSeparator);
                    Remove(format, "d", dateSeparator);
                    Remove(format, "zzz", null);
                }
                if ((Skip & DateTimeSkips.Hour) != 0)
                {
                    Remove(format, "HH", timeSeparator);
                    Remove(format, "H", timeSeparator);
                    Remove(format, "hh", timeSeparator);
                    Remove(format, "h", timeSeparator);
                    Remove(format, "tt", timeSeparator);
                    Remove(format, "zzz", null);
                }
                if ((Skip & DateTimeSkips.Ms) != 0)
                {
                    Remove(format, ".fff", timeSeparator);
                }
                else if (format.ToString().IndexOf(".fff") == -1)
                {
                    format.Replace("ss", "ss.fff");
                }
                if ((Skip & DateTimeSkips.Second) != 0)
                {
                    Remove(format, "ss", timeSeparator);
                }
                else if (format.ToString().IndexOf("ss") == -1)
                {
                    format.Replace("mm", "mm" + timeSeparator + "ss");
                }
                if ((Skip & DateTimeSkips.Minute) != 0)
                {
                    Remove(format, "mm", timeSeparator);
                    Remove(format, "m", timeSeparator);
                    Remove(format, "zzz", null);
                }
                string tmp = format.ToString().Trim();
                if (tmp == "")
                {
                    return "";
                }
                //FormatException is thrown if length of format is 1.
                if (tmp.IndexOf(dateSeparator) == -1 && tmp.IndexOf(timeSeparator) == -1)
                {
                    if ((Skip & DateTimeSkips.Year) == 0)
                    {
                        return Value.Year.ToString();
                    }
                    if ((Skip & DateTimeSkips.Month) == 0)
                    {
                        return Value.Month.ToString();
                    }
                    if ((Skip & DateTimeSkips.Day) == 0)
                    {
                        return Value.Day.ToString();
                    }
                    if ((Skip & DateTimeSkips.Hour) == 0)
                    {
                        return Value.Hour.ToString();
                    }
                    if ((Skip & DateTimeSkips.Minute) == 0)
                    {
                        return Value.Minute.ToString();
                    }
                    if ((Skip & DateTimeSkips.Second) == 0)
                    {
                        return Value.Second.ToString();
                    }
                    if ((Skip & DateTimeSkips.Ms) == 0)
                    {
                        return Value.Millisecond.ToString();
                    }
                }
                if (useLocalTime)
                {
                    return Value.LocalDateTime.ToString(tmp, culture);
                }
                string ret = Value.ToString(tmp, culture);
                if (Value.DateTime == Value.UtcDateTime)
                {
                    ret = ret.Substring(0, ret.Length - 6) + "Z";
                }
                return ret;
            }
            if (useLocalTime)
            {
                return Value.LocalDateTime.ToString(culture);
            }
            return Value.ToString(culture);
        }

        private static int GetSeconds(DateTime start, GXDateTime value)
        {
            int ret = 0;
            if ((value.Skip & DateTimeSkips.Second) != 0)
            {
                return value.Value.Second;
            }
            else if ((value.Skip & DateTimeSkips.Minute) != 0)
            {
                ret = value.Value.Second;
                ret -= start.Second;
            }
            else if ((value.Skip & DateTimeSkips.Hour) != 0)
            {
                ret = (60 * value.Value.Minute) + value.Value.Second;
                ret -= (60 * start.Minute) + start.Second;
            }
            else if ((value.Skip & DateTimeSkips.Day) != 0)
            {
                ret = (60 * 60 * value.Value.Hour) + (60 * value.Value.Minute) + value.Value.Second;
                ret -= (60 * 60 * start.Hour) + (60 * start.Minute) + start.Second;
            }
            else if ((value.Skip & DateTimeSkips.Month) != 0)
            {
                ret = 1 * ((60 * 60 * value.Value.Hour) + (60 * value.Value.Minute) + value.Value.Second);
                ret -= 1 * ((60 * 60 * start.Hour) + (60 * start.Minute) + start.Second);
            }
            else if ((value.Skip & DateTimeSkips.Year) != 0)
            {
                DateTime tmp = value.Value.DateTime;
                tmp = tmp.AddYears(start.Year - tmp.Year);
                ret = (int)(tmp - start).TotalSeconds;
            }
            return ret;
        }

        /// <summary>
        /// Gets the time remaining until the next scheduled occurrence.
        /// </summary>
        /// <param name="value">The date and time used to determine the next scheduled occurrence.</param>
        /// <returns>The time remaining until the next scheduled occurrence.</returns>
        public static TimeSpan GetTimeUntilNextSchedule(GXDateTime value)
        {
            DateTime now = DateTime.Now;
            return GetNextScheduledDates(now, value, 1)[0] - now;
        }

        /// <summary>
        /// Get next schedule dates.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public static DateTime[] GetNextScheduledDates(DateTime currentTime, GXDateTime value, int count)
        {
            List<DateTime> list = new List<DateTime>();
            for (int pos = 0; pos != count; ++pos)
            {
                currentTime = GetNextScheduledDate(currentTime, value, pos == 0);
                list.Add(currentTime);
            }
            return list.ToArray();
        }

        private static DateTime GetNextScheduledDate(DateTime currentTime, GXDateTime value, bool includeCurrent)
        {
            DateTime date = currentTime.Date;
            while (date <= DateTime.MaxValue.Date)
            {
                if (IsScheduledDate(date, value))
                {
                    for (int hour = 0; hour != 24; ++hour)
                    {
                        if ((value.Skip & DateTimeSkips.Hour) == 0 && hour != value.Value.Hour)
                        {
                            continue;
                        }
                        for (int minute = 0; minute != 60; ++minute)
                        {
                            if ((value.Skip & DateTimeSkips.Minute) == 0 && minute != value.Value.Minute)
                            {
                                continue;
                            }
                            for (int second = 0; second != 60; ++second)
                            {
                                if ((value.Skip & DateTimeSkips.Second) == 0 && second != value.Value.Second)
                                {
                                    continue;
                                }
                                DateTime candidate = new DateTime(date.Year, date.Month, date.Day, hour, minute, second);
                                if (candidate > currentTime || (includeCurrent && candidate == currentTime))
                                {
                                    return candidate;
                                }
                            }
                        }
                    }
                }
                if (date == DateTime.MaxValue.Date)
                {
                    break;
                }
                date = date.AddDays(1);
            }
            throw new ArgumentOutOfRangeException(nameof(value), "No future scheduled date can be calculated.");
        }

        private static bool IsScheduledDate(DateTime date, GXDateTime value)
        {
            if ((value.Skip & DateTimeSkips.Year) == 0 && date.Year != value.Value.Year ||
                (value.Skip & DateTimeSkips.Month) == 0 && date.Month != value.Value.Month)
            {
                return false;
            }
            if ((value.Extra & DateTimeExtraInfo.LastDay) != 0 && date.Day != DateTime.DaysInMonth(date.Year, date.Month) ||
                (value.Extra & DateTimeExtraInfo.LastDay2) != 0 && date.Day != DateTime.DaysInMonth(date.Year, date.Month) - 1 ||
                (value.Extra & (DateTimeExtraInfo.LastDay | DateTimeExtraInfo.LastDay2)) == 0 &&
                (value.Skip & DateTimeSkips.Day) == 0 && date.Day != value.Value.Day)
            {
                return false;
            }
            if (value.ScheduledDayOfWeek.HasValue)
            {
                return (int)date.DayOfWeek == value.ScheduledDayOfWeek.Value;
            }
            return true;
        }

        /// <summary>
        /// Get difference between given time and run time in ms.
        /// </summary>
        /// <param name="start">Start date time.</param>
        /// <param name="to">Compared time.</param>
        /// <returns>Difference in milliseconds.</returns>
        public static long GetDifference(DateTime start, GXDateTime to)
        {
            long diff = 0;
            //Compare ms.
            if ((to.Skip & DateTimeSkips.Ms) == 0)
            {
                if (start.Millisecond < to.Value.Millisecond)
                {
                    diff = to.Value.Millisecond;
                }
                else
                {
                    diff = -to.Value.Millisecond;
                }
            }
            //Compare seconds.
            if ((to.Skip & DateTimeSkips.Second) == 0)
            {
                if (start.Second < to.Value.Second)
                {
                    diff += (to.Value.Second - start.Second) * 1000L;
                }
                else
                {
                    diff -= (start.Second - to.Value.Second) * 1000L;
                }
            }
            else if (diff < 0)
            {
                diff = 60000 + diff;
            }
            //Compare minutes.
            if ((to.Skip & DateTimeSkips.Minute) == 0)
            {
                if (start.Minute < to.Value.Minute)
                {
                    diff += (to.Value.Minute - start.Minute) * 60000L;
                }
                else
                {
                    diff -= (start.Minute - to.Value.Minute) * 60000L;
                }
            }
            else if (diff < 0)
            {
                diff += 60 * 60000;
            }
            //Compare hours.
            if ((to.Skip & DateTimeSkips.Hour) == 0)
            {
                if (start.Hour < to.Value.Hour)
                {
                    diff += (to.Value.Hour - start.Hour) * 60 * 60000L;
                }
                else
                {
                    diff -= (start.Hour - to.Value.Hour) * 60 * 60000L;
                }
            }
            else if (diff < 0)
            {
                diff += 60 * 60000;
            }
            //Compare days.          
            if ((to.Skip & DateTimeSkips.Day) == 0)
            {
                if (start.Day < to.Value.Day)
                {
                    diff += (to.Value.Day - start.Day) * 24 * 60 * 60000;
                }
                else if (start.Day != to.Value.Day)
                {
                    if ((to.Skip & DateTimeSkips.Month) == 0)
                    {
                        if ((to.Extra & DateTimeExtraInfo.LastDay) != 0)
                        {
                            diff += (DateTime.DaysInMonth(to.Value.Year, to.Value.Month) - start.Day) * 24 * 60 * 60000L;
                        }
                        else if ((to.Extra & DateTimeExtraInfo.LastDay2) != 0)
                        {
                            diff += (DateTime.DaysInMonth(to.Value.Year, to.Value.Month) - 1 - start.Day) * 24 * 60 * 60000L;
                        }
                        else
                        {
                            diff += (to.Value.Day - start.Day) * 24 * 60 * 60000L;
                        }
                    }
                    else
                    {
                        diff += ((DateTime.DaysInMonth(start.Year, start.Month) - start.Day + to.Value.Day) * 24 * 60 * 60000L);
                    }
                }
            }
            else if (diff < 0)
            {
                diff += 24 * 60 * 60000;
            }
            //Compare months.
            if ((to.Skip & DateTimeSkips.Month) == 0)
            {
                if (start.Month < to.Value.Month)
                {
                    for (int m = start.Month; m != to.Value.Month; ++m)
                    {
                        diff += DateTime.DaysInMonth(start.Year, m) * 24 * 60 * 60000L;
                    }
                }
                else
                {
                    for (int m = to.Value.Month; m != start.Month; ++m)
                    {
                        diff += -DateTime.DaysInMonth(start.Year, m) * 24 * 60 * 60000L;
                    }
                }
            }
            else if (diff < 0)
            {
                diff += DateTime.DaysInMonth(start.Year, start.Month) * 24 * 60 * 60000L;
            }
            //Compare years.
            if ((to.Skip & DateTimeSkips.Year) == 0)
            {
                int s = to.Value.Year;
                int e = start.Year;
                if (s > e)
                {
                    for (int y = e; y != s; ++y)
                    {
                        for (int m = 1; m <= 12; ++m)
                        {
                            diff += DateTime.DaysInMonth(y, m) * 24 * 60 * 60000L;
                        }
                    }
                }
                else
                {
                    for (int y = s; y != e; ++y)
                    {
                        for (int m = 1; m <= 12; ++m)
                        {
                            diff -= DateTime.DaysInMonth(y, m) * 24 * 60 * 60000L;
                        }
                    }
                }
            }
            else if (diff < 0)
            {
                int y = start.Year;
                if (y != to.Value.Year)
                {
                    for (int m = 1; m <= 12; ++m)
                    {
                        diff += DateTime.DaysInMonth(y, m) * 24 * 60 * 60000L;
                    }
                }
            }
            return diff;
        }

        /// <summary>
        /// Get date time from Epoch time.
        /// </summary>
        /// <param name="unixTime">Unix time.</param>
        /// <returns>Date and time.</returns>
        public static GXDateTime FromUnixTime(long unixTime)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return new GXDateTime(epoch.AddSeconds(unixTime).ToLocalTime());
        }

        /// <summary>
        /// Get date time from high resolution clock time.
        /// </summary>
        /// <remarks>
        /// High resolution clock time is ms since 1970-01-01 00:00:00.
        /// </remarks>
        /// <param name="highResolution">High resolution clock time.</param>
        /// <returns>Date and time.</returns>
        public static GXDateTime FromHighResolutionTime(UInt64 highResolution)
        {
            DateTime high = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return new GXDateTime(high.AddMilliseconds(highResolution).ToLocalTime());
        }

        /// <summary>
        /// Convert date time to Epoch time.
        /// </summary>
        /// <param name="date">Date and time.</param>
        /// <returns>Unix time.</returns>
        public static long ToUnixTime(DateTime date)
        {
            if (date == DateTime.MinValue)
            {
                return 0;
            }
            if (date == DateTime.MaxValue)
            {
                return 0xFFFFFFFF;
            }
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(date.ToUniversalTime() - epoch).TotalSeconds;
        }

        /// <summary>
        /// Convert date time to Epoch time.
        /// </summary>
        /// <param name="date">Date and time.</param>
        /// <returns>Unix time.</returns>
        public static long ToUnixTime(GXDateTime date)
        {
            return ToUnixTime(date.Value.DateTime);
        }

        #region IConvertible Members

        TypeCode IConvertible.GetTypeCode()
        {
            return TypeCode.String;
        }

        bool IConvertible.ToBoolean(IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        byte IConvertible.ToByte(IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        char IConvertible.ToChar(IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        DateTime IConvertible.ToDateTime(IFormatProvider? provider)
        {
            return Value.LocalDateTime;
        }

        decimal IConvertible.ToDecimal(IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        double IConvertible.ToDouble(IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        short IConvertible.ToInt16(IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        int IConvertible.ToInt32(IFormatProvider? provider)
        {
            return (int)GXDateTime.ToUnixTime(this.Value.DateTime);
        }

        long IConvertible.ToInt64(IFormatProvider? provider)
        {
            return (long)GXDateTime.ToUnixTime(this.Value.DateTime);
        }

        sbyte IConvertible.ToSByte(IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        float IConvertible.ToSingle(IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        string IConvertible.ToString(IFormatProvider? provider)
        {
            return ToString();
        }

        object IConvertible.ToType(Type conversionType, IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        ushort IConvertible.ToUInt16(IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        uint IConvertible.ToUInt32(IFormatProvider? provider)
        {
            return (uint)GXDateTime.ToUnixTime(this.Value.DateTime);
        }

        ulong IConvertible.ToUInt64(IFormatProvider? provider)
        {
            return (ulong)GXDateTime.ToUnixTime(this.Value.DateTime);
        }

        #endregion

        /// <summary>
        /// Compare to date time.
        /// </summary>
        /// <param name="value">Date and time.</param>
        /// <returns>Zero if values are equal, -1 if value is bigger and - if value is smaller.</returns>
        public int Compare(DateTime value)
        {
            int ret = 0;
            DateTime localValue = Value.LocalDateTime;
            if ((Skip & DateTimeSkips.Year) == 0 &&
                localValue.Year != value.Year)
            {
                ret = localValue.Year < value.Year ? -1 : 1;
            }
            else if ((Skip & DateTimeSkips.Month) == 0 &&
                localValue.Month != value.Month)
            {
                ret = localValue.Month < value.Month ? -1 : 1;
            }
            else if ((Skip & DateTimeSkips.Day) == 0 &&
                localValue.Day != value.Day)
            {
                ret = localValue.Day < value.Day ? -1 : 1;
            }
            else if ((Skip & DateTimeSkips.Hour) == 0 &&
                localValue.Hour != value.Hour)
            {
                ret = localValue.Hour < value.Hour ? -1 : 1;
            }
            else if ((Skip & DateTimeSkips.Minute) == 0 &&
                localValue.Minute != value.Minute)
            {
                ret = localValue.Minute < value.Minute ? -1 : 1;
            }
            else if ((Skip & DateTimeSkips.Second) == 0 &&
                localValue.Second != value.Second)
            {
                ret = localValue.Second < value.Second ? -1 : 1;
            }
            return ret;
        }
    }
}
