using System;
using System.Reflection;
using Quartz;
using Xunit;

namespace CronRadar.Quartz.Tests;

/// <summary>
/// Tests for the ConvertIntervalToCron method in CronRadarJobListener.
/// Verifies that various time intervals are correctly converted to cron expressions.
/// </summary>
public class IntervalToCronTests
{
    /// <summary>
    /// Invokes the private ConvertIntervalToCron method via reflection.
    /// </summary>
    private string ConvertIntervalToCron(TimeSpan interval)
    {
        // Get the internal listener type
        var listenerType = typeof(QuartzExtensions).Assembly.GetType("CronRadar.Quartz.CronRadarJobListener");
        if (listenerType == null)
            throw new InvalidOperationException("Could not find CronRadarJobListener type");

        // Get the constructor (internal class has internal constructor accessible via NonPublic)
        var constructors = listenerType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (constructors.Length == 0)
            throw new InvalidOperationException("No constructors found on CronRadarJobListener");

        // Create instance with null API key
        var instance = constructors[0].Invoke(new object?[] { null });

        // Get and invoke the private method
        var method = listenerType.GetMethod("ConvertIntervalToCron", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            throw new InvalidOperationException("Could not find ConvertIntervalToCron method");

        return (string)method.Invoke(instance, new object[] { interval })!;
    }

    #region Minute-Based Intervals

    [Fact]
    public void ConvertIntervalToCron_1Minute_ReturnsEveryMinute()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromMinutes(1));
        Assert.Equal("*/1 * * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_5Minutes_ReturnsEvery5Minutes()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromMinutes(5));
        Assert.Equal("*/5 * * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_10Minutes_ReturnsEvery10Minutes()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromMinutes(10));
        Assert.Equal("*/10 * * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_15Minutes_ReturnsEvery15Minutes()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromMinutes(15));
        Assert.Equal("*/15 * * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_30Minutes_ReturnsEvery30Minutes()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromMinutes(30));
        Assert.Equal("*/30 * * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_45Minutes_ReturnsEvery45Minutes()
    {
        // 45 doesn't divide evenly into 60, but the method still uses */N format
        var result = ConvertIntervalToCron(TimeSpan.FromMinutes(45));
        Assert.Equal("*/45 * * * *", result);
    }

    #endregion

    #region Hour-Based Intervals

    [Fact]
    public void ConvertIntervalToCron_1Hour_ReturnsEveryHour()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromHours(1));
        Assert.Equal("0 */1 * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_2Hours_ReturnsEvery2Hours()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromHours(2));
        Assert.Equal("0 */2 * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_3Hours_ReturnsEvery3Hours()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromHours(3));
        Assert.Equal("0 */3 * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_4Hours_ReturnsEvery4Hours()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromHours(4));
        Assert.Equal("0 */4 * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_6Hours_ReturnsEvery6Hours()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromHours(6));
        Assert.Equal("0 */6 * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_8Hours_ReturnsEvery8Hours()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromHours(8));
        Assert.Equal("0 */8 * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_12Hours_ReturnsEvery12Hours()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromHours(12));
        Assert.Equal("0 */12 * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_FractionalHours_TruncatesToWholeHours()
    {
        // 1.5 hours truncates to 1 hour, and 24 % 1 == 0, so it returns hourly
        // The implementation casts TotalHours to int, truncating the fractional part
        var result = ConvertIntervalToCron(TimeSpan.FromHours(1.5));
        Assert.Equal("0 */1 * * *", result);
    }

    #endregion

    #region Day-Based Intervals

    [Fact]
    public void ConvertIntervalToCron_1Day_ReturnsDailyAtMidnight()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromDays(1));
        Assert.Equal("0 0 * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_2Days_ReturnsEvery2Days()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromDays(2));
        Assert.Equal("0 0 */2 * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_3Days_ReturnsEvery3Days()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromDays(3));
        Assert.Equal("0 0 */3 * *", result);
    }

    #endregion

    #region Week-Based Intervals

    [Fact]
    public void ConvertIntervalToCron_1Week_ReturnsWeeklyOnSunday()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromDays(7));
        Assert.Equal("0 0 * * 0", result);
    }

    [Fact]
    public void ConvertIntervalToCron_2Weeks_ReturnsMonthly()
    {
        // Multiple weeks defaults to monthly approximation
        var result = ConvertIntervalToCron(TimeSpan.FromDays(14));
        Assert.Equal("0 0 1 * *", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ConvertIntervalToCron_SubMinute_ReturnsEveryMinute()
    {
        // Sub-minute intervals default to every minute
        var result = ConvertIntervalToCron(TimeSpan.FromSeconds(30));
        Assert.Equal("* * * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_10Seconds_ReturnsEveryMinute()
    {
        var result = ConvertIntervalToCron(TimeSpan.FromSeconds(10));
        Assert.Equal("* * * * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_LargeInterval_ReturnsMonthly()
    {
        // Very large intervals (> 2 weeks) default to monthly
        var result = ConvertIntervalToCron(TimeSpan.FromDays(30));
        Assert.Equal("0 0 1 * *", result);
    }

    [Fact]
    public void ConvertIntervalToCron_ZeroInterval_ReturnsEveryMinute()
    {
        // Zero interval should be treated as sub-minute
        var result = ConvertIntervalToCron(TimeSpan.Zero);
        Assert.Equal("* * * * *", result);
    }

    #endregion

    #region Theory Tests for Common Intervals

    [Theory]
    [InlineData(60, "*/1 * * * *")]           // 1 minute
    [InlineData(300, "*/5 * * * *")]          // 5 minutes
    [InlineData(600, "*/10 * * * *")]         // 10 minutes
    [InlineData(900, "*/15 * * * *")]         // 15 minutes
    [InlineData(1800, "*/30 * * * *")]        // 30 minutes
    [InlineData(3600, "0 */1 * * *")]         // 1 hour
    [InlineData(7200, "0 */2 * * *")]         // 2 hours
    [InlineData(21600, "0 */6 * * *")]        // 6 hours
    [InlineData(43200, "0 */12 * * *")]       // 12 hours
    [InlineData(86400, "0 0 * * *")]          // 1 day
    [InlineData(604800, "0 0 * * 0")]         // 1 week
    public void ConvertIntervalToCron_CommonIntervals_ReturnsExpectedCron(int seconds, string expectedCron)
    {
        var result = ConvertIntervalToCron(TimeSpan.FromSeconds(seconds));
        Assert.Equal(expectedCron, result);
    }

    #endregion
}
