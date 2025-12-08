using System;
using System.Threading;
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
        private static CancellationTokenSource? _watcherCts;

        /// <summary>
        /// Enables CronRadar monitoring for all Quartz.NET jobs automatically.
        /// Jobs are monitored using their JobKey (Group.Name).
        /// Monitors are synced automatically as jobs are scheduled.
        /// Zero configuration required - just add this line!
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
        public static async Task<IScheduler> MonitorAll(this IScheduler scheduler, string apiKey = null)
        {
            if (scheduler == null)
            {
                Console.WriteLine("[CronRadar.Quartz] ERROR: scheduler parameter is null - cannot initialize monitoring");
                return null;
            }

            lock (_initLock)
            {
                if (_initialized)
                {
                    Console.WriteLine("[CronRadar.Quartz] Already initialized, skipping...");
                    return scheduler;
                }

                _initialized = true;

                // Store API key in environment for base SDK to use
                if (!string.IsNullOrEmpty(apiKey))
                {
                    Environment.SetEnvironmentVariable("CRONRADAR_API_KEY", apiKey, EnvironmentVariableTarget.Process);
                }

                Console.WriteLine("[CronRadar.Quartz] Initializing CronRadar monitoring...");
            }

            try
            {
                // Register job listener to ping on successful execution
                var jobListener = new CronRadarJobListener();
                scheduler.ListenerManager.AddJobListener(jobListener);
                Console.WriteLine("[CronRadar.Quartz] Job listener registered");

                // Register scheduler listener to sync monitors when jobs are scheduled
                var schedulerListener = new CronRadarSchedulerListener();
                scheduler.ListenerManager.AddSchedulerListener(schedulerListener);
                Console.WriteLine("[CronRadar.Quartz] Scheduler listener registered");

                // Start background watcher to discover existing jobs
                _watcherCts = new CancellationTokenSource();
                var watcher = new CronRadarJobWatcher(scheduler);

                // Run watcher in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await watcher.WatchForJobsAsync(_watcherCts.Token);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CronRadar.Quartz] Watcher stopped: {ex.Message}");
                    }
                }, _watcherCts.Token);

                Console.WriteLine("[CronRadar.Quartz] Job watcher started");
                Console.WriteLine("[CronRadar.Quartz] ✓ CronRadar monitoring enabled successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CronRadar.Quartz] Failed to initialize: {ex.Message}");
                // Reset initialization flag so user can retry
                lock (_initLock)
                {
                    _initialized = false;
                }
                // Silent failure - don't throw to user code
            }

            return scheduler;
        }

        /// <summary>
        /// Synchronous version of MonitorAll for scenarios where async is not available.
        /// Note: It's recommended to use the async version when possible.
        /// </summary>
        /// <param name="scheduler">The Quartz.NET scheduler instance</param>
        /// <param name="apiKey">Optional API key (defaults to CRONRADAR_API_KEY env var)</param>
        /// <returns>The scheduler instance for method chaining</returns>
        public static IScheduler MonitorAllSync(this IScheduler scheduler, string apiKey = null)
        {
            return scheduler.MonitorAll(apiKey).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Stops the CronRadar job watcher.
        /// This is typically called when the application is shutting down.
        /// </summary>
        public static void StopCronRadar()
        {
            lock (_initLock)
            {
                if (_watcherCts != null)
                {
                    _watcherCts.Cancel();
                    _watcherCts.Dispose();
                    _watcherCts = null;
                }

                _initialized = false;
                Console.WriteLine("[CronRadar.Quartz] CronRadar monitoring stopped");
            }
        }
    }
}
