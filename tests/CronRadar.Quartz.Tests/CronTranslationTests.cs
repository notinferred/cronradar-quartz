using System.Reflection;
using Xunit;

namespace CronRadar.Quartz.Tests;

public class CronTranslationTests
{
    private static string? Translate(string? expression) =>
        (string?)typeof(QuartzExtensions).Assembly
            .GetType("CronRadar.Quartz.CronRadarQuartzSchedule")!
            .GetMethod("TranslateCronExpression", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { expression });

    [Theory]
    [InlineData("0 0 2 * * ?", "0 2 * * *")]
    [InlineData("0 0/15 * * * ?", "*/15 * * * *")]
    [InlineData("0 0 2 ? * MON-FRI", "0 2 * * 1-5")]
    [InlineData("0 /10 * * * ?", "*/10 * * * *")]
    [InlineData("0 5/20 * * * ?", "5/20 * * * *")]
    [InlineData("30 15 10 * * ?", "15 10 * * *")]
    [InlineData("0 0 9-17/2 * * ?", "0 9-17/2 * * *")]
    [InlineData("0 0 6,18 * * ?", "0 6,18 * * *")]
    [InlineData("0 0 0 1/2 * ?", "0 0 */2 * *")]
    [InlineData("0 0 0 1 JAN ?", "0 0 1 JAN *")]
    [InlineData("0 0 0 ? 1/3 *", "0 0 * */3 *")]
    [InlineData("0 0 2 * * ? *", "0 2 * * *")]
    [InlineData("0 0 2 ? * 1", "0 2 * * 0")]
    [InlineData("0 0 2 ? * 7", "0 2 * * 6")]
    [InlineData("0 0 2 ? * 2-6", "0 2 * * 1-5")]
    [InlineData("0 0 2 ? * SUN,SAT", "0 2 * * 0,6")]
    [InlineData("0 0 2 ? * FRI-MON", "0 2 * * 0-1,5-6")]
    [InlineData("0 0 2 ? * 2/2", "0 2 * * 1,3,5")]
    [InlineData("0 0 2 ? * *", "0 2 * * *")]
    [InlineData("0 0 2 ? * mon", "0 2 * * 1")]
    public void Translates_expressions_five_field_cron_can_represent(string quartz, string expected)
    {
        Assert.Equal(expected, Translate(quartz));
    }

    [Theory]
    [InlineData("0/30 * * * * ?")]
    [InlineData("* * * * * ?")]
    [InlineData("0,30 * * * * ?")]
    [InlineData("0 0 0 L * ?")]
    [InlineData("0 0 0 15W * ?")]
    [InlineData("0 0 0 LW * ?")]
    [InlineData("0 0 0 ? * 6L")]
    [InlineData("0 0 0 ? * L")]
    [InlineData("0 0 0 ? * 6#3")]
    [InlineData("0 0 2 * * ? 2030")]
    [InlineData("0 0 2 ? * 8")]
    [InlineData("0 0 2 ? * NOPE")]
    [InlineData("0 0 2 * *")]
    [InlineData("0 0 2 * * ? * extra")]
    [InlineData("")]
    [InlineData(null)]
    public void Returns_null_when_five_field_cron_cannot_represent_the_schedule(string? quartz)
    {
        Assert.Null(Translate(quartz));
    }
}
