using Microsoft.Build.Logging;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Options;
using Sandbox103.Helpers;
using Sandbox103.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sandbox103.V1.LogDrops;

public class LogDropV1
{
    private readonly DirectoryInfo _root;
    private readonly DirectoryInfoWrapper _wrapper;

    public LogDropV1(IOptions<SdkStyleConversionOptions> sdkStyleConversionOptions) : this(sdkStyleConversionOptions.Value.LogDropPath)
    {
    }

    public LogDropV1(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        _root = new DirectoryInfo(path);

        if (!_root.Exists)
        {
            throw new DirectoryNotFoundException(path);
        }

        _wrapper = new DirectoryInfoWrapper(_root);
    }

    public string Path => _root.FullName;

    public IEnumerable<string> Glob(string pattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        return Glob(new Matcher().AddInclude(pattern));
    }

    public IEnumerable<string> Glob(Matcher glob)
    {
        PatternMatchingResult searchResult = glob.Execute(_wrapper);

        if (!searchResult.HasMatches)
        {
            return Array.Empty<string>();
        }

        return searchResult.Files.Select(item => PathHelper.NormalizePath(System.IO.Path.Join(_root.FullName, item.Path)));
    }
}
