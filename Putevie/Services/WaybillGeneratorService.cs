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

        var targetFuel = FuelMath.RoundLiters(request.TargetFuelRemainingAfterGeneration);
        if (targetFuel < 0)
        {
            result.Warnings.Add("Цільовий залишок палива після генерації не може бути від'ємним.");
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
        var sequenceNumber = Math.Max(1, request.StartingSequenceNumber);
        var currentNumberingMonth = -1;

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

            if (currentDate.Month != currentNumberingMonth)
            {
                // Новий місяць у номері — порядковий з початку (або стартовий для першого місяця).
                sequenceNumber = currentNumberingMonth < 0
                    ? Math.Max(1, request.StartingSequenceNumber)
                    : 1;
                currentNumberingMonth = currentDate.Month;
            }

            var waybillNumber = WaybillNumbering.Format(
                request.LicensePlate,
                currentDate,
                sequenceNumber);

            result.Entries.Add(new WaybillEntry
            {
                Date = currentDate,
                VehicleName = request.VehicleName,
                LicensePlate = request.LicensePlate,
                EmployeeName = request.EmployeeName,
                WaybillNumber = waybillNumber,
                SequenceNumber = sequenceNumber,
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

            sequenceNumber++;
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

        if (result.Entries.Count > 0)
        {
            ApplyTargetFuelRemaining(result, request.FuelConsumptionNormPer100Km, targetFuel);
        }

        return result;
    }

    /// <summary>
    /// Списує надлишок палива так, щоб залишок у баку після останнього
    /// колійного листа дорівнював цільовому значенню.
    /// </summary>
    internal static void ApplyTargetFuelRemaining(
        GenerationResult result,
        double fuelConsumptionNormPer100Km,
        double targetFuelRemaining)
    {
        var entries = result.Entries;
        if (entries.Count == 0)
        {
            return;
        }

        var targetFuel = FuelMath.RoundLiters(targetFuelRemaining);
        var actualFinal = FuelMath.RoundLiters(entries[^1].FuelReturn);
        var excess = FuelMath.RoundLiters(actualFinal - targetFuel);

        if (excess == 0)
        {
            return;
        }

        if (excess < 0)
        {
            result.Warnings.Add(
                $"Попередження: фактичний залишок після генерації ({actualFinal:F2} л) менший за цільовий ({targetFuel:F2} л). " +
                "Збільшити залишок без зменшення витрат неможливо — цільове значення не застосовано.");
            return;
        }

        var extraConsumption = DistributeWriteOff(entries, excess, targetFuel);
        RebuildFuelChain(entries, extraConsumption, fuelConsumptionNormPer100Km, targetFuel);

        var last = entries[^1];
        var leftover = FuelMath.RoundLiters(last.FuelReturn - targetFuel);

        if (leftover != 0)
        {
            var adjustedConsumption = FuelMath.RoundLiters(
                last.FuelDeparture + last.RefuelLiters - targetFuel);
            var maxConsumable = FuelMath.MaxConsumableLiters(
                last.FuelDeparture,
                last.RefuelLiters,
                targetFuel);

            if (adjustedConsumption >= 0 && adjustedConsumption <= maxConsumable + 0.001)
            {
                last.FuelConsumptionActual = Math.Min(adjustedConsumption, maxConsumable);
                last.FuelReturn = FuelMath.RoundLiters(
                    last.FuelDeparture + last.RefuelLiters - last.FuelConsumptionActual);

                // Якщо через округлення ще є розбіжність — фіксуємо ціль, коли це можливо.
                if (Math.Abs(last.FuelReturn - targetFuel) <= 0.01
                    || last.FuelDeparture + last.RefuelLiters - last.FuelConsumptionActual >= targetFuel - 0.01)
                {
                    last.FuelConsumptionActual = FuelMath.RoundLiters(
                        last.FuelDeparture + last.RefuelLiters - targetFuel);
                    last.FuelReturn = targetFuel;
                }

                last.DistanceKm = FuelMath.DistanceFromConsumption(
                    last.FuelConsumptionActual,
                    fuelConsumptionNormPer100Km);
                last.OdometerReturn = last.OdometerDeparture
                    + (int)Math.Round(last.DistanceKm, MidpointRounding.AwayFromZero);
                leftover = FuelMath.RoundLiters(last.FuelReturn - targetFuel);
            }
        }

        if (leftover > 0.01)
        {
            result.Warnings.Add(
                $"Попередження: не вдалося повністю списати надлишок палива. " +
                $"Залишок після генерації: {FuelMath.RoundLiters(last.FuelReturn):F2} л (ціль: {targetFuel:F2} л).");
        }
    }

    private static double[] DistributeWriteOff(
        IReadOnlyList<WaybillEntry> entries,
        double excess,
        double targetFuel)
    {
        var extras = new double[entries.Count];
        var remaining = excess;

        // Пропорційно до поточної витрати, щоб списання виглядало природно.
        var totalConsumption = entries.Sum(e => e.FuelConsumptionActual);
        if (totalConsumption > 0)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var share = FuelMath.RoundLiters(excess * entries[i].FuelConsumptionActual / totalConsumption);
                extras[i] = share;
                remaining = FuelMath.RoundLiters(remaining - share);
            }
        }

        // Залишок від округлення / нульових витрат — на останній день.
        if (remaining != 0)
        {
            extras[^1] = FuelMath.RoundLiters(extras[^1] + remaining);
        }

        // Обрізаємо частки, які неможливо списати в день (мін. залишок / ціль).
        // Точний контроль буде в RebuildFuelChain; тут лише груба оцінка по вихідних даних.
        for (var i = 0; i < entries.Count; i++)
        {
            var floor = i == entries.Count - 1 ? targetFuel : FuelMath.MinimumFuelLiters;
            var maxAdditional = FuelMath.RoundLiters(entries[i].FuelReturn - floor);
            if (extras[i] > maxAdditional && maxAdditional >= 0)
            {
                var overflow = FuelMath.RoundLiters(extras[i] - maxAdditional);
                extras[i] = maxAdditional;
                if (i > 0)
                {
                    extras[i - 1] = FuelMath.RoundLiters(extras[i - 1] + overflow);
                }
            }
        }

        return extras;
    }

    private static void RebuildFuelChain(
        IList<WaybillEntry> entries,
        IReadOnlyList<double> extraConsumption,
        double fuelConsumptionNormPer100Km,
        double targetFuel)
    {
        var fuel = entries[0].FuelDeparture;
        var odometer = entries[0].OdometerDeparture;
        var deferredWriteOff = 0.0;

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            entry.FuelDeparture = fuel;
            entry.OdometerDeparture = odometer;

            var desired = FuelMath.RoundLiters(
                entry.FuelConsumptionActual + extraConsumption[i] + deferredWriteOff);
            deferredWriteOff = 0;

            var dayFloor = i == entries.Count - 1 ? targetFuel : FuelMath.MinimumFuelLiters;
            var maxConsumable = FuelMath.MaxConsumableLiters(
                entry.FuelDeparture,
                entry.RefuelLiters,
                dayFloor);

            if (desired > maxConsumable)
            {
                deferredWriteOff = FuelMath.RoundLiters(desired - maxConsumable);
                desired = Math.Max(0, maxConsumable);
            }

            entry.FuelConsumptionActual = desired;
            entry.FuelReturn = FuelMath.RoundLiters(
                entry.FuelDeparture + entry.RefuelLiters - entry.FuelConsumptionActual);
            entry.DistanceKm = FuelMath.DistanceFromConsumption(
                entry.FuelConsumptionActual,
                fuelConsumptionNormPer100Km);
            entry.OdometerReturn = entry.OdometerDeparture
                + (int)Math.Round(entry.DistanceKm, MidpointRounding.AwayFromZero);

            fuel = entry.FuelReturn;
            odometer = entry.OdometerReturn;
        }

        // Якщо deferred лишився після останнього дня — вже не вмістився.
        if (deferredWriteOff > 0 && entries.Count > 0)
        {
            // Спробуємо дописати з кінця назад у дні, де ще є запас.
            for (var i = entries.Count - 1; i >= 0 && deferredWriteOff > 0; i--)
            {
                var entry = entries[i];
                var dayFloor = i == entries.Count - 1 ? targetFuel : FuelMath.MinimumFuelLiters;
                var maxConsumable = FuelMath.MaxConsumableLiters(
                    entry.FuelDeparture,
                    entry.RefuelLiters,
                    dayFloor);
                var available = FuelMath.RoundLiters(maxConsumable - entry.FuelConsumptionActual);
                if (available <= 0)
                {
                    continue;
                }

                var add = Math.Min(available, deferredWriteOff);
                entry.FuelConsumptionActual = FuelMath.RoundLiters(entry.FuelConsumptionActual + add);
                deferredWriteOff = FuelMath.RoundLiters(deferredWriteOff - add);
            }

            // Перерахунок ланцюга після дописування.
            fuel = entries[0].FuelDeparture;
            odometer = entries[0].OdometerDeparture;
            foreach (var entry in entries)
            {
                entry.FuelDeparture = fuel;
                entry.OdometerDeparture = odometer;
                entry.FuelReturn = FuelMath.RoundLiters(
                    entry.FuelDeparture + entry.RefuelLiters - entry.FuelConsumptionActual);
                entry.DistanceKm = FuelMath.DistanceFromConsumption(
                    entry.FuelConsumptionActual,
                    fuelConsumptionNormPer100Km);
                entry.OdometerReturn = entry.OdometerDeparture
                    + (int)Math.Round(entry.DistanceKm, MidpointRounding.AwayFromZero);
                fuel = entry.FuelReturn;
                odometer = entry.OdometerReturn;
            }
        }
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
