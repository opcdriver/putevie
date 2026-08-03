using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Putevie.Models;

namespace Putevie.Services;

public class WaybillDocumentExportResult
{
    public List<string> CreatedFiles { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

/// <summary>
/// Заповнює DOCX-шаблон даними колійного листа і зберігає файл
/// з іменем = номер колійного листа (держномер-місяць-порядковий).
/// </summary>
public class WaybillDocumentExportService
{
    public const string TemplateFileName = "waybill-template.docx";
    private const string PlaceholderPrefix = "{{";
    private const string PlaceholderSuffix = "}}";

    public string GetDefaultTemplatePath()
    {
        var appDir = AppContext.BaseDirectory;
        return Path.Combine(appDir, "Templates", TemplateFileName);
    }

    public string EnsureTemplateExists(string? templatePath = null)
    {
        try
        {
            var path = string.IsNullOrWhiteSpace(templatePath)
                ? GetDefaultTemplatePath()
                : templatePath;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(path))
            {
                CreateDefaultTemplate(path);
                AppLogger.LogInfo($"Created default waybill template at {path}");
            }

            return path;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Не вдалося підготувати шаблон колійного листа", ex);
            throw;
        }
    }

    public WaybillDocumentExportResult Export(
        IEnumerable<WaybillEntry> entries,
        string outputDirectory,
        string? templatePath = null)
    {
        var result = new WaybillDocumentExportResult();

        try
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                result.Errors.Add("Не вказано теку для збереження файлів.");
                return result;
            }

            Directory.CreateDirectory(outputDirectory);
            var template = EnsureTemplateExists(templatePath);

            foreach (var entry in entries)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(entry.WaybillNumber))
                    {
                        result.Errors.Add(
                            $"Пропущено запис від {entry.Date:dd.MM.yyyy}: відсутній номер колійного листа.");
                        continue;
                    }

                    var fileName = WaybillNumbering.ToFileName(entry.WaybillNumber) + ".docx";
                    var outputPath = Path.Combine(outputDirectory, fileName);
                    CreateDocumentFromTemplate(template, outputPath, entry);
                    result.CreatedFiles.Add(outputPath);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError($"Помилка експорту колійного листа {entry.WaybillNumber}", ex);
                    result.Errors.Add($"{entry.WaybillNumber}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Помилка експорту колійних листів", ex);
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    public void CreateDefaultTemplate(string path)
    {
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());

        var body = mainPart.Document.Body!;
        body.Append(CreateParagraph("КОЛІЙНИЙ ЛИСТ", bold: true, fontSize: "32"));
        body.Append(CreateParagraph($"Номер: {Placeholder("WaybillNumber")}", bold: true, fontSize: "28"));
        body.Append(CreateParagraph(string.Empty));
        body.Append(CreateParagraph($"Дата: {Placeholder("Date")}"));
        body.Append(CreateParagraph($"Автомобіль: {Placeholder("VehicleName")}"));
        body.Append(CreateParagraph($"Державний номер: {Placeholder("LicensePlate")}"));
        body.Append(CreateParagraph($"Працівник: {Placeholder("EmployeeName")}"));
        body.Append(CreateParagraph(string.Empty));
        body.Append(CreateParagraph($"Пробіг, км: {Placeholder("DistanceKm")}"));
        body.Append(CreateParagraph($"Одометр виїзд: {Placeholder("OdometerDeparture")}"));
        body.Append(CreateParagraph($"Одометр повернення: {Placeholder("OdometerReturn")}"));
        body.Append(CreateParagraph($"Заправка, л: {Placeholder("RefuelLiters")}"));
        body.Append(CreateParagraph($"Витрата палива, л: {Placeholder("FuelConsumption")}"));
        body.Append(CreateParagraph($"Паливо при виїзді, л: {Placeholder("FuelDeparture")}"));
        body.Append(CreateParagraph($"Паливо при поверненні, л: {Placeholder("FuelReturn")}"));
        body.Append(CreateParagraph($"Температура виїзд / повернення: {Placeholder("TemperatureDeparture")} / {Placeholder("TemperatureReturn")}"));
        body.Append(CreateParagraph(string.Empty));
        body.Append(CreateParagraph($"Файл: {Placeholder("WaybillNumber")}.docx"));

        mainPart.Document.Save();
    }

    private static void CreateDocumentFromTemplate(string templatePath, string outputPath, WaybillEntry entry)
    {
        File.Copy(templatePath, outputPath, overwrite: true);

        using var document = WordprocessingDocument.Open(outputPath, true);
        var body = document.MainDocumentPart?.Document.Body
            ?? throw new InvalidOperationException("Шаблон не містить тіла документа.");

        var replacements = BuildReplacements(entry);
        ReplacePlaceholders(body, replacements);
        document.MainDocumentPart!.Document.Save();
    }

    private static Dictionary<string, string> BuildReplacements(WaybillEntry entry) => new()
    {
        [Placeholder("WaybillNumber")] = entry.WaybillNumber,
        [Placeholder("Date")] = entry.Date.ToString("dd.MM.yyyy"),
        [Placeholder("VehicleName")] = entry.VehicleName,
        [Placeholder("LicensePlate")] = entry.LicensePlate,
        [Placeholder("EmployeeName")] = entry.EmployeeName,
        [Placeholder("DistanceKm")] = entry.DistanceKm.ToString("F2"),
        [Placeholder("OdometerDeparture")] = entry.OdometerDeparture.ToString("N0"),
        [Placeholder("OdometerReturn")] = entry.OdometerReturn.ToString("N0"),
        [Placeholder("RefuelLiters")] = entry.RefuelLiters.ToString("F2"),
        [Placeholder("FuelConsumption")] = entry.FuelConsumptionActual.ToString("F2"),
        [Placeholder("FuelDeparture")] = entry.FuelDeparture.ToString("F2"),
        [Placeholder("FuelReturn")] = entry.FuelReturn.ToString("F2"),
        [Placeholder("TemperatureDeparture")] = entry.TemperatureDeparture.ToString(),
        [Placeholder("TemperatureReturn")] = entry.TemperatureReturn.ToString(),
        [Placeholder("SequenceNumber")] = entry.SequenceNumber.ToString("000"),
        [Placeholder("Month")] = entry.Date.Month.ToString("00")
    };

    private static void ReplacePlaceholders(OpenXmlElement root, IReadOnlyDictionary<string, string> replacements)
    {
        // Word може розбивати плейсхолдери на кілька Run/Text — спочатку зливаємо текст у параграфах.
        foreach (var paragraph in root.Descendants<Paragraph>())
        {
            var texts = paragraph.Descendants<Text>().ToList();
            if (texts.Count == 0)
            {
                continue;
            }

            var combined = string.Concat(texts.Select(t => t.Text));
            var updated = combined;
            foreach (var (placeholder, value) in replacements)
            {
                if (updated.Contains(placeholder, StringComparison.Ordinal))
                {
                    updated = updated.Replace(placeholder, value, StringComparison.Ordinal);
                }
            }

            if (updated == combined)
            {
                continue;
            }

            texts[0].Text = updated;
            for (var i = 1; i < texts.Count; i++)
            {
                texts[i].Text = string.Empty;
            }
        }
    }

    private static string Placeholder(string name) => $"{PlaceholderPrefix}{name}{PlaceholderSuffix}";

    private static Paragraph CreateParagraph(string text, bool bold = false, string fontSize = "22")
    {
        var runProperties = new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
            new FontSize { Val = fontSize });

        if (bold)
        {
            runProperties.Append(new Bold());
        }

        var run = new Run(runProperties, new Text(text));
        return new Paragraph(run);
    }
}
