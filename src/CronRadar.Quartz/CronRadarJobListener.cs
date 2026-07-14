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
                if (CronRadarQuartzSchedule.ShouldSkip(jobType))
                {
                    if (CronRadar.DebugLoggingEnabled)
                    {
                        Console.WriteLine($"[CronRadar.Quartz] Skipping job {jobKey} due to [SkipMonitor] attribute");
                    }
                    return;
                }

                // Get monitor key from attribute or use normalized JobKey
                string monitorKey = CronRadarQuartzSchedule.GetMonitorKey(jobType, jobKey);
                if (string.IsNullOrWhiteSpace(monitorKey))
                    return;

                // Get schedule from trigger for auto-registration
                string? schedule = CronRadarQuartzSchedule.GetScheduleFromTrigger(context.Trigger);

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
                if (CronRadarQuartzSchedule.ShouldSkip(jobType))
                    return;

                // Get monitor key
                string monitorKey = CronRadarQuartzSchedule.GetMonitorKey(jobType, jobKey);
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

    }
}
