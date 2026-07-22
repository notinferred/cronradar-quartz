# CronRadar for Quartz.NET

[![NuGet](https://img.shields.io/nuget/v/CronRadar.Quartz.svg)](https://www.nuget.org/packages/CronRadar.Quartz/)

Auto-discovery for Quartz.NET jobs. Add one line after building your scheduler — every triggered job is registered with CronRadar, watched against its trigger schedule, and alerted on if it fails or misses a run.

Built on top of the [`CronRadar`](https://www.nuget.org/packages/CronRadar) base SDK. Quartz.NET 3.x supported.

## Install

```bash
dotnet add package CronRadar.Quartz
```

Targets `net6.0`, `net7.0`, `net8.0`, `net9.0`. Compatible with Quartz.NET 3.4+.

## Setup

The SDK reads its API key from the `CRONRADAR_API_KEY` environment variable.

```bash
export CRONRADAR_API_KEY=ck_app_xxxxxxxxxxxxxxxxxxxx
```

Or pass it explicitly to `MonitorAll(apiKey: ...)`.

Get the API key from the CronRadar dashboard at [app.cronradar.com](https://app.cronradar.com) under your application's settings. API keys have the form `ck_app_<random>`.

Create a separate CronRadar application and API key for production, staging, and development. Store the key in the deployment platform's secret manager; never commit it, print it, or reuse a production key outside production.

The Quartz integration runs once at scheduler startup and listens to every job execution — no per-job initialization is needed.

## Quickstart

Call `MonitorAll()` on your `IScheduler` after registering jobs and before `Start()`:

```csharp
using Quartz;
using CronRadar.Quartz;

var schedulerFactory = new StdSchedulerFactory();
IScheduler scheduler = await schedulerFactory.GetScheduler();

// register your jobs and triggers as usual
await scheduler.ScheduleJob(myJob, myTrigger);

// hook CronRadar in
await scheduler.MonitorAll();

await scheduler.Start();
```

That's it. On startup, the extension:

- Walks every registered `JobDetail` and `Trigger`
- Pre-syncs each job to CronRadar (so they appear in the dashboard immediately)
- Translates Quartz cron expressions to standard 5-field POSIX cron for CronRadar
- Registers a `IJobListener` that records `start` / `complete` / `fail` on every execution

If you're hosting Quartz via the .NET DI integration (`AddQuartz()` / `AddQuartzHostedService()`), call `MonitorAll()` from a hosted service or `IHostedService` after the scheduler starts:

```csharp
public class CronRadarBootstrap : IHostedService
{
    private readonly ISchedulerFactory _factory;
    public CronRadarBootstrap(ISchedulerFactory factory) => _factory = factory;

    public async Task StartAsync(CancellationToken ct)
    {
        var scheduler = await _factory.GetScheduler(ct);
        await scheduler.MonitorAll();
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

## Annotations

### Opt out of monitoring per-job

Decorate any `IJob` class with `[SkipMonitor]` to exclude it.

```csharp
using CronRadar.Quartz;
using Quartz;

[SkipMonitor]
public class InternalCleanupJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        // not monitored — internal/maintenance jobs you don't need alerts for
        return Task.CompletedTask;
    }
}
```

### Override the monitor key

By default, the monitor key is derived from the Quartz `JobKey` (`group.name`, lowercased and normalized). Override it explicitly:

```csharp
using CronRadar.Quartz;
using Quartz;

[CronRadar("critical-payment-processor")]
public class ProcessPaymentsJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        // appears in dashboard under the explicit key
        return Task.CompletedTask;
    }
}
```

## Reference

### `MonitorAll()` extension method

```csharp
public static Task<IScheduler> MonitorAll(
    this IScheduler scheduler,
    string? apiKey = null);
```

| Parameter | Type | Required | Notes |
|---|---|---|---|
| `apiKey` | `string?` | optional | API key. Falls back to `CRONRADAR_API_KEY` env var if omitted. |

Returns the scheduler for chaining.

Grace period defaults to 60 seconds per monitor and can be tuned via the dashboard or by setting `CronRadar.DefaultGracePeriodSeconds` on the base SDK before `MonitorAll()` runs.

### Attributes

| Attribute | Targets | Purpose |
|---|---|---|
| `[SkipMonitor]` | class implementing `IJob` | Exclude from monitoring entirely. |
| `[CronRadar("key")]` | class implementing `IJob` | Override the monitor key. |

### Cron expression translation

Quartz uses 6-field cron expressions with seconds (`0 0 2 * * ?`). CronRadar uses standard 5-field POSIX cron (`0 2 * * *`). The extension translates automatically:

| Quartz | Translated to CronRadar |
|---|---|
| `0 0 2 * * ?` | `0 2 * * *` |
| `0 0/15 * * * ?` | `*/15 * * * *` |
| `0 0 2 ? * MON-FRI` | `0 2 * * 1-5` |

If a translation isn't possible (rare — e.g., sub-minute resolution), the extension logs a warning and that job is not monitored. All other jobs continue normally.

## Configuration

| Environment variable | Required | Default | Purpose |
|---|---|---|---|
| `CRONRADAR_API_KEY` | yes | — | API key from the CronRadar dashboard. Format `ck_app_xxxxx`. |
| `CRONRADAR_BASE_URL` | no | `https://cron.life` | Override the ingestion endpoint. |
| `CRONRADAR_TIMEOUT_MS` | no | `5000` | HTTP request timeout in milliseconds. |
| `CRONRADAR_DEBUG` | no | `false` | Set to `true` to log every request to standard output. |

## Error handling

The extension upholds the same hard guarantees as the base SDK:

1. **Never throws to user code.** Network errors, timeouts, 4xx/5xx responses — all caught and logged. A failing CronRadar API must not break a Quartz job.
2. **Never alters job execution.** The extension registers an `IJobListener`; it never short-circuits or modifies `IJobExecutionContext`. If your `IJob.Execute` throws, Quartz's normal misfire and retry handling runs unchanged.

What this looks like at runtime:

| Situation | SDK behavior | Quartz behavior |
|---|---|---|
| CronRadar API unreachable | Logs once; returns | Job runs and is marked Vetoed/Succeeded normally |
| Sync on startup fails for one job | Logs; continues | Other jobs sync; scheduler starts |
| Per-job ping fails | Logs; returns | Job state unchanged |
| User job throws | Records `failJob` with exception message | Quartz applies misfire / retry policy |
| Cron expression not translatable | Warns; that job is not monitored | Job runs as scheduled (Quartz handles it) |

## Troubleshooting

### Jobs aren't showing up in the CronRadar dashboard

1. **Confirm `await scheduler.MonitorAll()` is called *after* you've scheduled jobs and *before* (or after, fine either way) `Start()`.** Jobs added after `MonitorAll()` are also caught by the live listener.
2. **Confirm `CRONRADAR_API_KEY` is set.** Check process env, not just shell env.
3. **Verify outbound HTTPS to `https://cron.life`.** If your environment has restricted egress, allowlist it.
4. **Set `CRONRADAR_DEBUG=true`** and re-run; you'll see every request and response.

### Some triggers are skipped with "cron expression not translatable"

You're using a Quartz feature that has no POSIX equivalent — usually sub-minute scheduling (`0/30 * * * * ?` for every 30 seconds). CronRadar monitors at minute resolution, so these can't be tracked. Either coarsen the schedule, or wrap the job manually with the base [`CronRadar`](https://www.nuget.org/packages/CronRadar) SDK at a custom interval.

### My job is registered with `JobKey("group.foo")` — what's the monitor key?

The monitor key is `group-foo` by default (group + name, joined with `-`, lowercased, normalized). Override with `[CronRadar("explicit-key")]` to make it whatever you want.

### How do I migrate from the deprecated synchronous `MonitorAll()` overload?

Earlier versions exposed a sync `MonitorAll()` extension. The current API is async-only — `await scheduler.MonitorAll()`. Migration:

1. Add `await` in front of every `MonitorAll()` call site.
2. If the call site is non-async, refactor to `async` or wrap in `.GetAwaiter().GetResult()` (last resort).

The sync overload is removed; references will fail to compile, which is intentional — agents reading old snippets get a clear signal to update.

## Verification

1. Start the scheduler after awaiting `scheduler.MonitorAll()`.
2. Confirm each supported recurring trigger appears in the intended CronRadar application with the translated schedule.
3. Run a job through Quartz.NET and confirm a terminal `success` or `failed` result; discovery alone is not activation.
4. Use CronRadar's guided reliability drill to verify notification delivery without changing a production job.

Check authentication and outbound HTTPS without printing the key:

```bash
curl --fail-with-body --silent --show-error --user "$CRONRADAR_API_KEY:" "https://cron.life/api/sync/status"
```

## Migration

Replace the deprecated synchronous `MonitorAll()` overload with `await scheduler.MonitorAll(apiKey)` after jobs and triggers are registered and before `scheduler.Start()`. Keep existing job keys stable, deploy, confirm reconciliation and one terminal run, then remove any manual wrapper using the same key.

## Links

- **Documentation:** [docs.cronradar.com/integrations/quartz](https://docs.cronradar.com/integrations/quartz)
- **Monitoring overview:** [cronradar.com/quartz](https://cronradar.com/quartz)
- **Agent-friendly index:** [docs.cronradar.com/llms.txt](https://docs.cronradar.com/llms.txt)
- **OpenAPI spec:** [docs.cronradar.com/openapi.json](https://docs.cronradar.com/openapi.json)
- **NuGet:** [nuget.org/packages/CronRadar.Quartz](https://www.nuget.org/packages/CronRadar.Quartz)
- **GitHub:** [github.com/cronradar/cronradar-quartz](https://github.com/cronradar/cronradar-quartz)
- **Base SDK:** [nuget.org/packages/CronRadar](https://www.nuget.org/packages/CronRadar)
- **Support:** support@cronradar.com

## License

© 2026 [CronRadar](https://cronradar.com) · Proprietary — see [LICENSE](./LICENSE).
