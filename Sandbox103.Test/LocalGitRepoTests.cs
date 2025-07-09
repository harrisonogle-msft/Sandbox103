using Sandbox103.Repos;
using Xunit.Abstractions;

namespace Sandbox103.Test;

public class LocalGitRepoTests
{
    private readonly ITestOutputHelper _output;

    public LocalGitRepoTests(ITestOutputHelper output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;
    }

    [Fact]
    public void EnumerateProjectFiles_Test()
    {
        _output.WriteLine($"Using git repository '{Constants.Repo.FullName}'.");

        var repo = new LocalGitRepo(Constants.Repo.FullName);

        _output.WriteLine("Getting project files.");
        foreach (string projectFile in repo.EnumerateProjectFiles())
        {
            _output.WriteLine($"  {projectFile}");
        }
    }

    [Fact]
    public void EnumerateProjectFiles_RelativePaths_Test()
    {
        _output.WriteLine($"Using git repository '{Constants.Repo.FullName}'.");

        var repo = new LocalGitRepo(Constants.Repo.FullName);

        _output.WriteLine("Getting project files.");
        foreach (string projectFile in repo.EnumerateProjectFiles(relativePaths: true))
        {
            _output.WriteLine($"  {projectFile}");
        }
    }
}
