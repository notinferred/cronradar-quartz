using System;
using System.Reflection;
using Xunit;

namespace CronRadar.Quartz.Tests;

public class CronRadarAttributeTests
{
    [Fact]
    public void CronRadarAttribute_CanBeCreatedWithMonitorKey()
    {
        var attribute = new CronRadarAttribute("test-job");
        Assert.NotNull(attribute);
        Assert.Equal("test-job", attribute.MonitorKey);
    }

    [Fact]
    public void CronRadarAttribute_MonitorKeyIsStored()
    {
        var attribute = new CronRadarAttribute("my-custom-key");
        Assert.Equal("my-custom-key", attribute.MonitorKey);
    }

    [Fact]
    public void CronRadarAttribute_CanBeAppliedToClass()
    {
        var classType = typeof(TestJobWithCustomKey);
        var attribute = classType.GetCustomAttribute<CronRadarAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("custom-job-key", attribute.MonitorKey);
    }

    [Fact]
    public void CronRadarAttribute_AllowMultipleIsFalse()
    {
        var attributeUsage = typeof(CronRadarAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(attributeUsage);
        Assert.False(attributeUsage.AllowMultiple);
    }

    [Fact]
    public void CronRadarAttribute_TargetsClassesOnly()
    {
        var attributeUsage = typeof(CronRadarAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(attributeUsage);
        Assert.Equal(AttributeTargets.Class, attributeUsage.ValidOn);
    }

    [Fact]
    public void CronRadarAttribute_IsNotPresentOnRegularClass()
    {
        var classType = typeof(RegularJob);
        var attribute = classType.GetCustomAttribute<CronRadarAttribute>();

        Assert.Null(attribute);
    }

    [Theory]
    [InlineData("simple-key")]
    [InlineData("key-with-numbers-123")]
    [InlineData("very-long-key-name-for-a-specific-job-type")]
    public void CronRadarAttribute_AcceptsVariousKeyFormats(string key)
    {
        var attribute = new CronRadarAttribute(key);
        Assert.Equal(key, attribute.MonitorKey);
    }

    // Test helper classes
    [CronRadar("custom-job-key")]
    private class TestJobWithCustomKey { }

    private class RegularJob { }
}
