using System.Text.Json.Nodes;
using BlogSpecLab.Infra.AI.ContentReviews;

namespace BlogSpecLab.Tests.Infra.AI;

/// <summary>The contracts file is the source of truth for the wire schema (contracts/application-ai.md).</summary>
public class ContentReviewSchemaContractTests
{
    [Fact]
    public void EmbeddedSchema_EqualsContractFile()
    {
        var contract = JsonNode.Parse(File.ReadAllText(FindContractFile()));
        var embedded = JsonNode.Parse(ContentReviewSchema.Json.ToString());

        Assert.True(JsonNode.DeepEquals(contract, embedded), "Embedded schema differs from contracts/model-output.schema.json.");
    }

    [Fact]
    public void SchemaName_MatchesContractTitle()
    {
        var contract = JsonNode.Parse(File.ReadAllText(FindContractFile()))!;

        Assert.Equal(ContentReviewSchema.Name, contract["title"]!.GetValue<string>());
    }

    private static string FindContractFile()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName, "specs", "002-ai-content-review", "contracts", "model-output.schema.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("contracts/model-output.schema.json was not found above the test output folder.");
    }
}
