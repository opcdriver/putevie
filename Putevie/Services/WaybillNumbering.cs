using System.IO;

namespace Putevie.Services;

/// <summary>
/// Формат номера колійного листа: {держномер}-{місяць MM}-{порядковий NNN}.
/// Приклад: AA1234BB-07-001
/// </summary>
public static class WaybillNumbering
{
    public static string NormalizePlate(string licensePlate)
    {
        if (string.IsNullOrWhiteSpace(licensePlate))
        {
            return "UNKNOWN";
        }

        var cleaned = new string(licensePlate
            .Trim()
            .Where(ch => !char.IsWhiteSpace(ch))
            .ToArray())
            .ToUpperInvariant();

        return string.IsNullOrEmpty(cleaned) ? "UNKNOWN" : cleaned;
    }

    public static string Format(string licensePlate, DateTime waybillDate, int sequenceNumber)
    {
        var plate = NormalizePlate(licensePlate);
        var month = waybillDate.Month.ToString("00");
        var sequence = Math.Max(1, sequenceNumber).ToString("000");
        return $"{plate}-{month}-{sequence}";
    }

    /// <summary>
    /// Ім'я файлу без розширення — той самий номер колійного листа.
    /// </summary>
    public static string ToFileName(string waybillNumber)
    {
        if (string.IsNullOrWhiteSpace(waybillNumber))
        {
            return "waybill";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(waybillNumber
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray());

        return string.IsNullOrWhiteSpace(safe) ? "waybill" : safe;
    }
}
