using MqttSecurity.Core.Authorization;
using Xunit;

namespace MqttSecurity.Tests.Authorization;

public class MqttTopicMatcherTests
{
    private readonly MqttTopicMatcher _matcher = new();

    [Theory]
    [InlineData("device/123/status", "device/123/status", true)]
    [InlineData("device/123/status", "device/+/status", true)]
    [InlineData("device/123/status", "device/#", true)]
    [InlineData("device/123/status", "device/+/#", true)]
    [InlineData("device/diagnostic/123", "device/diagnostic/#", true)]
    [InlineData("device", "device/#", true)]
    [InlineData("device", "#", true)]
    [InlineData("device/", "device/+", true)]

    [InlineData("device/123/status", "device/456/status", false)]
    [InlineData("device/123/status", "device/+", false)]
    [InlineData("device/123/status", "admin/#", false)]
    [InlineData("device", "device/+", false)]
    [InlineData("device/123/status", "device/+/diagnostic", false)]

    [InlineData("$SYS/status", "#", false)]
    [InlineData("$SYS/status", "+/#", false)]
    [InlineData("$SYS/status", "$SYS/#", true)]
    public void Matches_ShouldReturnExpectedResult(
        string topic,
        string filter,
        bool expected)
    {
        var result = _matcher.Matches(topic, filter);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("device/#", "device/#", true)]
    [InlineData("device/+", "device/+", true)]
    [InlineData("device/123", "device/123", true)]

    [InlineData("device/#", "device/123/#", true)]
    [InlineData("device/#", "device/123/status", true)]
    [InlineData("device/#", "device/+/status", true)]
    [InlineData("device/#", "device", true)]
    [InlineData("device/+", "device/123", true)]
    [InlineData("device/+/#", "device/123/status", true)]
    [InlineData("device/+/status", "device/123/status", true)]
    [InlineData("#", "device/#", true)]

    [InlineData("device/123/#", "device/#", false)]
    [InlineData("device/+", "device/#", false)]
    [InlineData("device/123/status", "device/+/status", false)]
    [InlineData("device/123/#", "device/456/#", false)]
    [InlineData("device/+", "device/123/#", false)]
    [InlineData("device/+/status", "device/#", false)]
    [InlineData("device/123", "device/+", false)]

    [InlineData("#", "$SYS/#", false)]
    [InlineData("$SYS/#", "$SYS/status", true)]
    [InlineData("$SYS/#", "device/#", false)]
    public void CoversFilter_ShouldReturnExpectedResult(
    string allowedFilter,
    string requestedFilter,
    bool expected)
    {
        var result = _matcher.CoversFilter(
            allowedFilter,
            requestedFilter);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("device/#/status")]
    [InlineData("device/+status")]
    [InlineData("device/status+")]
    [InlineData("device/#test")]
    [InlineData("device/te#st")]
    [InlineData("")]
    [InlineData("device/\0/status")]
    [InlineData("$share/group/device/#")]
    public void Matches_ShouldRejectInvalidFilters(
    string invalidFilter)
    {
        var result = _matcher.Matches(
            "device/123/status",
            invalidFilter);

        Assert.False(result);
    }

    [Theory]
    [InlineData("device/#/status")]
    [InlineData("device/+status")]
    [InlineData("device/#test")]
    [InlineData("")]
    [InlineData("$share/group/device/#")]
    public void CoversFilter_ShouldRejectInvalidFilters(
        string invalidFilter)
    {
        Assert.False(_matcher.CoversFilter(
            invalidFilter, "device/123/status"));

        Assert.False(_matcher.CoversFilter(
            "device/#", invalidFilter));
    }
}