namespace Putevie.Services;

public static class FuelMath
{
    public const double MinimumFuelLiters = 1.0;

    public static double RoundLiters(double liters) =>
        Math.Round(liters, 2, MidpointRounding.AwayFromZero);

    public static double MaxConsumableLiters(double fuelDeparture, double refuelLiters) =>
        RoundLiters(fuelDeparture + refuelLiters - MinimumFuelLiters);

    public static double MaxConsumableLiters(double fuelDeparture, double refuelLiters, double minimumRemaining) =>
        RoundLiters(fuelDeparture + refuelLiters - minimumRemaining);

    public static double DistanceFromConsumption(double consumptionLiters, double normPer100Km) =>
        normPer100Km <= 0
            ? 0
            : Math.Round(consumptionLiters * 100.0 / normPer100Km, 2, MidpointRounding.AwayFromZero);

    public static double ConsumptionFromDistance(double distanceKm, double normPer100Km) =>
        RoundLiters(distanceKm * normPer100Km / 100.0);
}
