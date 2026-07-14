using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl.Matchers;

namespace CronRadar.Quartz
{
    /// <summary>
    /// Declares all scheduled Quartz jobs to CronRadar (the authoritative set) so
    /// dead-man coverage exists before a job ever runs. Separate from execution
    /// telemetry (the job listener) — this is declaration, not run reporting.
    /// </summary>
    internal static class CronRadarReconciler
    {
        private const string Source = "quartz";
        private const string Version = "0.1.0";

        public static async Task ReconcileAsync(IScheduler scheduler, string? apiKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var monitors = new List<MonitorDefinition>();

                var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken).ConfigureAwait(false);

                foreach (var jobKey in jobKeys)
                {
                    var detail = await scheduler.GetJobDetail(jobKey, cancellationToken).ConfigureAwait(false);
                    if (detail == null || CronRadarQuartzSchedule.ShouldSkip(detail.JobType))
                        continue;

                    var triggers = await scheduler.GetTriggersOfJob(jobKey, cancellationToken).ConfigureAwait(false);

                    string? schedule = null;
                    foreach (var trigger in triggers)
                    {
                        schedule = CronRadarQuartzSchedule.GetScheduleFromTrigger(trigger);
                        if (!string.IsNullOrWhiteSpace(schedule))
                            break;
                    }

                    if (string.IsNullOrWhiteSpace(schedule))
                        continue;

                    monitors.Add(new MonitorDefinition
                    {
                        Key = CronRadarQuartzSchedule.GetMonitorKey(detail.JobType, jobKey),
                        Schedule = schedule
                    });
                }

                await CronRadar.SyncMonitorsAsync(monitors, Source, Version, apiKey).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Never break the scheduler
                if (CronRadar.DebugLoggingEnabled)
                {
                    Console.WriteLine($"[CronRadar.Quartz] Reconcile error: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Quartz job that periodically re-declares the schedule (heartbeat), so the server
    /// never treats a still-scheduled job as drifted. Excluded from monitoring itself.
    /// </summary>
    [SkipMonitor]
    [DisallowConcurrentExecution]
    public class CronRadarReconcileJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            return CronRadarReconciler.ReconcileAsync(context.Scheduler, QuartzExtensions.ApiKey, context.CancellationToken);
        }
    }
}
