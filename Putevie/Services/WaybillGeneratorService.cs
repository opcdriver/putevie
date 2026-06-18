using Putevie.Models;



namespace Putevie.Services;



public class WaybillGeneratorService

{

    private const double MinDistanceKm = 0.01;



    private readonly Random _random = new();



    private static readonly Dictionary<int, double> OdesaMonthlyCorrection = new()

    {

        [1] = -1.0,

        [2] = -0.5,

        [3] = 0.0,

        [4] = 0.7,

        [5] = 1.2,

        [6] = 2.0,

        [7] = 2.4,

        [8] = 2.1,

        [9] = 1.3,

        [10] = 0.5,

        [11] = -0.2,

        [12] = -0.8

    };



    public GenerationResult Generate(GenerationRequest request)

    {

        var result = new GenerationResult();



        if (request.GenerateUntilDate < request.LastWaybillDate)

        {

            result.Warnings.Add("Дата генерації повинна бути пізніше останньої дати колійного листа.");

            return result;

        }



        if (request.FuelConsumptionNormPer100Km <= 0)

        {

            result.Warnings.Add("Норма витрати палива повинна бути більше нуля.");

            return result;

        }



        var refuelByDate = request.Refuels

            .GroupBy(r => r.Date.Date)

            .ToDictionary(g => g.Key, g => FuelMath.RoundLiters(g.Sum(r => r.Liters)));



        var periodStart = request.LastWaybillDate.Date.AddDays(1);

        var endDate = request.GenerateUntilDate.Date;

        var requiredRefuelDates = request.Refuels

            .Select(r => r.Date.Date)

            .Where(d => d >= periodStart && d <= endDate)

            .ToHashSet();



        var currentDate = periodStart;

        var odometerDeparture = request.InitialOdometer;

        var fuelDeparture = FuelMath.RoundLiters(request.FuelRemainingAtLastDate);



        if (fuelDeparture < FuelMath.MinimumFuelLiters)

        {

            result.Warnings.Add(

                $"Попередження: залишок палива підвищено до мінімуму {FuelMath.MinimumFuelLiters:F2} л.");

            fuelDeparture = FuelMath.MinimumFuelLiters;

        }



        while (currentDate <= endDate)

        {

            var hasScheduledRefuel = requiredRefuelDates.Contains(currentDate);

            var isExcludedDay = request.ExcludedDays.Contains(currentDate.DayOfWeek);



            if (isExcludedDay && !hasScheduledRefuel)

            {

                currentDate = currentDate.AddDays(1);

                continue;

            }



            var refuelLiters = FuelMath.RoundLiters(refuelByDate.GetValueOrDefault(currentDate, 0));

            var maxConsumable = FuelMath.MaxConsumableLiters(fuelDeparture, refuelLiters);



            if (maxConsumable <= 0 && !hasScheduledRefuel)

            {

                result.Warnings.Add(

                    $"Попередження: {currentDate:dd.MM.yyyy} — недостатньо палива (мін. залишок {FuelMath.MinimumFuelLiters:F2} л), день пропущено.");

                currentDate = currentDate.AddDays(1);

                continue;

            }



            var distanceKm = GenerateRandomDistanceKm();

            var plannedConsumption = FuelMath.ConsumptionFromDistance(distanceKm, request.FuelConsumptionNormPer100Km);

            var actualConsumption = plannedConsumption;



            if (actualConsumption > maxConsumable)

            {

                actualConsumption = maxConsumable;

                distanceKm = FuelMath.DistanceFromConsumption(actualConsumption, request.FuelConsumptionNormPer100Km);



                if (!hasScheduledRefuel)

                {

                    result.Warnings.Add(

                        $"Попередження: {currentDate:dd.MM.yyyy} — недостатньо палива. " +

                        $"Пробіг зменшено до {distanceKm:F2} км (доступно {FuelMath.RoundLiters(fuelDeparture + refuelLiters):F2} л, мін. залишок {FuelMath.MinimumFuelLiters:F2} л).");

                }



                if (distanceKm < MinDistanceKm && !hasScheduledRefuel)

                {

                    result.Warnings.Add($"Попередження: {currentDate:dd.MM.yyyy} — день пропущено через нестачу палива.");

                    currentDate = currentDate.AddDays(1);

                    continue;

                }



                // Не обнуляем расход в день с заправкой:
                // списываем топливо в рамках доступного остатка (с учетом ограничения по минимальному остатку).

            }



            var fuelReturn = FuelMath.RoundLiters(fuelDeparture + refuelLiters - actualConsumption);

            if (fuelReturn < FuelMath.MinimumFuelLiters)

            {

                actualConsumption = FuelMath.RoundLiters(fuelDeparture + refuelLiters - FuelMath.MinimumFuelLiters);

                distanceKm = FuelMath.DistanceFromConsumption(actualConsumption, request.FuelConsumptionNormPer100Km);

                fuelReturn = FuelMath.MinimumFuelLiters;

            }



            var odometerReturn = odometerDeparture + (int)Math.Round(distanceKm, MidpointRounding.AwayFromZero);

            var (tempDeparture, tempReturn) = GenerateTemperatures(currentDate);



            result.Entries.Add(new WaybillEntry

            {

                Date = currentDate,

                VehicleName = request.VehicleName,

                LicensePlate = request.LicensePlate,

                EmployeeName = request.EmployeeName,

                DistanceKm = FuelMath.RoundLiters(distanceKm),

                OdometerDeparture = odometerDeparture,

                OdometerReturn = odometerReturn,

                RefuelLiters = refuelLiters,

                FuelConsumptionActual = actualConsumption,

                FuelDeparture = fuelDeparture,

                FuelReturn = fuelReturn,

                TemperatureDeparture = tempDeparture,

                TemperatureReturn = tempReturn

            });



            requiredRefuelDates.Remove(currentDate);

            odometerDeparture = odometerReturn;

            fuelDeparture = fuelReturn;

            currentDate = currentDate.AddDays(1);

        }



        foreach (var missedRefuelDate in requiredRefuelDates.OrderBy(d => d))

        {

            var liters = refuelByDate.GetValueOrDefault(missedRefuelDate, 0);

            result.Warnings.Add(

                $"Помилка: заправку на {missedRefuelDate:dd.MM.yyyy} ({liters:F2} л) не вдалося застосувати до колійного листа.");

        }



        if (result.Entries.Count == 0 && result.Warnings.Count == 0)

        {

            result.Warnings.Add("Не згенеровано жодного колійного листа для вказаного періоду.");

        }



        return result;

    }



    private double GenerateRandomDistanceKm() =>

        _random.Next(4000, 15100) / 100.0;



    private (int Morning, int Evening) GenerateTemperatures(DateTime date)

    {

        var dayOfYear = date.DayOfYear;

        var monthCorrection = OdesaMonthlyCorrection.GetValueOrDefault(date.Month, 0);



        var annualMean = 11.5;

        var amplitude = 13.5;

        var seasonalBase = annualMean + amplitude * Math.Sin((2 * Math.PI * (dayOfYear - 110)) / 365.25);



        var morningRaw = seasonalBase - 2.0 + monthCorrection + NextDouble(-3.0, 3.0);

        var eveningRaw = seasonalBase + 2.2 + monthCorrection + NextDouble(-2.5, 3.5);

        var eveningAtLeast = morningRaw + NextDouble(1.0, 6.0);



        var morning = (int)Math.Round(Clamp(morningRaw, -18, 38), MidpointRounding.AwayFromZero);

        var evening = (int)Math.Round(Clamp(Math.Max(eveningRaw, eveningAtLeast), -16, 40), MidpointRounding.AwayFromZero);



        return (morning, evening);

    }



    private double NextDouble(double minInclusive, double maxInclusive) =>

        minInclusive + (_random.NextDouble() * (maxInclusive - minInclusive));



    private static double Clamp(double value, double min, double max) =>

        Math.Max(min, Math.Min(max, value));

}


