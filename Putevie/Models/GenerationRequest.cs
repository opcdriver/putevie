namespace Putevie.Models;

public class GenerationRequest
{
    public string VehicleName { get; set; } = string.Empty;
    public int YearOfManufacture { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public EngineType EngineType { get; set; }
    public int EngineVolumeCc { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime LastWaybillDate { get; set; }
    public double FuelRemainingAtLastDate { get; set; }
    public DateTime GenerateUntilDate { get; set; }
    public double FuelConsumptionNormPer100Km { get; set; }
    public int InitialOdometer { get; set; }
    public IList<RefuelEntry> Refuels { get; set; } = [];
    public HashSet<DayOfWeek> ExcludedDays { get; set; } = [];
}
