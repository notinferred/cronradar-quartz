using System;

namespace CronRadar.Quartz
{
    /// <summary>
    /// Add [CronRadar("monitor-key")] to any Quartz.NET job to customize monitoring.
    /// By default, jobs are automatically monitored using their JobKey.
    /// Use this attribute to:
    /// - Specify a custom monitor key
    /// - Skip monitoring for specific jobs
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CronRadarAttribute : Attribute
    {
        /// <summary>
        /// The custom monitor key for this job.
        /// If null, the job's normalized JobKey will be used.
        /// </summary>
        public string? MonitorKey { get; }

        /// <summary>
        /// If true, this job will not be monitored by CronRadar.
        /// Useful for internal maintenance jobs or high-frequency jobs.
        /// </summary>
        public bool Skip { get; set; }

        /// <summary>
        /// Creates a new CronRadar monitoring attribute with a custom monitor key.
        /// </summary>
        /// <param name="monitorKey">The unique key to identify this job in CronRadar</param>
        public CronRadarAttribute(string monitorKey)
        {
            MonitorKey = monitorKey;
            Skip = false;
        }

        /// <summary>
        /// Creates a new CronRadar attribute to skip monitoring for this job.
        /// </summary>
        public CronRadarAttribute()
        {
            MonitorKey = null;
            Skip = false;
        }
    }
}
