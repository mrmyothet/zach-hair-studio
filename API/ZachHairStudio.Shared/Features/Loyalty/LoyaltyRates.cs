namespace ZachHairStudio.Shared.Features.Loyalty;

/// <summary>MVP loyalty rates (D-16). Tune only via these constants.</summary>
public static class LoyaltyRates
{
    public const int PointsPerCompletedAppointment = 1;

    public const int RedeemBlockPoints = 10;

    public const decimal RedeemBlockDollars = 5m;

    /// <summary>Server dollars for a redeem request: floor(points/10)*$5.</summary>
    public static decimal DollarsForPoints(int points)
    {
        if (points <= 0) return 0m;
        var blocks = points / RedeemBlockPoints;
        return blocks * RedeemBlockDollars;
    }
}
