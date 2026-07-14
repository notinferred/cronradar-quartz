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

        public Task SchedulerStarted(CancellationToken cancellationToken = default)
        {
            return CronRadarReconciler.ReconcileAsync(_scheduler, _apiKey, cancellationToken);
        }

        // Declaration is idempotent, so reflecting add/remove promptly is safe.
        public Task JobAdded(IJobDetail jobDetail, CancellationToken cancellationToken = default)
            => CronRadarReconciler.ReconcileAsync(_scheduler, _apiKey, cancellationToken);

        public Task JobDeleted(JobKey jobKey, CancellationToken cancellationToken = default)
            => CronRadarReconciler.ReconcileAsync(_scheduler, _apiKey, cancellationToken);

        // Remaining members are not relevant to declaration.
        public Task JobScheduled(ITrigger trigger, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task JobUnscheduled(TriggerKey triggerKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task TriggerFinalized(ITrigger trigger, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task TriggerPaused(TriggerKey triggerKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task TriggersPaused(string? triggerGroup, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task TriggerResumed(TriggerKey triggerKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task TriggersResumed(string? triggerGroup, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task JobInterrupted(JobKey jobKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task JobPaused(JobKey jobKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task JobsPaused(string jobGroup, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task JobResumed(JobKey jobKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task JobsResumed(string jobGroup, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SchedulerError(string msg, SchedulerException cause, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SchedulerInStandbyMode(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SchedulerStarting(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SchedulerShutdown(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SchedulerShuttingdown(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SchedulingDataCleared(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
