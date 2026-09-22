using System.Globalization;
using System.Text.Json;
using Xunit;
using Gurux.Scheduling;
using Gurux.Scheduling.Enums;
using System.Collections.Generic;
using System.Linq;

namespace Gurux.Scheduling.Net.Tests;

public class GXDateTimeTests
{
    [Fact]
    public void JsonConverterRoundTripsLocalizedWeekdayScheduleUsingInvariantCulture()
    {
        // Would fail if JSON lost the weekday restriction or depended on the source culture.
        JsonSerializerOptions options = new();
        options.Converters.Add(new GXDateTimeJsonConverter());
        GXDateTime schedule = new("maanantai/*/* 12.00.00", CultureInfo.GetCultureInfo("fi-FI"));

        string json = JsonSerializer.Serialize(schedule, options);
        GXDateTime restored = JsonSerializer.Deserialize<GXDateTime>(json, options)!;
        DateTime next = GXDateTime.GetNextScheduledDates(new DateTime(2026, 6, 16, 11, 0, 0), restored, 1).Single();

        Assert.Equal("\"Monday/*/* 12:00:00\"", json);
        Assert.Equal(new DateTime(2026, 6, 22, 12, 0, 0), next);
    }

    [Fact]
    public void JsonConverterRejectsNonStringJsonToken()
    {
        // Would fail if malformed JSON silently created a default schedule.
        JsonSerializerOptions options = new();
        options.Converters.Add(new GXDateTimeJsonConverter());

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GXDateTime>("42", options));
    }

    public static IEnumerable<object[]> LocalizedWeekdaySchedules()
    {
        yield return ["en-US", "Monday/*/* 12:00:00 PM", new DateTime(2026, 6, 15, 11, 0, 0), new DateTime(2026, 6, 15, 12, 0, 0)];
        yield return ["de-DE", "Montag/*/* 12:00:00", new DateTime(2026, 6, 15, 11, 0, 0), new DateTime(2026, 6, 15, 12, 0, 0)];
        yield return ["fi-FI", "maanantai/*/* 12.00.00", new DateTime(2026, 6, 15, 11, 0, 0), new DateTime(2026, 6, 15, 12, 0, 0)];
        yield return ["en-US", "mOnDaY/*/* 12:00:00 PM", new DateTime(2026, 6, 16, 11, 0, 0), new DateTime(2026, 6, 22, 12, 0, 0)];
        yield return ["en-US", "Mon/12/* 12:00:00 PM", new DateTime(2026, 11, 30, 13, 0, 0), new DateTime(2026, 12, 7, 12, 0, 0)];
    }

    [Theory]
    [MemberData(nameof(LocalizedWeekdaySchedules))]
    public void LocalizedWeekdayScheduleReturnsNextMatchingOccurrence(string cultureName, string cron, DateTime currentTime, DateTime expected)
    {
        // Would fail if a localized weekday was parsed as a date field or was ignored during scheduling.
        GXDateTime schedule = new(cron, CultureInfo.GetCultureInfo(cultureName));

        DateTime next = GXDateTime.GetNextScheduledDates(currentTime, schedule, 1).Single();

        Assert.Equal(expected, next);
    }

    [Fact]
    public void LocalizedWeekdayScheduleRejectsUnknownWeekday()
    {
        // Would fail if an unknown weekday silently created an unconstrained schedule.
        CultureInfo culture = CultureInfo.GetCultureInfo("en-US");

        Assert.Throws<FormatException>(() => new GXDateTime("Funday/*/* 12:00:00 PM", culture));
    }

    [Fact]
    public void DoesNotExposeNumericDayOfWeekProperty()
    {
        // Would fail if the removed public numeric scheduling API was restored.
        Assert.Null(typeof(GXDateTime).GetProperty("DayOfWeek"));
    }

    [Fact]
    public void LocalizedWeekdayScheduleKeepsFixedYearWithTwoDigitYearCulture()
    {
        // Would fail if localized weekday parsing inferred date fields from formatted sample digits.
        CultureInfo culture = (CultureInfo)CultureInfo.GetCultureInfo("en-GB").Clone();
        culture.DateTimeFormat.ShortDatePattern = "dd/MM/yy";
        GXDateTime schedule = new("Monday/12/26 12:00:00", culture);

        DateTime next = GXDateTime.GetNextScheduledDates(new DateTime(2026, 11, 30, 13, 0, 0), schedule, 1).Single();

        Assert.False(schedule.Skip.HasFlag(DateTimeSkips.Year));
        Assert.Equal(new DateTime(2026, 12, 7, 12, 0, 0), next);
    }

    [Fact]
    public void LocalizedWeekdayScheduleRoundTripsThroughFormatString()
    {
        // Would fail if formatting discarded the weekday restriction and reparsing scheduled every day.
        CultureInfo culture = CultureInfo.GetCultureInfo("en-US");
        GXDateTime original = new("Monday/*/* 12:00:00 PM", culture);
        GXDateTime reparsed = new(original.ToFormatString(culture), culture);

        DateTime next = GXDateTime.GetNextScheduledDates(new DateTime(2026, 6, 16, 11, 0, 0), reparsed, 1).Single();

        Assert.Equal("Monday/*/* 12:00:00 PM", original.ToFormatString(culture));
        Assert.Equal(new DateTime(2026, 6, 22, 12, 0, 0), next);
    }

    [Fact]
    public void LocalizedWeekdayParsingKeepsSpecialDateTokens()
    {
        // Would fail if existing BEGIN and LASTDAY tokens were mistaken for unknown weekdays.
        GXDateTime dstBegin = new("BEGIN/1/* 12:00:00 AM", CultureInfo.GetCultureInfo("en-US"));
        GXDateTime lastDay = new("LASTDAY/12/* 12:00:00", CultureInfo.GetCultureInfo("en-GB"));

        Assert.True(dstBegin.Extra.HasFlag(DateTimeExtraInfo.DstBegin));
        Assert.True(lastDay.Extra.HasFlag(DateTimeExtraInfo.LastDay));
    }

    public static IEnumerable<object[]> CultureDateTimes()
    {
        yield return ["en-GB", "31/12/2026 23:45:30", "31/12/2026 23:45:30"];
        yield return ["de-DE", "31.12.2026 23:45:30", "31.12.2026 23:45:30"];
        yield return ["it-IT", "31/12/2026 23:45:30", "31/12/2026 23:45:30"];
        yield return ["sv-SE", "2026-12-31 23:45:30", "2026-12-31 23:45:30"];
        yield return ["en-IN", "31/12/2026 11:45:30 PM", "31/12/2026 11:45:30 pm"];
    }

    [Theory]
    [MemberData(nameof(CultureDateTimes))]
    public void ParsesAndFormatsCultureSpecificDateTime(string cultureName, string dateTime, string expectedFormat)
    {
        // Would fail if a culture's date order, separator, or 12/24-hour clock was parsed incorrectly.
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);

        GXDateTime value = new(dateTime, culture);

        Assert.Equal(2026, value.Value.Year);
        Assert.Equal(12, value.Value.Month);
        Assert.Equal(31, value.Value.Day);
        Assert.Equal(23, value.Value.Hour);
        Assert.Equal("45", value.Value.Minute.ToString("00", CultureInfo.InvariantCulture));
        Assert.Equal(expectedFormat, value.ToString(culture));
    }

    public static IEnumerable<object[]> WildcardSchedules()
    {
        yield return ["15/*/2026 23:45:30", DateTimeSkips.Month, new DateTime(2026, 6, 16, 0, 0, 0), new DateTime(2026, 7, 15, 23, 45, 30)];
        yield return ["*/12/2026 23:45:30", DateTimeSkips.Day, new DateTime(2026, 12, 15, 23, 50, 0), new DateTime(2026, 12, 16, 23, 45, 30)];
        yield return ["15/12/* 23:45:30", DateTimeSkips.Year, new DateTime(2026, 12, 16, 0, 0, 0), new DateTime(2027, 12, 15, 23, 45, 30)];
        yield return ["15/12/2026 *:45:30", DateTimeSkips.Hour, new DateTime(2026, 12, 15, 15, 50, 0), new DateTime(2026, 12, 15, 16, 45, 30)];
        yield return ["15/12/2026 23:*:30", DateTimeSkips.Minute, new DateTime(2026, 12, 15, 22, 59, 40), new DateTime(2026, 12, 15, 23, 0, 30)];
    }

    [Theory]
    [MemberData(nameof(WildcardSchedules))]
    public void WildcardFieldSchedulesNextMatchingOccurrence(string cron, DateTimeSkips skippedField, DateTime currentTime, DateTime expected)
    {
        // Would fail if one wildcard field was treated as a fixed value or ignored the remaining fixed fields.
        GXDateTime schedule = new(cron, CultureInfo.GetCultureInfo("en-GB"));

        DateTime next = GXDateTime.GetNextScheduledDates(currentTime, schedule, 1).Single();

        Assert.True(schedule.Skip.HasFlag(skippedField));
        Assert.Equal(expected, next);
    }

    [Fact]
    public void PreservesBritishSummerTimeOffsetAfterSpringTransition()
    {
        // Would fail if the UTC-to-local conversion kept standard time after DST begins.
        TimeZoneInfo london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        DateTimeOffset utc = new(2026, 3, 29, 1, 30, 0, TimeSpan.Zero);

        GXDateTime value = new(TimeZoneInfo.ConvertTime(utc, london));

        Assert.Equal(2, value.Value.Hour);
        Assert.Equal(TimeSpan.FromHours(1), value.Value.Offset);
    }

    [Fact]
    public void PreservesBritishStandardTimeOffsetAfterAutumnTransition()
    {
        // Would fail if the UTC-to-local conversion kept daylight saving time after it ends.
        TimeZoneInfo london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        DateTimeOffset utc = new(2026, 10, 25, 1, 30, 0, TimeSpan.Zero);

        GXDateTime value = new(TimeZoneInfo.ConvertTime(utc, london));

        Assert.Equal(1, value.Value.Hour);
        Assert.Equal(TimeSpan.Zero, value.Value.Offset);
    }

    public static IEnumerable<object[]> CronSchedules()
    {
        // A change that ignores a fixed cron field or fails to carry into the next period must fail one of these cases.
        yield return ["*/*/* 12:00:00 AM", new DateTime(2026, 6, 15, 23, 59, 30), new DateTime(2026, 6, 16, 0, 0, 0)];
        yield return ["*/1/* 12:*:00 AM", new DateTime(2026, 6, 15, 23, 59, 30), new DateTime(2026, 7, 1, 0, 0, 0)];
        yield return ["*/*/* 12:30:00 AM", new DateTime(2026, 6, 15, 23, 45, 0), new DateTime(2026, 6, 16, 0, 30, 0)];
        yield return ["*/*/* 12:*:00 AM", new DateTime(2026, 6, 15, 23, 59, 30), new DateTime(2026, 6, 16, 0, 0, 0)];
        yield return ["*/*/* *:15:00 AM", new DateTime(2026, 6, 15, 10, 45, 0), new DateTime(2026, 6, 15, 11, 15, 0)];
        yield return ["*/*/* *:*:10 AM", new DateTime(2026, 6, 15, 10, 15, 45), new DateTime(2026, 6, 15, 10, 16, 10)];
        yield return ["*/*/* 12:00:00 PM", new DateTime(2026, 6, 15, 11, 59, 30), new DateTime(2026, 6, 15, 12, 0, 0)];
        yield return ["12/31/* 12:00:00 AM", new DateTime(2026, 12, 30, 23, 59, 30), new DateTime(2026, 12, 31, 0, 0, 0)];
        yield return ["1/1/* 12:00:00 AM", new DateTime(2026, 12, 31, 0, 0, 1), new DateTime(2027, 1, 1, 0, 0, 0)];
        yield return ["2/29/* 12:00:00 AM", new DateTime(2027, 3, 1, 0, 0, 0), new DateTime(2028, 2, 29, 0, 0, 0)];
    }

    [Theory]
    [MemberData(nameof(CronSchedules))]
    public void CronScheduleReturnsTheNextMatchingOccurrence(string cron, DateTime currentTime, DateTime expected)
    {
        GXDateTime schedule = new(cron, CultureInfo.GetCultureInfo("en-US"));

        DateTime next = GXDateTime.GetNextScheduledDates(currentTime, schedule, 1).Single();

        Assert.Equal(expected, next);
    }

    [Fact]
    public void ParsesFinnishDateTimeAndFormatsItUsingTheSameCulture()
    {
        // Would fail if parsing selected a non-Finnish date order or formatting lost the time fields.
        CultureInfo culture = CultureInfo.GetCultureInfo("fi-FI");

        GXDateTime value = new("31.12.2026 23.45.30", culture);

        Assert.Equal(2026, value.Value.Year);
        Assert.Equal(12, value.Value.Month);
        Assert.Equal(31, value.Value.Day);
        Assert.Equal(23, value.Value.Hour);
        Assert.Equal(45, value.Value.Minute);
        Assert.Equal(30, value.Value.Second);
        Assert.Equal("31.12.2026 23.45.30", value.ToString(culture));
    }

    [Fact]
    public void ParsesAmericanDateTimeAndFormatsItUsingTheSameCulture()
    {
        // Would fail if parsing interpreted 11 PM as 11 AM or formatting changed the culture's time format.
        CultureInfo culture = CultureInfo.GetCultureInfo("en-US");

        GXDateTime value = new("12/31/2026 11:45:30 PM", culture);

        Assert.Equal(23, value.Value.Hour);
        Assert.Equal("12/31/2026 11:45:30 PM", value.ToString(culture));
    }

    [Fact]
    public void ParsesWildcardMinuteAndKeepsItInFormatString()
    {
        // Would fail if a skipped minute was treated as a literal minute in the format representation.
        CultureInfo culture = CultureInfo.GetCultureInfo("en-US");

        GXDateTime value = new("12/31/2026 11:*:30 PM", culture);

        Assert.True(value.Skip.HasFlag(DateTimeSkips.Minute));
        Assert.Equal("12/31/2026 11:*:30 PM", value.ToFormatString(culture));
    }

    [Fact]
    public void GetNextScheduledDatesReturnsNextOccurrenceForSecondOnlySchedule()
    {
        // Would fail if a past second stayed in the current minute instead of moving to the next one.
        DateTime currentTime = new(2026, 12, 31, 23, 59, 55);
        GXDateTime schedule = new(new DateTime(2000, 1, 1, 0, 0, 10))
        {
            Skip = DateTimeSkips.Year | DateTimeSkips.Month | DateTimeSkips.Day |
                   DateTimeSkips.Hour | DateTimeSkips.Minute | DateTimeSkips.Ms
        };

        DateTime next = GXDateTime.GetNextScheduledDates(currentTime, schedule, 1).Single();

        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 10), next);
    }

    [Fact]
    public void GetTimeUntilNextScheduleReturnsAWaitForTheUpcomingSecond()
    {
        // Would fail if the method returned a past occurrence or a duration unrelated to the schedule.
        DateTime before = DateTime.Now;
        int scheduledSecond = (before.Second + 5) % 60;
        GXDateTime schedule = new(new DateTime(2000, 1, 1, 0, 0, scheduledSecond))
        {
            Skip = DateTimeSkips.Year | DateTimeSkips.Month | DateTimeSkips.Day |
                   DateTimeSkips.Hour | DateTimeSkips.Minute | DateTimeSkips.Ms
        };

        TimeSpan wait = GXDateTime.GetTimeUntilNextSchedule(schedule);
        Assert.InRange(wait, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(6));
    }
}
