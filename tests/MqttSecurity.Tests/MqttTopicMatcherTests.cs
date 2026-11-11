using MqttSecurity.Core.Authorization;
using MqttSecurity.Core.Validation;

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
    [InlineData("$SYS/status", "$SYS/#", false)]
    public void Matches_ShouldReturnExpectedResult(
        string topic,
        string filter,
        bool expected)
    {
        var result = _matcher.Matches(topic, filter);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("device/#", "device/123/#", true)]
    [InlineData("device/#", "device/+/status", true)]
    [InlineData("device/#", "device", true)]
    [InlineData("device/+", "device/123", true)]
    [InlineData("device/+", "device/123/#", false)]
    [InlineData("device/123/#", "device/#", false)]
    [InlineData("device/123", "device/+", false)]
    [InlineData("device/+", "device/#", false)]
    [InlineData("device/+/status", "device/123/status", true)]
    [InlineData("device/+/status", "device/#", false)]
    [InlineData("#", "device/#", true)]
    [InlineData("+/#", "#", true)]
    [InlineData("#", "+/#", true)]
    [InlineData("device/#", "$SYS/#", false)]
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

    [Theory]
    [InlineData("device/#", true)]
    [InlineData("device/+/status", true)]
    [InlineData("device//status", true)]
    [InlineData("#", true)]
    [InlineData("+", true)]
    [InlineData("/device/#", true)]
    [InlineData("device/#/status", false)]
    [InlineData("device/+status", false)]
    [InlineData("device/abc+", false)]
    [InlineData("device/#abc", false)]
    [InlineData("$SYS/#", false)]
    [InlineData("$share/g/device/#", false)]
    [InlineData("", false)]
    public void IsValidFilter_ShouldValidateCorrectly(
    string filter,
    bool expected)
    {
        Assert.Equal(
            expected,
            MqttTopicValidator.IsValidFilter(filter));
    }


}