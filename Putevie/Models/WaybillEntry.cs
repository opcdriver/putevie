namespace Putevie.Models;

public class WaybillEntry
{
    public DateTime Date { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>
    /// Номер колійного листа: держномер-місяць-порядковий (напр. AA1234BB-07-001).
    /// </summary>
    public string WaybillNumber { get; set; } = string.Empty;

    /// <summary>
    /// Порядковий номер у межах місяця.
    /// </summary>
    public int SequenceNumber { get; set; }

    public double DistanceKm { get; set; }
    public int OdometerDeparture { get; set; }
    public int OdometerReturn { get; set; }
    public double RefuelLiters { get; set; }
    public double FuelConsumptionActual { get; set; }
    public double FuelDeparture { get; set; }
    public double FuelReturn { get; set; }
    public int TemperatureDeparture { get; set; }
    public int TemperatureReturn { get; set; }

    public string DateDisplay => Date.ToString("dd.MM.yyyy");
    public string DistanceDisplay => $"{DistanceKm:F2} км";
    public string OdometerDisplay => $"{OdometerDeparture:N0} → {OdometerReturn:N0}";
    public string FuelDisplay => $"{FuelDeparture:F2} → {FuelReturn:F2} л";
    public string RefuelDisplay => RefuelLiters > 0 ? $"{RefuelLiters:F2} л" : "—";
    public string FuelConsumptionDisplay => $"{FuelConsumptionActual:F2}";
    public string TemperatureDisplay => $"{TemperatureDeparture}°C / {TemperatureReturn}°C";
}
