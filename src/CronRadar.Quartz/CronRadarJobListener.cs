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
        /// </summary>
        private string ConvertIntervalToCron(TimeSpan interval)
        {
            if (interval.TotalMinutes < 1) return "* * * * *";
            if (interval.TotalMinutes == 1) return "* * * * *";
            if (interval.TotalMinutes == 5) return "*/5 * * * *";
            if (interval.TotalMinutes == 15) return "*/15 * * * *";
            if (interval.TotalMinutes == 30) return "*/30 * * * *";
            if (interval.TotalHours == 1) return "0 * * * *";
            if (interval.TotalHours % 1 == 0 && interval.TotalHours < 24) return $"0 */{(int)interval.TotalHours} * * *";
            if (interval.TotalDays == 1) return "0 0 * * *";
            if (interval.TotalDays == 7) return "0 0 * * 0";
            return "0 * * * *"; // Default to hourly
        }
    }
}
