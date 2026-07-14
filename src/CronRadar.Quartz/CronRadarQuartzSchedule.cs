using System;
using Quartz;

namespace CronRadar.Quartz
{
    /// <summary>
    /// Shared logic for deriving a monitor key and a cron schedule from a Quartz job/trigger.
    /// Used by both the execution listener (telemetry) and the reconciler (declaration).
    /// </summary>
    internal static class CronRadarQuartzSchedule
    {
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
                    return CronRadarKeyNormalizer.Normalize(attr.MonitorKey);
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
                    return cronTrigger.CronExpressionString;
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
