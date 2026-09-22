# Gurux Scheduling for .NET

`Gurux.Scheduling.Net` provides culture-aware date-time schedules for .NET 10.\
It supports fixed date-times, wildcard schedule fields, localized weekday schedules,\
and invariant JSON serialization.

## Features

- Culture-aware `GXDateTime` parsing and formatting.
- Wildcards for year, month, day, hour, minute, and second.
- Localized weekday schedules such as `Monday/*/* 12:00:00 PM`, `Montag/*/* 12:00:00`, and `maanantai/*/* 12.00.00`.
- Next-occurrence calculation with `GXDateTime.GetNextScheduledDates`.
- Invariant `System.Text.Json` converter for portable schedule storage.
- Daylight-saving-time coverage for UK transitions.

## Requirements

- .NET 10 SDK or runtime.

## Install

Add the package to your application when it is available from your configured NuGet feed:

```bash
dotnet add package Gurux.Scheduling.Net
```

## Localized schedules

Construct `GXDateTime` with the culture that matches the source expression.

```csharp
using System.Globalization;
using Gurux.Scheduling;

var usSchedule = new GXDateTime(
    "Monday/*/* 12:00:00 PM",
    CultureInfo.GetCultureInfo("en-US"));

var finnishSchedule = new GXDateTime(
    "maanantai/*/* 12.00.00",
    CultureInfo.GetCultureInfo("fi-FI"));
```

The weekday schedule format is:

```text
weekday/month/year time
```

`*` means any value. For example, `Mon/12/* 12:00:00 PM` runs every Monday in December at noon.\
Full and abbreviated weekday names are accepted without regard to letter case.

## Find the next occurrence

Use a fixed current time when calculating schedule occurrences in application logic or tests.

```csharp
var currentTime = new DateTime(2026, 6, 16, 11, 0, 0);
DateTime next = GXDateTime
    .GetNextScheduledDates(currentTime, usSchedule, 1)
    .Single();

// 2026-06-22 12:00:00
```

`GetTimeUntilNextSchedule` uses the system clock and returns the wait time until the next occurrence.

## JSON serialization

`GXDateTimeJsonConverter` stores schedules as an invariant string. A localized expression is normalized during serialization, so it can be deserialized without its original culture.

`GXDateTime` is annotated with the converter, so no `JsonSerializerOptions` registration is required. The annotation uses the concrete converter type:

```csharp
[JsonConverter(typeof(GXDateTimeJsonConverter))]
public class GXDateTime
```

```csharp
using System.Text.Json;
using Gurux.Scheduling;

string json = JsonSerializer.Serialize(finnishSchedule);
// "Monday/*/* 12:00:00"

GXDateTime restored = JsonSerializer.Deserialize<GXDateTime>(json)!;
```

`[JsonConverter(typeof(JsonConverter<GXDateTime>))]` is not valid for this purpose: `JsonConverter<GXDateTime>` is an abstract base class and cannot be instantiated by the serializer. Registering `GXDateTimeJsonConverter` in `JsonSerializerOptions` remains possible when an application prefers global configuration, but is redundant for `GXDateTime`.

The JSON converter does not configure XML serialization.\
For XML, persist the invariant `ToFormatString(CultureInfo.InvariantCulture)` value in a string element and reconstruct it with `new GXDateTime(value, CultureInfo.InvariantCulture)`.

## License

This project is licensed under the [GNU General Public License v2.0](https://www.gnu.org/licenses/old-licenses/gpl-2.0.html) only.

## Links

- [Gurux](https://www.gurux.fi/)
- [Project repository](https://github.com/gurux/gurux.scheduling.net)
