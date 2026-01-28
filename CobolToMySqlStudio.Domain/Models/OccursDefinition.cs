namespace CobolToMySqlStudio.Domain.Models;

public class OccursDefinition
{
    public string FieldName { get; set; } = string.Empty;
    public int Count { get; set; }
    public OccursMode Mode { get; set; } = OccursMode.Flatten;
}
