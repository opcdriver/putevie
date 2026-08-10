using System.Windows;
using Putevie.Services;
using Putevie.ViewModels;

namespace Putevie.Views;

public partial class TargetFuelRemainingWindow : Window
{
    public TargetFuelRemainingWindow(TargetFuelRemainingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
    }

    public TargetFuelRemainingViewModel ViewModel => (TargetFuelRemainingViewModel)DataContext;

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        try
        {
            if (ViewModel.DialogResult is bool result)
            {
                // Якщо IsCancel вже закрив діалог — DialogResult встановлювати не можна.
                if (IsVisible)
                {
                    DialogResult = result;
                }
            }
            else if (IsVisible)
            {
                Close();
            }
        }
        catch (InvalidOperationException ex)
        {
            AppLogger.LogError("Діалог уже закрито під час встановлення DialogResult", ex);
            if (IsVisible)
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Помилка закриття вікна цільового залишку палива", ex);
            if (IsVisible)
            {
                Close();
            }
        }
    }
}
