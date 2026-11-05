using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Quartz;

namespace CronRadar.Quartz
{
    /// <summary>
    /// Shared logic for deriving a monitor key and a cron schedule from a Quartz job/trigger.
    /// Used by both the execution listener (telemetry) and the reconciler (declaration).
    /// </summary>
    internal static class CronRadarQuartzSchedule
    {
        private const int QuartzSunday = 1;
        private const int QuartzSaturday = 7;
        private static readonly string[] QuartzDayNames = { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" };
        private static readonly Regex NumericField = new Regex(@"^(\*|\d+(-\d+)?)?(/\d+)?$", RegexOptions.CultureInvariant);
        private static readonly Regex MonthField = new Regex(@"^(\*|(\d+|[A-Z]{3})(-(\d+|[A-Z]{3}))?)?(/\d+)?$", RegexOptions.CultureInvariant);
        private static readonly Regex DayOfWeekPart = new Regex(@"^(\*|(\d|[A-Z]{3})(-(\d|[A-Z]{3}))?)?(/(\d+))?$", RegexOptions.CultureInvariant);
        // A job fires on every run, so warn once per expression rather than once per run.
        private static readonly ConcurrentDictionary<string, byte> WarnedExpressions = new ConcurrentDictionary<string, byte>();

        /// <summary>
        /// The job's CLR type. Quartz 4 wraps it in a JobType value; Quartz 3 exposes it directly.
        /// </summary>
        public static Type JobClrType(IJobDetail jobDetail)
        {
#if QUARTZ4
            return jobDetail.JobType.Type;
#else
            return jobDetail.JobType;
#endif
        }

        /// <summary>
        /// Gets the monitor key from [CronRadar("key")] or the normalized JobKey.
        /// </summary>
        public static string GetMonitorKey(Type jobType, JobKey jobKey)
        {
            var cronRadarAttr = jobType.GetCustomAttributes(typeof(CronRadarAttribute), false);
            if (cronRadarAttr.Length > 0)
            {
                var attr = (CronRadarAttribute)cronRadarAttr[0];
                if (!string.IsNullOrWhiteSpace(attr.MonitorKey))
                {
                    // non-null: checked by IsNullOrWhiteSpace above, which netstandard2.0 does not annotate
                    return CronRadarKeyNormalizer.Normalize(attr.MonitorKey!);
                }
            }

            return CronRadarKeyNormalizer.Normalize(jobKey.Group, jobKey.Name);
        }

        /// <summary>
        /// True if the job type opts out of monitoring via [SkipMonitor].
        /// </summary>
        public static bool ShouldSkip(Type jobType)
        {
            return jobType.GetCustomAttributes(typeof(SkipMonitorAttribute), false).Length > 0;
        }

        /// <summary>
        /// Extracts a cron schedule from a trigger for registration.
        /// </summary>
        public static string? GetScheduleFromTrigger(ITrigger trigger)
        {
            try
            {
                if (trigger is ICronTrigger cronTrigger)
                {
                    return TranslateOrWarn(cronTrigger.CronExpressionString);
                }

                if (trigger is ISimpleTrigger simpleTrigger && simpleTrigger.RepeatCount != 0)
                {
                    return ConvertIntervalToCron(simpleTrigger.RepeatInterval);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Converts a Quartz cron expression to the five-field cron CronRadar accepts,
        /// or returns null when five-field cron cannot express the same schedule.
        /// </summary>
        public static string? TranslateCronExpression(string? expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return null;

            var fields = expression!.Trim().ToUpperInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 6 && fields.Length != 7)
                return null;

            var isFixedSecond = int.TryParse(fields[0], out var second) && second >= 0 && second <= 59;
            var isEveryYear = fields.Length == 6 || fields[6] == "*";
            if (!isFixedSecond || !isEveryYear)
                return null;

            var minute = TranslateField(fields[1], NumericField, firstValue: 0);
            var hour = TranslateField(fields[2], NumericField, firstValue: 0);
            var dayOfMonth = fields[3] == "?" ? "*" : TranslateField(fields[3], NumericField, firstValue: 1);
            var month = TranslateField(fields[4], MonthField, firstValue: 1);
            var dayOfWeek = fields[5] == "?" ? "*" : TranslateDayOfWeek(fields[5]);
            if (minute == null || hour == null || dayOfMonth == null || month == null || dayOfWeek == null)
                return null;

            return $"{minute} {hour} {dayOfMonth} {month} {dayOfWeek}";
        }

        private static string? TranslateOrWarn(string? expression)
        {
            var translated = TranslateCronExpression(expression);
            if (translated == null && expression != null && WarnedExpressions.TryAdd(expression, 0))
            {
                Console.Error.WriteLine(
                    $"[CronRadar.Quartz] Warning: cron expression \"{expression}\" is not translatable to five-field cron, so this job is not monitored.");
            }

            return translated;
        }

        private static string? TranslateField(string field, Regex format, int firstValue)
        {
            var parts = field.Split(',');
            if (parts.Any(part => part.Length == 0 || !format.IsMatch(part)))
                return null;

            return string.Join(",", parts.Select(part =>
            {
                // "/15" and "0/15" both mean every 15 from the first value, which is "*/15" in cron.
                if (part[0] == '/')
                    return "*" + part;
                return part.StartsWith(firstValue + "/", StringComparison.Ordinal)
                    ? "*" + part.Substring(part.IndexOf('/'))
                    : part;
            }));
        }

        /// <summary>
        /// Quartz numbers weekdays 1-7 from Sunday and cron numbers them 0-6, so every value is
        /// expanded and renumbered. Expanding also handles Quartz ranges that wrap, such as FRI-MON.
        /// </summary>
        private static string? TranslateDayOfWeek(string field)
        {
            var quartzDays = new SortedSet<int>();
            foreach (var part in field.Split(','))
            {
                var match = DayOfWeekPart.Match(part);
                if (part.Length == 0 || !match.Success)
                    return null;

                int first = QuartzSunday, last = QuartzSaturday;
                if (match.Groups[2].Success)
                {
                    var start = ParseQuartzDay(match.Groups[2].Value);
                    if (start == null)
                        return null;
                    first = start.Value;
                    var hasStep = match.Groups[6].Success;
                    var end = match.Groups[4].Success ? ParseQuartzDay(match.Groups[4].Value) : (hasStep ? QuartzSaturday : first);
                    if (end == null)
                        return null;
                    last = end.Value;
                }

                var step = match.Groups[6].Success ? int.Parse(match.Groups[6].Value) : 1;
                if (step < 1)
                    return null;

                var span = (last - first + 7) % 7;
                for (var offset = 0; offset <= span; offset += step)
                    quartzDays.Add((first - 1 + offset) % 7 + 1);
            }

            if (quartzDays.Count == 7)
                return "*";

            return FormatCronDays(quartzDays.Select(day => day - 1).ToList());
        }

        private static int? ParseQuartzDay(string value)
        {
            if (int.TryParse(value, out var number))
                return number >= QuartzSunday && number <= QuartzSaturday ? number : (int?)null;

            var index = Array.IndexOf(QuartzDayNames, value);
            return index >= 0 ? index + 1 : (int?)null;
        }

        private static string FormatCronDays(List<int> days)
        {
            var parts = new List<string>();
            for (var start = 0; start < days.Count;)
            {
                var end = start;
                while (end + 1 < days.Count && days[end + 1] == days[end] + 1)
                    end++;
                parts.Add(end == start ? days[start].ToString() : $"{days[start]}-{days[end]}");
                start = end + 1;
            }

            return string.Join(",", parts);
        }

        /// <summary>
        /// Converts a TimeSpan interval to an approximate cron expression.
        /// </summary>
        public static string ConvertIntervalToCron(TimeSpan interval)
        {
            if (interval.TotalMinutes < 1)
            {
                return "* * * * *";
            }

            if (interval.TotalMinutes < 60)
            {
                var minutes = (int)interval.TotalMinutes;
                return $"*/{minutes} * * * *";
            }

            if (interval.TotalHours < 24)
            {
                var hours = (int)interval.TotalHours;
                if (24 % hours == 0)
                {
                    return $"0 */{hours} * * *";
                }
                var totalMinutes = (int)interval.TotalMinutes;
                return $"*/{totalMinutes} * * * *";
            }

            if (interval.TotalDays < 7)
            {
                var days = (int)interval.TotalDays;
                if (days == 1)
                {
                    return "0 0 * * *";
                }
                return $"0 0 */{days} * *";
            }

            if (interval.TotalDays % 7 == 0)
            {
                var weeks = (int)(interval.TotalDays / 7);
                if (weeks == 1)
                {
                    return "0 0 * * 0";
                }
                return "0 0 1 * *";
            }

            return "0 0 1 * *";
        }
    }
}
