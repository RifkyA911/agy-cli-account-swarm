using AgyAccountSwarm.Services;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class AuthDetectorQuotaTests
{
    [Theory]
    [InlineData("Basic", 100)]
    [InlineData("free", 100)]
    [InlineData("unverified", 100)]
    [InlineData("Plus", 300)]
    [InlineData("Pro", 1000)]
    [InlineData("Ultra", 2500)]
    [InlineData("enterprise", 2500)]
    public void GetDailyQuotaForTier_ReturnsAccurateTierAllowance(string tier, int expectedQuota)
    {
        int quota = AuthDetectorService.GetDailyQuotaForTier(tier);
        Assert.Equal(expectedQuota, quota);
    }

    [Theory]
    [InlineData("Basic", 500)]
    [InlineData("Plus", 1500)]
    [InlineData("Pro", 5000)]
    [InlineData("Ultra", 12500)]
    public void GetWeeklyQuotaForTier_ReturnsAccurateWeeklyAllowance(string tier, int expectedWeekly)
    {
        int weekly = AuthDetectorService.GetWeeklyQuotaForTier(tier);
        Assert.Equal(expectedWeekly, weekly);
    }

    [Theory]
    [InlineData("Basic", 500000L)]
    [InlineData("Plus", 1500000L)]
    [InlineData("Pro", 5000000L)]
    [InlineData("Ultra", 15000000L)]
    public void GetDailyTokensLimitForTier_ReturnsAccurateTokenAllowance(string tier, long expectedTokens)
    {
        long tokens = AuthDetectorService.GetDailyTokensLimitForTier(tier);
        Assert.Equal(expectedTokens, tokens);
    }

    [Fact]
    public void BuildAsciiProgressBar_ReturnsExpectedLengthAndFill()
    {
        string zeroBar = AuthDetectorService.BuildAsciiProgressBar(0.0, 50);
        Assert.Equal(50, zeroBar.Length);
        Assert.Equal(new string('░', 50), zeroBar);

        string fullBar = AuthDetectorService.BuildAsciiProgressBar(100.0, 50);
        Assert.Equal(50, fullBar.Length);
        Assert.Equal(new string('█', 50), fullBar);

        string halfBar = AuthDetectorService.BuildAsciiProgressBar(50.0, 50);
        Assert.Equal(50, halfBar.Length);
        Assert.Equal(new string('█', 25) + new string('░', 25), halfBar);
    }
}
