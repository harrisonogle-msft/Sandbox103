using Microsoft.Build.Framework;
using System.Collections;

namespace Sandbox103.Extensions;

public static class BuildEventArgsExtensions
{
    public static IEnumerable<KeyValuePair<string, ITaskItem>> EnumerateItems(this ProjectEvaluationFinishedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return GetItemsCore(args.Items);
    }

    public static IEnumerable<KeyValuePair<string, ITaskItem>> EnumerateItems(this ProjectStartedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return GetItemsCore(args.Items);
    }

    private static IEnumerable<KeyValuePair<string, ITaskItem>> GetItemsCore(IEnumerable? items)
    {
        if (items is null)
        {
            yield break;
        }

        if (items is not IEnumerable<DictionaryEntry> entries)
        {
            throw new InvalidOperationException($"Expected an enumerable of dictionary entries; observed '{items.GetType()}'.");
        }

        foreach (DictionaryEntry item in items)
        {
            if (item.Key is not string key)
            {
                throw new InvalidOperationException($"Unexpected key type: '{item.Key?.GetType()}'.");
            }

            object? value = item.Value;

            if (value is null)
            {
                throw new InvalidOperationException("Invalid dictionary entry: value is null.");
            }

            if (value is not ITaskItem taskItem)
            {
                throw new InvalidOperationException($"Unexpected dictionary entry value type: '{item.Value?.GetType()}'.");
            }

            yield return new KeyValuePair<string, ITaskItem>(key, taskItem);
        }
    }
}
