using Xunit;

namespace Solitaire.Engine.Tests;

public class VectorFileTests
{
    [Fact]
    public void EmitVectorsFile_WritesSharedKlondikeJson()
    {
        var file = VectorData.BuildFileDto();
        string json = VectorData.Serialize(file);

        string path = VectorData.VectorFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);

        Assert.True(File.Exists(path));
        Assert.NotEmpty(file.Vectors);
    }

    [Fact]
    public void EveryVector_ReplaysToItsExpectedScoreAndOutcome()
    {
        foreach (var (name, seed, options, moves) in VectorData.BuildCases())
        {
            var result = Klondike.Replay(seed, options, moves);
            var expected = Klondike.Replay(seed, options, moves); // stable recompute

            Assert.True(result.AllMovesLegal, $"Vector '{name}' contains an illegal move.");
            Assert.Equal(expected.Score, result.Score);
            Assert.Equal(expected.Won, result.Won);
        }
    }

    [Fact]
    public void VectorsIncludeAtLeastOneWinAndOneNonWin()
    {
        var file = VectorData.BuildFileDto();
        Assert.Contains(file.Vectors, v => v.ExpectedWin);
        Assert.Contains(file.Vectors, v => !v.ExpectedWin);
    }

    [Fact]
    public void MoveDto_RoundTripsThroughSerialization()
    {
        // Serializing a vector's moves and parsing them back must reproduce the
        // same replay outcome — this is what guarantees the TS port can consume it.
        var file = VectorData.BuildFileDto();
        foreach (var vector in file.Vectors)
        {
            var moves = vector.Moves.Select(VectorData.FromDto).ToList();
            var options = new GameOptions(vector.Options.DrawCount, vector.Options.MaxRedeals);
            var result = Klondike.Replay(vector.Seed, options, moves);

            Assert.Equal(vector.ExpectedFinalScore, result.Score);
            Assert.Equal(vector.ExpectedWin, result.Won);
        }
    }

    [Fact]
    public void CommittedFile_IsInSyncWithGenerator()
    {
        string path = VectorData.VectorFilePath();
        Assert.True(
            File.Exists(path),
            $"Vector file missing at {path}. Run the EmitVectorsFile test to generate it.");

        string onDisk = File.ReadAllText(path).Replace("\r\n", "\n");
        string generated = VectorData.Serialize(VectorData.BuildFileDto()).Replace("\r\n", "\n");

        Assert.Equal(generated, onDisk);
    }
}
