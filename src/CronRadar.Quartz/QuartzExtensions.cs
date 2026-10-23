using System;
using System.Threading.Tasks;
using Quartz;

namespace CronRadar.Quartz
{
    /// <summary>
    /// Extension methods for simple CronRadar integration with Quartz.NET.
    /// Uses base SDK for all monitoring operations.
    /// </summary>
    public static class QuartzExtensions
    {
        private static bool _initialized = false;
        private static readonly object _initLock = new object();
        private static string? _apiKey = null;

        /// <summary>API key captured during initialization, used by the reconcile job.</summary>
        internal static string? ApiKey => _apiKey;

        /// <summary>How often the reconcile job re-declares the schedule (heartbeat), in minutes.</summary>
        private const int ReconcileIntervalMinutes = 15;

        /// <summary>
        /// Enables CronRadar monitoring for all Quartz.NET jobs automatically.
        /// Jobs are monitored using their normalized JobKey (group-name).
        /// Use [SkipMonitor] attribute to exclude specific jobs.
        ///
        /// Example:
        /// <code>
        /// IScheduler scheduler = await StdSchedulerFactory.GetDefaultScheduler();
        /// await scheduler.MonitorAll();
        /// await scheduler.Start();
        /// </code>
        /// </summary>
        /// <param name="scheduler">The Quartz.NET scheduler instance</param>
        /// <param name="apiKey">Optional API key (defaults to CRONRADAR_API_KEY env var)</param>
        /// <returns>The scheduler instance for method chaining</returns>
        public static async Task<IScheduler> MonitorAll(this IScheduler scheduler, string? apiKey = null)
        {
            if (scheduler == null)
            {
                Console.WriteLine("[CronRadar.Quartz] ERROR: scheduler parameter is null");
                // Only callers that ignore the non-nullable parameter reach this branch. They get their
                // null back as before, and the return stays non-nullable for every caller that complies.
                return scheduler!;
            }

            bool shouldInitialize = false;

            lock (_initLock)
            {
                if (_initialized)
                {
                    Console.WriteLine("[CronRadar.Quartz] Already initialized. Ignoring duplicate call.");
                    // Return immediately without holding the lock
                }
                else
                {
                    _initialized = true;
                    _apiKey = apiKey; // Store for listener to use
                    shouldInitialize = true;

                    if (CronRadar.DebugLoggingEnabled)
                    {
                        Console.WriteLine("[CronRadar.Quartz] Initializing MonitorAll mode - use [SkipMonitor] to opt-out");
                    }
                }
            }

            if (shouldInitialize)
            {
                try
                {
                    // 1. Execution telemetry: report start/complete/fail as jobs run.
                    scheduler.ListenerManager.AddJobListener(new CronRadarJobListener(_apiKey));

                    // 2. Declaration: reconcile the full schedule when the scheduler starts,
                    //    so jobs that have not run yet still get dead-man coverage.
                    scheduler.ListenerManager.AddSchedulerListener(new CronRadarSchedulerListener(scheduler, _apiKey));

                    // 3. Heartbeat: periodic reconcile so the server never treats a still
                    //    scheduled job as drifted, and late-added jobs are picked up.
                    var reconcileJob = JobBuilder.Create<CronRadarReconcileJob>()
                        .WithIdentity("cronradar-reconcile", "cronradar")
                        .Build();
                    var reconcileTrigger = TriggerBuilder.Create()
                        .WithIdentity("cronradar-reconcile-trigger", "cronradar")
                        .StartNow()
                        .WithSimpleSchedule(s => s.WithIntervalInMinutes(ReconcileIntervalMinutes).RepeatForever())
                        .Build();

                    if (!await scheduler.CheckExists(reconcileJob.Key).ConfigureAwait(false))
                    {
                        await scheduler.ScheduleJob(reconcileJob, reconcileTrigger).ConfigureAwait(false);
                    }

                    // If the scheduler is already running, SchedulerStarted won't fire again —
                    // reconcile immediately so coverage exists without waiting for the interval.
                    if (scheduler.IsStarted)
                    {
                        await CronRadarReconciler.ReconcileAsync(scheduler, _apiKey).ConfigureAwait(false);
                    }

                    if (CronRadar.DebugLoggingEnabled)
                    {
                        Console.WriteLine("[CronRadar.Quartz] ✓ Telemetry + reconcile registered successfully");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CronRadar.Quartz] Failed to initialize: {ex.Message}");
                    // Reset so user can retry
                    lock (_initLock)
                    {
                        _initialized = false;
                    }
                }
            }

            return scheduler;
        }
    }
}
