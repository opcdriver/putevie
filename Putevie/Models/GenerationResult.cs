namespace Putevie.Models;

public class GenerationResult
{
    public List<WaybillEntry> Entries { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}
