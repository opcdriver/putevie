using System.Globalization;
using System.Windows.Data;
using Putevie.Models;

namespace Putevie.Converters;

public class EngineTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            EngineType.Petrol => "Бензин",
            EngineType.Diesel => "Дизель",
            EngineType.Gas => "Газ",
            _ => value?.ToString() ?? string.Empty
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            "Бензин" => EngineType.Petrol,
            "Дизель" => EngineType.Diesel,
            "Газ" => EngineType.Gas,
            _ => EngineType.Petrol
        };
}
