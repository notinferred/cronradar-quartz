# CronRadar.Quartz

Monitor all Quartz.NET jobs with one line of code.

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
await scheduler.UseCronRadar();

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
await scheduler.UseCronRadar("ck_app_xxxxx_yyyyy");
```

Get your API key from [cronradar.com/dashboard](https://cronradar.com/dashboard)

## Documentation

See [cronradar.com/docs/sdks/quartz](https://cronradar.com/docs/sdks/quartz) for full documentation.

## License

MIT
