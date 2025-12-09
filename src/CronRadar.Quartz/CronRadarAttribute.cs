using System;

namespace CronRadar.Quartz
{
    /// <summary>
    /// Add [CronRadar("custom-key")] to any Quartz.NET job to specify a custom monitor key.
    /// By default, jobs are monitored using their normalized JobKey (group-name).
    /// Use this attribute to:
    /// - Specify a custom monitor key for easier identification
    /// - Align the monitor key with other systems or naming conventions
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CronRadarAttribute : Attribute
    {
        /// <summary>
        /// The custom monitor key for this job.
        /// </summary>
        public string? MonitorKey { get; }

        /// <summary>
        /// Creates a new CronRadar monitoring attribute with a custom monitor key.
        /// </summary>
        /// <param name="monitorKey">The unique key to identify this job in CronRadar</param>
        public CronRadarAttribute(string monitorKey)
        {
            MonitorKey = monitorKey;
        }
    }
}
