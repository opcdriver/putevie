using System.Windows.Input;
using Putevie.Services;

namespace Putevie.ViewModels;

public class TargetFuelRemainingViewModel : ViewModelBase
{
    private double _targetFuelRemaining = 10.00;
    private string _validationMessage = string.Empty;

    public TargetFuelRemainingViewModel(double suggestedRemaining)
    {
        _targetFuelRemaining = suggestedRemaining >= 0
            ? FuelMath.RoundLiters(suggestedRemaining)
            : 10.00;

        ConfirmCommand = new RelayCommand(Confirm, CanConfirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    public double TargetFuelRemaining
    {
        get => _targetFuelRemaining;
        set
        {
            if (SetProperty(ref _targetFuelRemaining, value))
            {
                ValidationMessage = value < 0
                    ? "Залишок не може бути від'ємним."
                    : string.Empty;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public bool? DialogResult { get; private set; }

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public event EventHandler? CloseRequested;

    private bool CanConfirm() => TargetFuelRemaining >= 0 && string.IsNullOrEmpty(ValidationMessage);

    private void Confirm()
    {
        try
        {
            if (!CanConfirm())
            {
                return;
            }

            TargetFuelRemaining = FuelMath.RoundLiters(TargetFuelRemaining);
            DialogResult = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Помилка підтвердження цільового залишку палива", ex);
            ValidationMessage = "Не вдалося підтвердити значення.";
        }
    }

    private void Cancel()
    {
        DialogResult = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
