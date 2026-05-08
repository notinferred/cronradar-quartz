using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Quartz;
using Quartz.Impl.Triggers;
using Xunit;

namespace CronRadar.Quartz.Tests;

/// <summary>
/// Tests for the CronRadarJobListener class.
/// Since the listener is internal, we test through the public MonitorAll API
/// and use reflection where needed.
/// </summary>
public class CronRadarJobListenerTests : IDisposable
{
    private readonly string _originalApiKey;
    private readonly bool _originalDebug;
    private Mock<HttpMessageHandler>? _mockHandler;
    private HttpClient? _mockClient;
    private readonly List<string> _capturedUrls = new();

    public CronRadarJobListenerTests()
    {
        _originalApiKey = Environment.GetEnvironmentVariable("CRONRADAR_API_KEY") ?? "";
        _originalDebug = CronRadar.DebugLoggingEnabled;

        Environment.SetEnvironmentVariable("CRONRADAR_API_KEY", "test-api-key");
        CronRadar.DebugLoggingEnabled = false;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("CRONRADAR_API_KEY", _originalApiKey);
        CronRadar.DebugLoggingEnabled = _originalDebug;
        ResetHttpClient();
        ResetExtensionsState();
    }

    private void ResetHttpClient()
    {
        var field = typeof(CronRadar).GetField("_httpClient", BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, null);
    }

    private void ResetExtensionsState()
    {
        var initField = typeof(QuartzExtensions).GetField("_initialized", BindingFlags.Static | BindingFlags.NonPublic);
        initField?.SetValue(null, false);
    }

    private void InjectMockHttpClient(Mock<HttpMessageHandler> mockHandler, string baseUrl = "https://cron.life")
    {
        _mockHandler = mockHandler;
        _mockClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(5)
        };

        var field = typeof(CronRadar).GetField("_httpClient", BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, _mockClient);
    }

    private Mock<HttpMessageHandler> SetupMockHandler()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                _capturedUrls.Add(req.RequestUri?.PathAndQuery ?? "");
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        return mockHandler;
    }

    private IJobListener CreateListener(string? apiKey = "test-api-key")
    {
        // Use reflection to create the internal listener with correct binding flags
        var listenerType = typeof(QuartzExtensions).Assembly.GetType("CronRadar.Quartz.CronRadarJobListener");
        if (listenerType == null)
            throw new InvalidOperationException("Could not find CronRadarJobListener type");

        // Get constructors with NonPublic flag since the class is internal
        var constructors = listenerType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (constructors.Length == 0)
            throw new InvalidOperationException("No constructors found on CronRadarJobListener");

        var constructor = constructors[0];
        return (IJobListener)constructor.Invoke(new object?[] { apiKey });
    }

    #region JobToBeExecuted Tests

    [Fact]
    public async Task JobToBeExecuted_CallsStartJobAsync_WithJobKey()
    {
        // Arrange
        var mockHandler = SetupMockHandler();
        InjectMockHttpClient(mockHandler);

        var listener = CreateListener();
        var context = CreateJobExecutionContext<TestJob>("TestGroup", "TestJobName");

        // Act
        await listener.JobToBeExecuted(context);

        // Assert
        Assert.Contains(_capturedUrls, url => url.Contains("/start"));
        Assert.Contains(_capturedUrls, url => url.Contains("testgroup-testjobname")); // normalized key
    }

    [Fact]
    public async Task JobToBeExecuted_UsesCronRadarAttributeKey_WhenPresent()
    {
        // Arrange
        var mockHandler = SetupMockHandler();
        InjectMockHttpClient(mockHandler);

        var listener = CreateListener();
        var context = CreateJobExecutionContext<TestJobWithCustomKey>("Group", "Name");

        // Act
        await listener.JobToBeExecuted(context);

        // Assert
        Assert.Contains(_capturedUrls, url => url.Contains("custom-monitor-key"));
    }

    [Fact]
    public async Task JobToBeExecuted_SkipsJob_WithSkipMonitorAttribute()
    {
        // Arrange
        var mockHandler = SetupMockHandler();
        InjectMockHttpClient(mockHandler);

        var listener = CreateListener();
        var context = CreateJobExecutionContext<TestJobWithSkipMonitor>("Group", "Name");

        // Act
        await listener.JobToBeExecuted(context);

        // Assert - should not call any API
        Assert.DoesNotContain(_capturedUrls, url => url.Contains("/start"));
    }

    [Fact]
    public async Task JobToBeExecuted_ExtractsSchedule_FromCronTrigger()
    {
        // Arrange
        var mockHandler = SetupMockHandler();
        InjectMockHttpClient(mockHandler);

        var listener = CreateListener();
        var cronTrigger = new CronTriggerImpl("TestTrigger", "TestGroup", "0 0 * * * ?");
        var context = CreateJobExecutionContextWithTrigger<TestJob>("Group", "Name", cronTrigger);

        // Act
        await listener.JobToBeExecuted(context);

        // Assert
        Assert.Contains(_capturedUrls, url => url.Contains("schedule="));
    }

    [Fact]
    public async Task JobToBeExecuted_ConvertsSimpleTrigger_ToApproximateCron()
    {
        // Arrange
        var mockHandler = SetupMockHandler();
        InjectMockHttpClient(mockHandler);

        var listener = CreateListener();
        var simpleTrigger = new SimpleTriggerImpl("TestTrigger", "TestGroup", -1, TimeSpan.FromHours(1));
        var context = CreateJobExecutionContextWithTrigger<TestJob>("Group", "Name", simpleTrigger);

        // Act
        await listener.JobToBeExecuted(context);

        // Assert - should include a schedule parameter
        Assert.Contains(_capturedUrls, url => url.Contains("schedule="));
    }

    [Fact]
    public async Task JobToBeExecuted_NormalizesJobKey_Correctly()
    {
        // Arrange
        var mockHandler = SetupMockHandler();
        InjectMockHttpClient(mockHandler);

        var listener = CreateListener();
        var context = CreateJobExecutionContext<TestJob>("My Reports", "Daily Report");

        // Act
        await listener.JobToBeExecuted(context);

        // Assert - spaces should be converted to hyphens and lowercased
        Assert.Contains(_capturedUrls, url => url.Contains("my-reports-daily-report"));
    }

    #endregion

    #region JobWasExecuted Tests

    [Fact]
    public async Task JobWasExecuted_CallsCompleteJobAsync_OnSuccess()
    {
        // Arrange
        var mockHandler = SetupMockHandler();
        InjectMockHttpClient(mockHandler);

        var listener = CreateListener();
        var context = CreateJobExecutionContext<TestJob>("Group", "Name");

        // Act
        await listener.JobWasExecuted(context, null);

        // Assert
        Assert.Contains(_capturedUrls, url => url.Contains("/complete"));
    }

    [Fact]
    public async Task JobWasExecuted_CallsFailJobAsync_WhenExceptionPresent()
    {
        // Arrange
        var mockHandler = SetupMockHandler();
        InjectMockHttpClient(mockHandler);

        var listener = CreateListener();
        var context = CreateJobExecutionContext<TestJob>("Group", "Name");
        var exception = new JobExecutionException("Test error occurred");

        // Act
        await listener.JobWasExecuted(context, exception);

        // Assert
        Assert.Contains(_capturedUrls, url => url.Contains("/fail"));
        Assert.Contains(_capturedUrls, url => url.Contains("message="));
    }

    [Fact]
    public async Task JobWasExecuted_IncludesExceptionMessage_InFailCall()
    {
        // Arrange
        var mockHandler = SetupMockHandler();
        InjectMockHttpClient(mockHandler);

        var listener = CreateListener();
        var context = CreateJobExecutionContext<TestJob>("Group", "Name");
        var exception = new JobExecutionException("Database connection failed");

        // Act
        await listener.JobWasExecuted(context, exception);

        // Assert
        Assert.Contains(_capturedUrls, url => url.Contains("Database%20connection%20failed"));
    }

    [Fact]
    public async Task JobWasExecuted_SkipsJob_WithSkipMonitorAttribute()
    {
        // Arrange
        var mockHandler = SetupMockHandler();
        InjectMockHttpClient(mockHandler);

        var listener = CreateListener();
        var context = CreateJobExecutionContext<TestJobWithSkipMonitor>("Group", "Name");

        // Act
        await listener.JobWasExecuted(context, null);

        // Assert - should not call any API
        Assert.DoesNotContain(_capturedUrls, url => url.Contains("/complete"));
        Assert.DoesNotContain(_capturedUrls, url => url.Contains("/fail"));
    }

    [Fact]
    public async Task JobWasExecuted_UsesCronRadarAttributeKey_WhenPresent()
    {
        // Arrange
        var mockHandler = SetupMockHandler();
        InjectMockHttpClient(mockHandler);

        var listener = CreateListener();
        var context = CreateJobExecutionContext<TestJobWithCustomKey>("Group", "Name");

        // Act
        await listener.JobWasExecuted(context, null);

        // Assert
        Assert.Contains(_capturedUrls, url => url.Contains("custom-monitor-key"));
    }

    #endregion

    #region JobExecutionVetoed Tests

    [Fact]
    public async Task JobExecutionVetoed_ReturnsCompletedTask()
    {
        // Arrange
        var listener = CreateListener();
        var context = CreateJobExecutionContext<TestJob>("Group", "Name");

        // Act
        var task = listener.JobExecutionVetoed(context);

        // Assert
        Assert.True(task.IsCompleted);
        await task; // Should complete immediately
    }

    #endregion

    #region Listener Properties Tests

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // Arrange
        var listener = CreateListener();

        // Act & Assert
        Assert.Equal("CronRadar.Quartz.JobListener", listener.Name);
    }

    #endregion

    #region MonitorAll Extension Tests

    [Fact]
    public async Task MonitorAll_RegistersJobListener()
    {
        // Arrange
        ResetExtensionsState();
        var mockScheduler = new Mock<IScheduler>();
        var mockListenerManager = new Mock<IListenerManager>();
        mockScheduler.Setup(s => s.ListenerManager).Returns(mockListenerManager.Object);

        // Act
        await mockScheduler.Object.MonitorAll("test-key");

        // Assert
        mockListenerManager.Verify(lm => lm.AddJobListener(
            It.Is<IJobListener>(l => l.Name == "CronRadar.Quartz.JobListener"),
            It.IsAny<IMatcher<JobKey>[]>()), Times.Once);
    }

    [Fact]
    public async Task MonitorAll_IgnoresDuplicateCalls()
    {
        // Arrange
        ResetExtensionsState();
        var mockScheduler = new Mock<IScheduler>();
        var mockListenerManager = new Mock<IListenerManager>();
        mockScheduler.Setup(s => s.ListenerManager).Returns(mockListenerManager.Object);

        // Act
        await mockScheduler.Object.MonitorAll();
        await mockScheduler.Object.MonitorAll(); // Second call

        // Assert - should only register once
        mockListenerManager.Verify(lm => lm.AddJobListener(
            It.IsAny<IJobListener>(),
            It.IsAny<IMatcher<JobKey>[]>()), Times.Once);
    }

    [Fact]
    public async Task MonitorAll_ReturnsSchedulerForChaining()
    {
        // Arrange
        ResetExtensionsState();
        var mockScheduler = new Mock<IScheduler>();
        var mockListenerManager = new Mock<IListenerManager>();
        mockScheduler.Setup(s => s.ListenerManager).Returns(mockListenerManager.Object);

        // Act
        var result = await mockScheduler.Object.MonitorAll();

        // Assert
        Assert.Same(mockScheduler.Object, result);
    }

    #endregion

    #region Helper Methods

    private IJobExecutionContext CreateJobExecutionContext<TJob>(string group, string name) where TJob : IJob
    {
        var jobKey = new JobKey(name, group);
        var triggerKey = new TriggerKey("trigger", "triggerGroup");

        var mockJobDetail = new Mock<IJobDetail>();
        mockJobDetail.Setup(j => j.Key).Returns(jobKey);
        mockJobDetail.Setup(j => j.JobType).Returns(typeof(TJob));

        var mockTrigger = new Mock<ITrigger>();
        mockTrigger.Setup(t => t.Key).Returns(triggerKey);

        var mockContext = new Mock<IJobExecutionContext>();
        mockContext.Setup(c => c.JobDetail).Returns(mockJobDetail.Object);
        mockContext.Setup(c => c.Trigger).Returns(mockTrigger.Object);

        return mockContext.Object;
    }

    private IJobExecutionContext CreateJobExecutionContextWithTrigger<TJob>(string group, string name, ITrigger trigger) where TJob : IJob
    {
        var jobKey = new JobKey(name, group);

        var mockJobDetail = new Mock<IJobDetail>();
        mockJobDetail.Setup(j => j.Key).Returns(jobKey);
        mockJobDetail.Setup(j => j.JobType).Returns(typeof(TJob));

        var mockContext = new Mock<IJobExecutionContext>();
        mockContext.Setup(c => c.JobDetail).Returns(mockJobDetail.Object);
        mockContext.Setup(c => c.Trigger).Returns(trigger);

        return mockContext.Object;
    }

    #endregion

    #region Test Job Classes

    public class TestJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }

    [CronRadar("custom-monitor-key")]
    public class TestJobWithCustomKey : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }

    [SkipMonitor]
    public class TestJobWithSkipMonitor : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }

    #endregion
}
