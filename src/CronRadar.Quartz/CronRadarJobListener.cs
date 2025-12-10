using System;
using System.Threading;
using System.Threading.Tasks;
using Quartz;

namespace CronRadar.Quartz
{
    /// <summary>
    /// Job listener that automatically tracks job lifecycle for CronRadar monitoring.
    /// Intercepts all job executions and sends start/complete/fail signals.
    /// </summary>
    internal class CronRadarJobListener : IJobListener
    {
        private readonly string? _apiKey;

        public CronRadarJobListener(string? apiKey)
        {
            _apiKey = apiKey;
        }

        public string Name => "CronRadar.Quartz.JobListener";

        /// <summary>
        /// Called when a job is about to be executed.
        /// Signals job start for lifecycle tracking with auto-registration.
        /// </summary>
        public async Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var jobType = context.JobDetail.JobType;
                var jobKey = context.JobDetail.Key;

                // Check for [SkipMonitor] attribute
                var skipAttr = jobType.GetCustomAttributes(typeof(SkipMonitorAttribute), false);
                if (skipAttr.Length > 0)
                {
                    if (CronRadar.DebugLoggingEnabled)
                    {
                        Console.WriteLine($"[CronRadar.Quartz] Skipping job {jobKey} due to [SkipMonitor] attribute");
                    }
                    return;
                }

                // Get monitor key from attribute or use normalized JobKey
                string monitorKey = GetMonitorKey(jobType, jobKey);
                if (string.IsNullOrWhiteSpace(monitorKey))
                    return;

                // Get schedule from trigger for auto-registration
                string? schedule = GetScheduleFromTrigger(context.Trigger);

                if (CronRadar.DebugLoggingEnabled && schedule != null)
                {
                    Console.WriteLine($"[CronRadar.Quartz] Starting job {monitorKey} with schedule {schedule}");
                }

                // Signal job start - API will auto-register if monitor doesn't exist
                await CronRadar.StartJobAsync(monitorKey, schedule, _apiKey);
            }
            catch (Exception ex)
            {
                // Never break job execution
                if (CronRadar.DebugLoggingEnabled)
                {
                    Console.WriteLine($"[CronRadar.Quartz] Error in JobToBeExecuted: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Called when a job execution was vetoed (prevented from executing).
        /// </summary>
        public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called after a job has been executed.
        /// Signals job completion or failure for lifecycle tracking.
        /// </summary>
        public async Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
        {
            try
            {
                var jobType = context.JobDetail.JobType;
                var jobKey = context.JobDetail.Key;

                // Check for [SkipMonitor] attribute
                var skipAttr = jobType.GetCustomAttributes(typeof(SkipMonitorAttribute), false);
                if (skipAttr.Length > 0)
                    return;

                // Get monitor key
                string monitorKey = GetMonitorKey(jobType, jobKey);
                if (string.IsNullOrWhiteSpace(monitorKey))
                    return;

                // Report success or failure
                if (jobException != null)
                {
                    if (CronRadar.DebugLoggingEnabled)
                    {
                        Console.WriteLine($"[CronRadar.Quartz] Job {monitorKey} failed: {jobException.Message}");
                    }
                    await CronRadar.FailJobAsync(monitorKey, jobException.Message, _apiKey);
                }
                else
                {
                    if (CronRadar.DebugLoggingEnabled)
                    {
                        Console.WriteLine($"[CronRadar.Quartz] Job {monitorKey} completed successfully");
                    }
                    await CronRadar.CompleteJobAsync(monitorKey, _apiKey);
                }
            }
            catch (Exception ex)
            {
                // Never break job execution
                if (CronRadar.DebugLoggingEnabled)
                {
                    Console.WriteLine($"[CronRadar.Quartz] Error in JobWasExecuted: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets the monitor key from attribute or normalized job key.
        /// </summary>
        private string GetMonitorKey(Type jobType, JobKey jobKey)
        {
            // Check for [CronRadar("custom-key")] attribute
            var cronRadarAttr = jobType.GetCustomAttributes(typeof(CronRadarAttribute), false);
            if (cronRadarAttr.Length > 0)
            {
                var attr = (CronRadarAttribute)cronRadarAttr[0];
                if (!string.IsNullOrWhiteSpace(attr.MonitorKey))
                {
                    return CronRadarKeyNormalizer.Normalize(attr.MonitorKey);
                }
            }

            // Use normalized JobKey
            return CronRadarKeyNormalizer.Normalize(jobKey.Group, jobKey.Name);
        }

        /// <summary>
        /// Extracts schedule from trigger for auto-registration.
        /// </summary>
        private string? GetScheduleFromTrigger(ITrigger trigger)
        {
            try
            {
                // Cron trigger - use the cron expression directly
                if (trigger is ICronTrigger cronTrigger)
                {
                    return cronTrigger.CronExpressionString;
                }

                // Simple trigger - convert to approximate cron
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
        /// Handles common intervals (minutes, hours, days, weeks) and generates best-fit cron expressions.
        /// </summary>
        private string ConvertIntervalToCron(TimeSpan interval)
        {
            // Sub-minute intervals - run every minute
            if (interval.TotalMinutes < 1)
            {
                return "* * * * *";
            }

            // Minute-based intervals
            if (interval.TotalMinutes < 60)
            {
                var minutes = (int)interval.TotalMinutes;
                // Check if it's a clean divisor of 60
                if (60 % minutes == 0)
                {
                    return $"*/{minutes} * * * *";
                }
                // Otherwise, run every N minutes (may not align perfectly)
                return $"*/{minutes} * * * *";
            }

            // Hour-based intervals
            if (interval.TotalHours < 24)
            {
                var hours = (int)interval.TotalHours;
                // Check if it's a clean divisor of 24
                if (24 % hours == 0)
                {
                    return $"0 */{hours} * * *";
                }
                // Fractional hours - convert to minutes
                var totalMinutes = (int)interval.TotalMinutes;
                return $"*/{totalMinutes} * * * *";
            }

            // Day-based intervals
            if (interval.TotalDays < 7)
            {
                var days = (int)interval.TotalDays;
                if (days == 1)
                {
                    return "0 0 * * *";  // Daily at midnight
                }
                // Multiple days: run every N days at midnight
                // Note: Standard cron doesn't support "every N days", so we approximate
                return $"0 0 */{days} * *";
            }

            // Week-based intervals
            if (interval.TotalDays % 7 == 0)
            {
                var weeks = (int)(interval.TotalDays / 7);
                if (weeks == 1)
                {
                    return "0 0 * * 0";  // Weekly on Sunday at midnight
                }
                // Multiple weeks: approximate with monthly
                return "0 0 1 * *";  // Monthly on 1st at midnight
            }

            // Default: monthly
            return "0 0 1 * *";  // Monthly on 1st at midnight
        }
    }
}
