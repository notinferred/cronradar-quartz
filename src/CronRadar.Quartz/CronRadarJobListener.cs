using System;
using System.Threading;
using System.Threading.Tasks;
using Quartz;

namespace CronRadar.Quartz
{
    /// <summary>
    /// Job listener that automatically pings CronRadar when jobs complete successfully.
    /// Intercepts all job executions and sends pings using the base CronRadar SDK.
    /// </summary>
    public class CronRadarJobListener : IJobListener
    {
        /// <summary>
        /// Name of this listener
        /// </summary>
        public string Name => "CronRadar.Quartz.JobListener";

        /// <summary>
        /// Called when a job is about to be executed.
        /// </summary>
        public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            // Not needed - we only care about completion
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when a job execution was vetoed (prevented from executing).
        /// </summary>
        public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            // Not needed
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called after a job has been executed.
        /// Only pings CronRadar if the job completed successfully.
        /// </summary>
        public Task JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException, CancellationToken cancellationToken = default)
        {
            // Only ping if job completed successfully (no exception)
            if (jobException != null)
                return Task.CompletedTask;

            try
            {
                var jobType = context.JobDetail.JobType;
                var jobKey = context.JobDetail.Key;

                // Check for [CronRadar] attribute on the job class
                var attribute = jobType.GetCustomAttributes(typeof(CronRadarAttribute), false);

                string monitorKey = null;

                if (attribute.Length > 0)
                {
                    var cronRadarAttr = (CronRadarAttribute)attribute[0];

                    // If Skip is true, don't monitor this job
                    if (cronRadarAttr.Skip)
                        return Task.CompletedTask;

                    // Use custom monitor key from attribute if provided
                    if (!string.IsNullOrWhiteSpace(cronRadarAttr.MonitorKey))
                    {
                        monitorKey = CronRadarKeyNormalizer.Normalize(cronRadarAttr.MonitorKey);
                    }
                }

                // If no custom key from attribute, use normalized JobKey
                if (string.IsNullOrWhiteSpace(monitorKey))
                {
                    monitorKey = CronRadarKeyNormalizer.Normalize(jobKey.Group, jobKey.Name);
                }

                // Skip if we couldn't determine a valid monitor key
                if (string.IsNullOrWhiteSpace(monitorKey))
                    return Task.CompletedTask;

                // Use base SDK to monitor
                // We don't pass schedule here because the job is already registered
                // via the scheduler listener or watcher
                CronRadar.Monitor(monitorKey);
            }
            catch
            {
                // Silently ignore all errors - never break the job execution
            }

            return Task.CompletedTask;
        }
    }
}
