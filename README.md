# CronRadar for Quartz.NET

[![NuGet](https://img.shields.io/nuget/v/CronRadar.Quartz.svg)](https://www.nuget.org/packages/CronRadar.Quartz/)

**Monitor all Quartz.NET jobs automatically.**

## Installation

```bash
dotnet add package CronRadar.Quartz
```

## Quick Start

```csharp
// Set API key via environment variable
// CRONRADAR_API_KEY=ck_app_xxxxx

IScheduler scheduler = await StdSchedulerFactory.GetDefaultScheduler();
await scheduler.MonitorAll();  // 👈 That's it!
await scheduler.Start();
```

All scheduled jobs are now monitored automatically.

## Opt-Out Specific Jobs

```csharp
[SkipMonitor]
public class InternalTask : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        // This job won't be monitored
    }
}
```

## Custom Monitor Keys

```csharp
[CronRadar("critical-payment-processor")]
public class ProcessPaymentsJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        // Monitored with custom key
    }
}
```

## Configuration

**API Key** (required):
```bash
CRONRADAR_API_KEY=ck_app_xxxxx
```

Or pass directly:
```csharp
await scheduler.MonitorAll(apiKey: "ck_app_xxxxx");
```

**Debug logging** (optional):
```bash
CRONRADAR_DEBUG=true
```

## How It Works

1. Detects all Quartz.NET jobs
2. Auto-registers monitors on first execution
3. Tracks start/complete/fail for every run
4. Never breaks your jobs (all exceptions caught)

## License

Proprietary - © 2025 CronRadar. All rights reserved.
