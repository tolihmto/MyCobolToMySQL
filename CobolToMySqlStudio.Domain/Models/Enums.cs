namespace CobolToMySqlStudio.Domain.Models;

public enum UsageType
{
    Display,
    Comp,
    Comp3,
    Binary
}

public enum OccursMode
{
    Flatten,
    Normalize
}

public enum RedefinesMode
{
    StoreAll,
    DiscriminatorRule
}
