using System;
using System.Reflection;
using Xunit;

namespace CronRadar.Quartz.Tests;

public class SkipMonitorAttributeTests
{
    [Fact]
    public void SkipMonitorAttribute_CanBeAppliedToClass()
    {
        var classType = typeof(TestJobsWithSkip);
        var attribute = classType.GetCustomAttribute<SkipMonitorAttribute>();

        Assert.NotNull(attribute);
    }

    [Fact]
    public void SkipMonitorAttribute_IsNotPresentOnRegularClass()
    {
        var classType = typeof(RegularJob);
        var attribute = classType.GetCustomAttribute<SkipMonitorAttribute>();

        Assert.Null(attribute);
    }

    [Fact]
    public void SkipMonitorAttribute_AllowMultipleIsFalse()
    {
        var attributeUsage = typeof(SkipMonitorAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(attributeUsage);
        Assert.False(attributeUsage.AllowMultiple);
    }

    [Fact]
    public void SkipMonitorAttribute_TargetsClassesOnly()
    {
        var attributeUsage = typeof(SkipMonitorAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(attributeUsage);
        Assert.Equal(AttributeTargets.Class, attributeUsage.ValidOn);
    }

    // Test helper classes
    [SkipMonitor]
    private class TestJobsWithSkip { }

    private class RegularJob { }
}
