# CronRadar.Quartz

Monitor all Quartz.NET jobs automatically with zero configuration.

## Installation

```bash
dotnet add package CronRadar.Quartz
```

## Quick Start

```csharp
using Quartz;
using CronRadar.Quartz;

// Get your scheduler
IScheduler scheduler = await StdSchedulerFactory.GetDefaultScheduler();

// Enable CronRadar monitoring
await scheduler.MonitorAll();

// Start the scheduler
await scheduler.Start();
```

Every scheduled job is now monitored automatically.

## Configuration

Set your API key as an environment variable:

```bash
# Linux/macOS
export CRONRADAR_API_KEY=ck_app_xxxxx_yyyyy

# Windows
set CRONRADAR_API_KEY=ck_app_xxxxx_yyyyy
```

Or pass it directly:

```csharp
await scheduler.MonitorAll("ck_app_xxxxx_yyyyy");
```

Get your API key from [cronradar.com/dashboard](https://cronradar.com/dashboard)

## Controlling Monitoring

By default, all scheduled jobs are monitored. Use the `[CronRadar]` attribute to customize behavior:

### Skip Specific Jobs

```csharp
using CronRadar.Quartz;

[CronRadar(Skip = true)]
public class InternalMaintenanceJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        // This job won't be monitored
    }
}
```

### Custom Monitor Keys

```csharp
[CronRadar("critical-payment-processor")]
public class ProcessPaymentsJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        // Monitored with key "critical-payment-processor"
    }
}
```

## Documentation

See [cronradar.com/docs/sdks/quartz](https://cronradar.com/docs/sdks/quartz) for full documentation.

## License

Proprietary - © 2025 CronRadar. All rights reserved.

See [LICENSE](./LICENSE) for details. This SDK may only be used with the CronRadar monitoring service.
