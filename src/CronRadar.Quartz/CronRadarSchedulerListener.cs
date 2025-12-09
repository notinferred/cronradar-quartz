using System;
using System.Threading;
using System.Threading.Tasks;
using Quartz;

namespace CronRadar.Quartz
{
    /// <summary>
    /// Scheduler listener that automatically syncs monitors when jobs are scheduled.
    /// Detects new job/trigger registrations and creates corresponding CronRadar monitors.
    /// </summary>
    public class CronRadarSchedulerListener : ISchedulerListener
    {
        /// <summary>
        /// Called when a job is scheduled with a trigger.
        /// This is our opportunity to sync the monitor to CronRadar.
        /// </summary>
        public async Task JobScheduled(ITrigger trigger, CancellationToken cancellationToken = default)
        {
            try
            {
                // Only monitor cron triggers and simple repeating triggers
                string cronExpression = null;

                if (trigger is ICronTrigger cronTrigger)
                {
                    cronExpression = cronTrigger.CronExpressionString;
                }
                else if (trigger is ISimpleTrigger simpleTrigger)
                {
                    // Convert simple trigger to approximate cron expression
                    // For repeating jobs, we'll use the repeat interval
                    if (simpleTrigger.RepeatCount != 0) // -1 = infinite, or any positive number
                    {
                        cronExpression = ConvertIntervalToCron(simpleTrigger.RepeatInterval);
                    }
                }

                // If we have a cron expression and a job key, sync it
                if (!string.IsNullOrWhiteSpace(cronExpression) && trigger.JobKey != null)
                {
                    var monitorKey = CronRadarKeyNormalizer.Normalize(trigger.JobKey.Group, trigger.JobKey.Name);

                    if (!string.IsNullOrWhiteSpace(monitorKey))
                    {
                        var jobName = GenerateReadableName(trigger.JobKey.Name);

                        // Use base SDK to sync monitor
                        await CronRadar.SyncMonitorAsync(
                            monitorKey,
                            cronExpression,
                            source: "quartz",
                            name: jobName
                        );

                        Console.WriteLine($"[CronRadar.Quartz] Synced monitor: {monitorKey} with schedule: {cronExpression}");
                    }
                }
            }
            catch
            {
                // Silently ignore errors - never break job scheduling
            }
        }

        /// <summary>
        /// Converts a TimeSpan interval to an approximate cron expression.
        /// </summary>
        private string ConvertIntervalToCron(TimeSpan interval)
        {
            // Convert common intervals to cron expressions
            if (interval.TotalMinutes < 1)
            {
                // Every minute (can't represent sub-minute in cron)
                return "* * * * *";
            }
            else if (interval.TotalMinutes == 1)
            {
                return "* * * * *"; // Every minute
            }
            else if (interval.TotalMinutes == 5)
            {
                return "*/5 * * * *"; // Every 5 minutes
            }
            else if (interval.TotalMinutes == 15)
            {
                return "*/15 * * * *"; // Every 15 minutes
            }
            else if (interval.TotalMinutes == 30)
            {
                return "*/30 * * * *"; // Every 30 minutes
            }
            else if (interval.TotalHours == 1)
            {
                return "0 * * * *"; // Every hour
            }
            else if (interval.TotalHours % 1 == 0 && interval.TotalHours < 24)
            {
                return $"0 */{(int)interval.TotalHours} * * *"; // Every N hours
            }
            else if (interval.TotalDays == 1)
            {
                return "0 0 * * *"; // Daily
            }
            else if (interval.TotalDays == 7)
            {
                return "0 0 * * 0"; // Weekly (Sunday)
            }
            else
            {
                // Default to hourly for other intervals
                return "0 * * * *";
            }
        }

        /// <summary>
        /// Generate a human-readable name from a job name.
        /// </summary>
        private string GenerateReadableName(string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
                return jobName;

            // "DailyReportJob" -> "Daily Report Job"
            // "backup-database" -> "Backup Database"
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

        #region Unused ISchedulerListener Methods

        public Task JobAdded(IJobDetail jobDetail, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task JobDeleted(JobKey jobKey, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task JobInterrupted(JobKey jobKey, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task JobPaused(JobKey jobKey, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task JobResumed(JobKey jobKey, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task JobsPaused(string jobGroup, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task JobsResumed(string jobGroup, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task JobUnscheduled(TriggerKey triggerKey, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SchedulerError(string msg, SchedulerException cause, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SchedulerInStandbyMode(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SchedulerShutdown(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SchedulerShuttingdown(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SchedulerStarted(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SchedulerStarting(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SchedulingDataCleared(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task TriggerFinalized(ITrigger trigger, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task TriggerPaused(TriggerKey triggerKey, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task TriggerResumed(TriggerKey triggerKey, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task TriggersPaused(string triggerGroup, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task TriggersResumed(string triggerGroup, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        #endregion
    }
}
