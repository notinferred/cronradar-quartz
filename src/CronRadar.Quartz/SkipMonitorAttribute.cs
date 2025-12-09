using System;

namespace CronRadar.Quartz
{
    /// <summary>
    /// Add [SkipMonitor] to any Quartz.NET job to exclude it from CronRadar monitoring.
    /// By default, all jobs are automatically monitored when MonitorAll() is called.
    /// Use this attribute for internal maintenance jobs or high-frequency jobs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class SkipMonitorAttribute : Attribute
    {
    }
}
