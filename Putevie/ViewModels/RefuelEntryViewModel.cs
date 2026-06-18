namespace Putevie.ViewModels;

public class RefuelEntryViewModel : ViewModelBase
{
    private DateTime _date = DateTime.Today;
    private double _liters = 20;

    public Guid Id { get; } = Guid.NewGuid();

    public DateTime Date
    {
        get => _date;
        set => SetProperty(ref _date, value);
    }

    public double Liters
    {
        get => _liters;
        set => SetProperty(ref _liters, value);
    }
}
