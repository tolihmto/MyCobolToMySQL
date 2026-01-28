using System.Collections.Generic;

namespace CobolToMySqlStudio.Domain.Models;

public class RedefinesGroup
{
    public string BaseField { get; set; } = string.Empty;
    public List<string> Variants { get; set; } = new();
    public RedefinesMode Mode { get; set; } = RedefinesMode.StoreAll;
    public string? DiscriminatorRule { get; set; }
}
