#if QUARTZ4
using QuartzTask = System.Threading.Tasks.ValueTask;
#else
using QuartzTask = System.Threading.Tasks.Task;
#endif
using System;
using System.Threading;
using System.Threading.Tasks;
using Quartz;

namespace CronRadar.Quartz
{
    /// <summary>
    /// Reconciles the full schedule to CronRadar as soon as the scheduler starts (all
    /// jobs are registered by then), giving dead-man coverage at t=0. Ongoing heartbeat
    /// and drift reflection are handled by the periodic reconcile job.
    /// </summary>
    internal sealed class CronRadarSchedulerListener : ISchedulerListener
    {
        private readonly IScheduler _scheduler;
        private readonly string? _apiKey;

        public CronRadarSchedulerListener(IScheduler scheduler, string? apiKey)
        {
            _scheduler = scheduler;
            _apiKey = apiKey;
        }

#if QUARTZ4
        // Every ISchedulerListener member has a default in Quartz 4, and a callback without the
        // leading IScheduler parameter compiles but is never called.
        public QuartzTask SchedulerStarted(IScheduler scheduler, CancellationToken cancellationToken = default)
            => CronRadarReconciler.ReconcileAsync(_scheduler, _apiKey, cancellationToken);

        // Declaration is idempotent, so reflecting add/remove promptly is safe.
        public QuartzTask JobAdded(IScheduler scheduler, IJobDetail jobDetail, CancellationToken cancellationToken = default)
            => CronRadarReconciler.ReconcileAsync(_scheduler, _apiKey, cancellationToken);

        public QuartzTask JobDeleted(IScheduler scheduler, JobKey jobKey, CancellationToken cancellationToken = default)
            => CronRadarReconciler.ReconcileAsync(_scheduler, _apiKey, cancellationToken);
#else
        public QuartzTask SchedulerStarted(CancellationToken cancellationToken = default)
        {
            return CronRadarReconciler.ReconcileAsync(_scheduler, _apiKey, cancellationToken);
        }

        // Declaration is idempotent, so reflecting add/remove promptly is safe.
        public QuartzTask JobAdded(IJobDetail jobDetail, CancellationToken cancellationToken = default)
            => CronRadarReconciler.ReconcileAsync(_scheduler, _apiKey, cancellationToken);

        public QuartzTask JobDeleted(JobKey jobKey, CancellationToken cancellationToken = default)
            => CronRadarReconciler.ReconcileAsync(_scheduler, _apiKey, cancellationToken);

        // Remaining members are not relevant to declaration.
        public QuartzTask JobScheduled(ITrigger trigger, CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask JobUnscheduled(TriggerKey triggerKey, CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask TriggerFinalized(ITrigger trigger, CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask TriggerPaused(TriggerKey triggerKey, CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask TriggersPaused(string? triggerGroup, CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask TriggerResumed(TriggerKey triggerKey, CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask TriggersResumed(string? triggerGroup, CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask JobInterrupted(JobKey jobKey, CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask JobPaused(JobKey jobKey, CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask JobsPaused(string jobGroup, CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask JobResumed(JobKey jobKey, CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask JobsResumed(string jobGroup, CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask SchedulerError(string msg, SchedulerException cause, CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask SchedulerInStandbyMode(CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask SchedulerStarting(CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask SchedulerShutdown(CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask SchedulerShuttingdown(CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
        public QuartzTask SchedulingDataCleared(CancellationToken cancellationToken = default) => QuartzTask.CompletedTask;
#endif
    }
}
