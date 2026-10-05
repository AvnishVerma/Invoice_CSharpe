using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

public sealed class RegressionTests
{
    [Fact]
    [Trait("Category", "Regression")]
    public void Desktop_AllExistingHeadlessWorkflows_Pass()
    {
        var output = Path.Combine(Path.GetTempPath(), "ledgernest-regression", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var temporaryVariables = new[] { "TEMP", "TMP", "TMPDIR" };
        var originalVariables = temporaryVariables.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var name in temporaryVariables) Environment.SetEnvironmentVariable(name, output);
            Program.Main([output]);
        }
        finally
        {
            foreach (var name in temporaryVariables) Environment.SetEnvironmentVariable(name, originalVariables[name]);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }
}
