using CobolToMySqlStudio.Application.Services;
using CobolToMySqlStudio.Domain.Models;
using FluentAssertions;
using Xunit;

namespace CobolToMySqlStudio.Tests;

public class ParserAndMappingTests
{
    [Fact]
    public void Parse_SimpleCopybook_ComputesTree()
    {
        var text = """
      01 CUSTOMER-RECORD.
         05 ID PIC 9(3).
         05 NAME PIC X(5).
""";
        var parser = new CopybookParser();
        var res = parser.Parse(text);
        res.Root.Children.Should().HaveCount(1);
        res.Root.Children[0].Children.Should().HaveCount(2);
    }

    [Fact]
    public void Layout_Estimates_Offsets()
    {
        var root = new CopybookNode { Name = "ROOT", Level = 0, IsGroup = true };
        var rec = new CopybookNode { Name = "REC", Level = 1, IsGroup = true, Parent = root };
        root.Children.Add(rec);
        rec.Children.Add(new CopybookNode { Name = "A", Level = 5, Picture = "9(3)" });
        rec.Children.Add(new CopybookNode { Name = "B", Level = 5, Picture = "X(2)" });

        var layout = new LayoutCalculator();
        layout.ComputeOffsets(root);
        rec.Children[0].Offset.Should().Be(0);
        rec.Children[1].Offset.Should().BeGreaterThan(0);
        layout.GetTotalLength(root).Should().BeGreaterThan(0);
    }

    [Fact]
    public void SqlGenerator_MapsTypes()
    {
        var root = new CopybookNode { Name = "ROOT", Level = 0, IsGroup = true };
        var rec = new CopybookNode { Name = "REC", Level = 1, IsGroup = true, Parent = root };
        root.Children.Add(rec);
        rec.Children.Add(new CopybookNode { Name = "CUST-ID", Level = 5, Picture = "9(9)" });
        rec.Children.Add(new CopybookNode { Name = "CUST-NAME", Level = 5, Picture = "X(20)" });

        var gen = new SqlGenerator();
        var ddl = gen.GenerateStagingTableDdl("staging", root);
        ddl.Should().Contain("CREATE TABLE IF NOT EXISTS");
        ddl.Should().Contain("CUST_ID");
    }

    [Fact]
    public void Transform_Generates_View_SQL()
    {
        var eng = new TransformEngine();
        var sql = eng.GenerateSql("stg", "view_curated", "MOVE A -> B\nCOMPUTE C = A + 1");
        sql.Should().Contain("CREATE OR REPLACE VIEW");
        sql.Should().Contain("AS `B`");
    }
}
