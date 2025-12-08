using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl.Matchers;

namespace CronRadar.Quartz
{
    /// <summary>
    /// Background watcher that discovers existing Quartz.NET jobs and syncs them to CronRadar.
    /// Runs periodically to detect new jobs that may have been added.
    /// </summary>
    public class CronRadarJobWatcher
    {
        private readonly IScheduler _scheduler;
        private readonly HashSet<string> _syncedJobs = new HashSet<string>();
        private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);

        public CronRadarJobWatcher(IScheduler scheduler)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        /// <summary>
        /// Starts watching for jobs. Runs continuously in the background.
        /// </summary>
        public async Task WatchForJobsAsync(CancellationToken cancellationToken = default)
        {
            // Initial delay to let the scheduler fully initialize
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await DiscoverAndSyncJobsAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log error but continue watching
                    Console.WriteLine($"[CronRadar.Quartz] Error during job discovery: {ex.Message}");
                }

                // Wait 10 seconds before next check
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
            }
        }

        /// <summary>
        /// Discovers all jobs in the scheduler and syncs them to CronRadar.
        /// </summary>
        private async Task DiscoverAndSyncJobsAsync(CancellationToken cancellationToken)
        {
            await _syncLock.WaitAsync(cancellationToken);

            try
            {
                // Get all job keys from all groups
                var jobKeys = await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken);

                foreach (var jobKey in jobKeys)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        // Get triggers for this job
                        var triggers = await _scheduler.GetTriggersOfJob(jobKey, cancellationToken);

                        foreach (var trigger in triggers)
                        {
                            await SyncJobTriggerAsync(jobKey, trigger, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CronRadar.Quartz] Failed to sync job {jobKey}: {ex.Message}");
                    }
                }
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// Syncs a specific job/trigger combination to CronRadar.
        /// </summary>
        private async Task SyncJobTriggerAsync(JobKey jobKey, ITrigger trigger, CancellationToken cancellationToken)
        {
            string cronExpression = null;

            // Extract cron expression based on trigger type
            if (trigger is ICronTrigger cronTrigger)
            {
                cronExpression = cronTrigger.CronExpressionString;
            }
            else if (trigger is ISimpleTrigger simpleTrigger)
            {
                // Only sync repeating simple triggers
                if (simpleTrigger.RepeatCount != 0) // -1 = infinite, or any positive number
                {
                    cronExpression = ConvertIntervalToCron(simpleTrigger.RepeatInterval);
                }
            }

            // Skip if no valid schedule
            if (string.IsNullOrWhiteSpace(cronExpression))
                return;

            // Generate monitor key
            var monitorKey = CronRadarKeyNormalizer.Normalize(jobKey.Group, jobKey.Name);

            if (string.IsNullOrWhiteSpace(monitorKey))
                return;

            // Check if already synced
            if (_syncedJobs.Contains(monitorKey))
                return;

            try
            {
                // Get job detail to check for attributes
                var jobDetail = await _scheduler.GetJobDetail(jobKey, cancellationToken);

                if (jobDetail != null)
                {
                    var jobType = jobDetail.JobType;
                    var attribute = jobType.GetCustomAttributes(typeof(CronRadarAttribute), false);

                    // Check if this job should be skipped
                    if (attribute.Length > 0)
                    {
                        var cronRadarAttr = (CronRadarAttribute)attribute[0];
                        if (cronRadarAttr.Skip)
                        {
                            _syncedJobs.Add(monitorKey); // Mark as synced so we don't check again
                            return;
                        }

                        // Use custom monitor key if provided
                        if (!string.IsNullOrWhiteSpace(cronRadarAttr.MonitorKey))
                        {
                            monitorKey = CronRadarKeyNormalizer.Normalize(cronRadarAttr.MonitorKey);
                        }
                    }
                }

                // Generate readable name
                var jobName = GenerateReadableName(jobKey.Name);

                // Use base SDK to sync monitor
                await CronRadar.SyncAsync(
                    monitorKey,
                    cronExpression,
                    source: "quartz",
                    name: jobName
                );

                _syncedJobs.Add(monitorKey);
                Console.WriteLine($"[CronRadar.Quartz] Discovered and synced: {monitorKey} ({cronExpression})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CronRadar.Quartz] Failed to sync {monitorKey}: {ex.Message}");
                // Don't add to _syncedJobs so we can retry later
            }
        }

        /// <summary>
        /// Converts a TimeSpan interval to an approximate cron expression.
        /// </summary>
        private string ConvertIntervalToCron(TimeSpan interval)
        {
            if (interval.TotalMinutes < 1)
            {
                return "* * * * *"; // Every minute
            }
            else if (interval.TotalMinutes == 1)
            {
                return "* * * * *";
            }
            else if (interval.TotalMinutes == 5)
            {
                return "*/5 * * * *";
            }
            else if (interval.TotalMinutes == 15)
            {
                return "*/15 * * * *";
            }
            else if (interval.TotalMinutes == 30)
            {
                return "*/30 * * * *";
            }
            else if (interval.TotalHours == 1)
            {
                return "0 * * * *";
            }
            else if (interval.TotalHours % 1 == 0 && interval.TotalHours < 24)
            {
                return $"0 */{(int)interval.TotalHours} * * *";
            }
            else if (interval.TotalDays == 1)
            {
                return "0 0 * * *";
            }
            else if (interval.TotalDays == 7)
            {
                return "0 0 * * 0";
            }
            else
            {
                return "0 * * * *"; // Default to hourly
            }
        }

        /// <summary>
        /// Generate a human-readable name from a job name.
        /// </summary>
        private string GenerateReadableName(string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
                return jobName;

            var result = new System.Text.StringBuilder();
            bool lastWasUpper = false;

            for (int i = 0; i < jobName.Length; i++)
            {
                var c = jobName[i];

                if (i == 0)
                {
                    result.Append(char.ToUpper(c));
                    lastWasUpper = char.IsUpper(c);
                }
                else if (c == '-' || c == '_' || c == '.')
                {
                    result.Append(' ');
                    lastWasUpper = false;
                }
                else if (char.IsUpper(c) && !lastWasUpper)
                {
                    result.Append(' ');
                    result.Append(c);
                    lastWasUpper = true;
                }
                else
                {
                    result.Append(c);
                    lastWasUpper = char.IsUpper(c);
                }
            }

            return result.ToString();
        }
    }
}
