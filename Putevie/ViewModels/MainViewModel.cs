using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Putevie.Models;
using Putevie.Services;
using Putevie.Views;

namespace Putevie.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly WaybillGeneratorService _generatorService = new();
    private readonly WaybillDocumentExportService _documentExportService = new();

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
    private int _startingSequenceNumber = 1;
    private bool _skipMonday;
    private bool _skipTuesday;
    private bool _skipWednesday;
    private bool _skipThursday;
    private bool _skipFriday;
    private bool _skipSaturday = true;
    private bool _skipSunday = true;
    private bool _isGenerating;
    private bool _isExporting;
    private string _statusMessage = "Готово до генерації";
    private string _warningsText = string.Empty;
    private string? _lastExportDirectory;

    public MainViewModel()
    {
        RefuelEntries = new ObservableCollection<RefuelEntryViewModel>();
        GeneratedWaybills = new ObservableCollection<WaybillEntry>();
        EngineTypes = Enum.GetValues<EngineType>();

        AddRefuelCommand = new RelayCommand(AddRefuel);
        RemoveRefuelCommand = new RelayCommand<RefuelEntryViewModel>(RemoveRefuel);
        GenerateCommand = new RelayCommand(async () => await GenerateAsync(), () => !IsGenerating && !IsExporting);
        ExportDocumentsCommand = new RelayCommand(
            async () => await ExportDocumentsAsync(),
            () => !IsGenerating && !IsExporting && GeneratedWaybills.Count > 0);
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

    public int StartingSequenceNumber
    {
        get => _startingSequenceNumber;
        set => SetProperty(ref _startingSequenceNumber, Math.Max(1, value));
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

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (SetProperty(ref _isExporting, value))
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
    public ICommand ExportDocumentsCommand { get; }

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

            CommandManager.InvalidateRequerySuggested();

            WarningsText = result.Warnings.Count > 0
                ? string.Join(Environment.NewLine, result.Warnings)
                : string.Empty;

            var finalFuel = result.Entries.Count > 0
                ? result.Entries[^1].FuelReturn
                : (double?)null;

            var numbersPreview = result.Entries.Count > 0
                ? $" Номери: {result.Entries[0].WaybillNumber}" +
                  (result.Entries.Count > 1 ? $" … {result.Entries[^1].WaybillNumber}" : string.Empty) + "."
                : string.Empty;

            StatusMessage = result.Entries.Count > 0
                ? $"Згенеровано {result.Entries.Count} колійних листів. Залишок у баку: {finalFuel:F2} л (ціль: {targetFuel.Value:F2} л).{numbersPreview}"
                : "Генерацію завершено без результатів.";

            // Автоматично пропонуємо наступний порядковий номер після останнього в цьому місяці.
            if (result.Entries.Count > 0)
            {
                var last = result.Entries[^1];
                StartingSequenceNumber = last.SequenceNumber + 1;
            }

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

    private async Task ExportDocumentsAsync()
    {
        try
        {
            if (GeneratedWaybills.Count == 0)
            {
                StatusMessage = "Немає згенерованих колійних листів для експорту.";
                return;
            }

            var dialog = new OpenFolderDialog
            {
                Title = "Оберіть теку для збереження колійних листів",
                Multiselect = false
            };

            if (!string.IsNullOrWhiteSpace(_lastExportDirectory) && Directory.Exists(_lastExportDirectory))
            {
                dialog.InitialDirectory = _lastExportDirectory;
            }

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                StatusMessage = "Експорт скасовано.";
                return;
            }

            _lastExportDirectory = dialog.FolderName;
            IsExporting = true;
            StatusMessage = "Формування документів за шаблоном...";

            var entries = GeneratedWaybills.ToList();
            var outputDirectory = dialog.FolderName;
            var exportResult = await Task.Run(() => _documentExportService.Export(entries, outputDirectory));

            if (exportResult.Errors.Count > 0)
            {
                WarningsText = string.IsNullOrWhiteSpace(WarningsText)
                    ? string.Join(Environment.NewLine, exportResult.Errors)
                    : WarningsText + Environment.NewLine + string.Join(Environment.NewLine, exportResult.Errors);
            }

            StatusMessage = exportResult.CreatedFiles.Count > 0
                ? $"Збережено {exportResult.CreatedFiles.Count} файл(ів) у «{outputDirectory}». " +
                  $"Імена: номер колійного листа (напр. {Path.GetFileName(exportResult.CreatedFiles[0])})."
                : "Документи не створено.";

            AppLogger.LogInfo(
                $"Exported {exportResult.CreatedFiles.Count} waybill documents with {exportResult.Errors.Count} errors.");
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Помилка експорту документів колійних листів", ex);
            StatusMessage = "Сталася помилка під час експорту документів.";
            MessageBox.Show(
                $"Не вдалося зберегти документи:\n{ex.Message}",
                "Помилка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsExporting = false;
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
            StartingSequenceNumber = Math.Max(1, StartingSequenceNumber),
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
