using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Quartz;
using Xunit;

namespace CronRadar.Quartz.Tests;

/// <summary>
/// Runs a real Quartz 4 scheduler against a local HTTP server, so a pass proves what the package
/// sends and when, not only that it compiles against Quartz 4.
/// </summary>
public class GoldenPathTests
{
    private const string NightlyCron = "0 0 2 * * ?";
    private const string WeekdayCron = "0 15 3 ? * MON-FRI";
    private static readonly TaskCompletionSource JobFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class NightlyReportJob : IJob
    {
        public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
        {
            JobFinished.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BrokenExportJob : IJob
    {
        public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("export failed");
    }

    [Fact(Timeout = 30_000)]
    public async Task MonitorAll_declares_the_translated_schedule_and_reports_the_run()
    {
        var requests = new ConcurrentQueue<(string Path, string Body)>();
        using var listener = StartServer(requests, out var baseUrl);
        Environment.SetEnvironmentVariable("CRONRADAR_BASE_URL", baseUrl);
        Environment.SetEnvironmentVariable("CRONRADAR_API_KEY", "ck_app_local_test");

        var scheduler = await QuartzSchedulerBuilder.Create(_ => { }).BuildScheduler();
        try
        {
            var job = JobBuilder.Create<NightlyReportJob>().WithIdentity("nightly-report", "reports").StoreDurably().Build();
            var trigger = TriggerBuilder.Create()
                .WithIdentity("nightly-report-trigger", "reports")
                .WithCronSchedule(NightlyCron)
                .Build();
            await scheduler.ScheduleJob(job, trigger);
            var brokenJob = JobBuilder.Create<BrokenExportJob>().WithIdentity("broken-export", "reports").StoreDurably().Build();
            var brokenTrigger = TriggerBuilder.Create()
                .WithIdentity("broken-export-trigger", "reports")
                .WithCronSchedule(WeekdayCron)
                .Build();
            await scheduler.ScheduleJob(brokenJob, brokenTrigger);

            await scheduler.MonitorAll();
            await scheduler.Start();
            await scheduler.TriggerJob(job.Key);
            await scheduler.TriggerJob(brokenJob.Key);

            await JobFinished.Task;
            await WaitFor(() => requests.Any(r => r.Path.Contains("/complete"))
                && requests.Any(r => r.Path.Contains("/fail"))
                && requests.Any(r => r.Path.Contains("/sync") && r.Body.Contains("broken")));
        }
        finally
        {
            await scheduler.Shutdown();
        }

        var sync = requests.Last(r => r.Path.Contains("/sync")).Body;
        Assert.Contains("\"schedule\":\"0 2 * * *\"", sync);
        Assert.Contains("\"schedule\":\"15 3 * * 1-5\"", sync);
        Assert.DoesNotContain(NightlyCron, sync);
        Assert.Contains(requests, r => r.Path.Contains("reports-nightly-report") && r.Path.Contains("/start"));
        Assert.Contains(requests, r => r.Path.Contains("reports-nightly-report") && r.Path.Contains("/complete"));
        Assert.Contains(requests, r => r.Path.Contains("reports-broken-export") && r.Path.Contains("/fail"));
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        while (!condition())
            await Task.Delay(25);
    }

    private static HttpListener StartServer(ConcurrentQueue<(string Path, string Body)> requests, out string baseUrl)
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        baseUrl = $"http://127.0.0.1:{port}";

        var listener = new HttpListener();
        listener.Prefixes.Add($"{baseUrl}/");
        listener.Start();
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync(); }
                catch (Exception) when (!listener.IsListening) { return; }
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                requests.Enqueue((context.Request.Url!.PathAndQuery, await reader.ReadToEndAsync()));
                context.Response.StatusCode = 200;
                await context.Response.OutputStream.WriteAsync("{}"u8.ToArray());
                context.Response.Close();
            }
        });
        return listener;
    }
}
