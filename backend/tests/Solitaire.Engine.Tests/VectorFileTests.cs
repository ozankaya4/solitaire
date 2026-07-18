using Xunit;

namespace Solitaire.Engine.Tests;

public class VectorFileTests
{
    public static TheoryData<string> Files
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (string name in VectorData.FileNames)
            {
                data.Add(name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EmitVectorsFile_WritesSharedJson(string fileName)
    {
        var file = VectorData.BuildFile(fileName);
        string json = VectorData.Serialize(file);

        string path = VectorData.VectorFilePath(fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);

        Assert.True(File.Exists(path));
        Assert.NotEmpty(file.Vectors);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryVector_ReplaysToExpected_ThroughUniformEngine(string fileName)
    {
        // Deserialize the committed file and verify each vector through the common
        // ISolitaireEngine interface (the exact path the API uses).
        string path = VectorData.VectorFilePath(fileName);
        Assert.True(File.Exists(path), $"Missing {path}; run EmitVectorsFile first.");

        var file = VectorData.Deserialize(File.ReadAllText(path));
        Assert.NotEmpty(file.Vectors);

        foreach (var vector in file.Vectors)
        {
            var engine = SolitaireEngines.For(vector.Variant);
            var outcome = engine.Replay(new GameDefinition(vector.Seed, vector.Options, vector.Moves));

            Assert.True(outcome.AllMovesLegal, $"Vector '{vector.Name}' contains an illegal move.");
            Assert.Equal(vector.ExpectedFinalScore, outcome.Score);
            Assert.Equal(vector.ExpectedWin, outcome.Won);
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void CommittedFile_IsInSyncWithGenerator(string fileName)
    {
        string path = VectorData.VectorFilePath(fileName);
        Assert.True(File.Exists(path), $"Vector file missing at {path}. Run EmitVectorsFile to generate it.");

        string onDisk = File.ReadAllText(path).Replace("\r\n", "\n");
        string generated = VectorData.Serialize(VectorData.BuildFile(fileName)).Replace("\r\n", "\n");

        Assert.Equal(generated, onDisk);
    }

    [Fact]
    public void KlondikeVectors_IncludeAWinAndANonWin()
    {
        var file = VectorData.BuildFile("klondike.json");
        Assert.Contains(file.Vectors, v => v.ExpectedWin);
        Assert.Contains(file.Vectors, v => !v.ExpectedWin);
    }

    [Fact]
    public void SpiderVectors_CoverAllThreeDifficulties()
    {
        var file = VectorData.BuildFile("spider.json");
        Assert.Contains(file.Vectors, v => v.Options["suitCount"] == 1);
        Assert.Contains(file.Vectors, v => v.Options["suitCount"] == 2);
        Assert.Contains(file.Vectors, v => v.Options["suitCount"] == 4);
    }

    [Fact]
    public void FreeCellVectors_AreNonEmpty()
    {
        // Unlike Klondike, a guaranteed win is not required here — see
        // FreeCellSolver's remarks (mirrors Spider's equivalent test).
        var file = VectorData.BuildFile("freecell.json");
        Assert.NotEmpty(file.Vectors);
        Assert.Contains(file.Vectors, v => v.Moves.Count > 0);
    }
}
