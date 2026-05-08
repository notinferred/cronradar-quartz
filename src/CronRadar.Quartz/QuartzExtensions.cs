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
                return scheduler;
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
                    // Register job listener for lifecycle tracking
                    var jobListener = new CronRadarJobListener(_apiKey);
                    await Task.Run(() => scheduler.ListenerManager.AddJobListener(jobListener)).ConfigureAwait(false);

                    if (CronRadar.DebugLoggingEnabled)
                    {
                        Console.WriteLine("[CronRadar.Quartz] ✓ Job listener registered successfully");
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
