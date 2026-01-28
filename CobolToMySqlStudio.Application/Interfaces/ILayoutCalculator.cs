using CobolToMySqlStudio.Domain.Models;

namespace CobolToMySqlStudio.Application.Interfaces;

public interface ILayoutCalculator
{
    void ComputeOffsets(CopybookNode root);
    int GetTotalLength(CopybookNode root);
}
