using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Putevie.Models;
using Putevie.Services;
using Putevie.Views;

namespace Putevie.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly WaybillGeneratorService _generatorService = new();

    private string _vehicleName = "Toyota Camry";
    private int _yearOfManufacture = 2018;
    private string _licensePlate = "AA1234BB";
    private EngineType _selectedEngineType = EngineType.Petrol;
    private int _engineVolumeCc = 1998;
    private string _employeeName = "Іваненко Іван Іванович";
    private DateTime _lastWaybillDate = DateTime.Today.AddDays(-7);
    private double _fuelRemainingAtLastDate = 35.50;
    private double _lastTargetFuelRemaining = 10.00;
    private DateTime _generateUntilDate = DateTime.Today;
    private double _fuelConsumptionNorm = 8.45;
    private int _initialOdometer = 100_000;
    private bool _skipMonday;
    private bool _skipTuesday;
    private bool _skipWednesday;
    private bool _skipThursday;
    private bool _skipFriday;
    private bool _skipSaturday = true;
    private bool _skipSunday = true;
    private bool _isGenerating;
    private string _statusMessage = "Готово до генерації";
    private string _warningsText = string.Empty;

    public MainViewModel()
    {
        RefuelEntries = new ObservableCollection<RefuelEntryViewModel>();
        GeneratedWaybills = new ObservableCollection<WaybillEntry>();
        EngineTypes = Enum.GetValues<EngineType>();

        AddRefuelCommand = new RelayCommand(AddRefuel);
        RemoveRefuelCommand = new RelayCommand<RefuelEntryViewModel>(RemoveRefuel);
        GenerateCommand = new RelayCommand(async () => await GenerateAsync(), () => !IsGenerating);
    }

    public ObservableCollection<RefuelEntryViewModel> RefuelEntries { get; }
    public ObservableCollection<WaybillEntry> GeneratedWaybills { get; }
    public Array EngineTypes { get; }

    public string VehicleName
    {
        get => _vehicleName;
        set => SetProperty(ref _vehicleName, value);
    }

    public int YearOfManufacture
    {
        get => _yearOfManufacture;
        set => SetProperty(ref _yearOfManufacture, value);
    }

    public string LicensePlate
    {
        get => _licensePlate;
        set => SetProperty(ref _licensePlate, value);
    }

    public EngineType SelectedEngineType
    {
        get => _selectedEngineType;
        set => SetProperty(ref _selectedEngineType, value);
    }

    public int EngineVolumeCc
    {
        get => _engineVolumeCc;
        set => SetProperty(ref _engineVolumeCc, value);
    }

    public string EmployeeName
    {
        get => _employeeName;
        set => SetProperty(ref _employeeName, value);
    }

    public DateTime LastWaybillDate
    {
        get => _lastWaybillDate;
        set => SetProperty(ref _lastWaybillDate, value);
    }

    public double FuelRemainingAtLastDate
    {
        get => _fuelRemainingAtLastDate;
        set => SetProperty(ref _fuelRemainingAtLastDate, value);
    }

    public DateTime GenerateUntilDate
    {
        get => _generateUntilDate;
        set => SetProperty(ref _generateUntilDate, value);
    }

    public double FuelConsumptionNorm
    {
        get => _fuelConsumptionNorm;
        set => SetProperty(ref _fuelConsumptionNorm, value);
    }

    public int InitialOdometer
    {
        get => _initialOdometer;
        set => SetProperty(ref _initialOdometer, value);
    }

    public bool SkipMonday
    {
        get => _skipMonday;
        set => SetProperty(ref _skipMonday, value);
    }

    public bool SkipTuesday
    {
        get => _skipTuesday;
        set => SetProperty(ref _skipTuesday, value);
    }

    public bool SkipWednesday
    {
        get => _skipWednesday;
        set => SetProperty(ref _skipWednesday, value);
    }

    public bool SkipThursday
    {
        get => _skipThursday;
        set => SetProperty(ref _skipThursday, value);
    }

    public bool SkipFriday
    {
        get => _skipFriday;
        set => SetProperty(ref _skipFriday, value);
    }

    public bool SkipSaturday
    {
        get => _skipSaturday;
        set => SetProperty(ref _skipSaturday, value);
    }

    public bool SkipSunday
    {
        get => _skipSunday;
        set => SetProperty(ref _skipSunday, value);
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        private set
        {
            if (SetProperty(ref _isGenerating, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string WarningsText
    {
        get => _warningsText;
        set => SetProperty(ref _warningsText, value);
    }

    public ICommand AddRefuelCommand { get; }
    public ICommand RemoveRefuelCommand { get; }
    public ICommand GenerateCommand { get; }

    private void AddRefuel()
    {
        RefuelEntries.Add(new RefuelEntryViewModel
        {
            Date = DateTime.Today,
            Liters = 30.25
        });
    }

    private void RemoveRefuel(RefuelEntryViewModel? entry)
    {
        if (entry is not null)
        {
            RefuelEntries.Remove(entry);
        }
    }

    private async Task GenerateAsync()
    {
        try
        {
            var targetFuel = PromptTargetFuelRemaining();
            if (targetFuel is null)
            {
                StatusMessage = "Генерацію скасовано.";
                return;
            }

            IsGenerating = true;
            StatusMessage = "Генерація колійних листів...";
            WarningsText = string.Empty;

            var request = BuildRequest(targetFuel.Value);
            var result = await Task.Run(() => _generatorService.Generate(request));

            GeneratedWaybills.Clear();
            foreach (var entry in result.Entries)
            {
                GeneratedWaybills.Add(entry);
            }

            WarningsText = result.Warnings.Count > 0
                ? string.Join(Environment.NewLine, result.Warnings)
                : string.Empty;

            var finalFuel = result.Entries.Count > 0
                ? result.Entries[^1].FuelReturn
                : (double?)null;

            StatusMessage = result.Entries.Count > 0
                ? $"Згенеровано {result.Entries.Count} колійних листів. Залишок у баку: {finalFuel:F2} л (ціль: {targetFuel.Value:F2} л)."
                : "Генерацію завершено без результатів.";

            AppLogger.LogInfo(
                $"Generated {result.Entries.Count} waybills with {result.Warnings.Count} warnings. " +
                $"Target fuel remaining: {targetFuel.Value:F2} L.");
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Помилка генерації колійних листів", ex);
            StatusMessage = "Сталася помилка під час генерації.";
            MessageBox.Show(
                $"Не вдалося згенерувати колійні листи:\n{ex.Message}",
                "Помилка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private double? PromptTargetFuelRemaining()
    {
        try
        {
            var dialogViewModel = new TargetFuelRemainingViewModel(_lastTargetFuelRemaining);
            var dialog = new TargetFuelRemainingWindow(dialogViewModel)
            {
                Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                        ?? Application.Current?.MainWindow
            };

            var confirmed = dialog.ShowDialog() == true;
            if (!confirmed)
            {
                return null;
            }

            _lastTargetFuelRemaining = FuelMath.RoundLiters(dialogViewModel.TargetFuelRemaining);
            return _lastTargetFuelRemaining;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Помилка відкриття вікна залишку палива", ex);
            MessageBox.Show(
                $"Не вдалося відкрити вікно введення залишку палива:\n{ex.Message}",
                "Помилка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return null;
        }
    }

    private GenerationRequest BuildRequest(double targetFuelRemainingAfterGeneration)
    {
        var excludedDays = new HashSet<DayOfWeek>();
        if (SkipMonday) excludedDays.Add(DayOfWeek.Monday);
        if (SkipTuesday) excludedDays.Add(DayOfWeek.Tuesday);
        if (SkipWednesday) excludedDays.Add(DayOfWeek.Wednesday);
        if (SkipThursday) excludedDays.Add(DayOfWeek.Thursday);
        if (SkipFriday) excludedDays.Add(DayOfWeek.Friday);
        if (SkipSaturday) excludedDays.Add(DayOfWeek.Saturday);
        if (SkipSunday) excludedDays.Add(DayOfWeek.Sunday);

        return new GenerationRequest
        {
            VehicleName = VehicleName,
            YearOfManufacture = YearOfManufacture,
            LicensePlate = LicensePlate,
            EngineType = SelectedEngineType,
            EngineVolumeCc = EngineVolumeCc,
            EmployeeName = EmployeeName,
            LastWaybillDate = LastWaybillDate,
            FuelRemainingAtLastDate = FuelMath.RoundLiters(FuelRemainingAtLastDate),
            GenerateUntilDate = GenerateUntilDate,
            FuelConsumptionNormPer100Km = FuelMath.RoundLiters(FuelConsumptionNorm),
            InitialOdometer = InitialOdometer,
            TargetFuelRemainingAfterGeneration = FuelMath.RoundLiters(targetFuelRemainingAfterGeneration),
            Refuels = RefuelEntries
                .Select(r => new RefuelEntry
                {
                    Date = r.Date,
                    Liters = FuelMath.RoundLiters(r.Liters)
                })
                .ToList(),
            ExcludedDays = excludedDays
        };
    }
}
