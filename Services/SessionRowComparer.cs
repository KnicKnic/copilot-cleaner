using System.Collections;
using System.ComponentModel;
using CopilotCleaner.Models;

namespace CopilotCleaner.Services;

public sealed class SessionRowComparer(IReadOnlyList<(string Key, ListSortDirection Direction)> sorts) : IComparer
{
    public int Compare(object? x, object? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is not SessionRow left || y is not SessionRow right)
        {
            return 0;
        }

        foreach (var sort in sorts)
        {
            var comparison = CompareValues(left.GetSortValue(sort.Key), right.GetSortValue(sort.Key));
            if (comparison != 0)
            {
                return sort.Direction == ListSortDirection.Ascending ? comparison : -comparison;
            }
        }

        return string.Compare(left.FolderName, right.FolderName, StringComparison.CurrentCultureIgnoreCase);
    }

    private static int CompareValues(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (left.GetType() == right.GetType() && left is IComparable comparable)
        {
            return comparable.CompareTo(right);
        }

        return string.Compare(left.ToString(), right.ToString(), StringComparison.CurrentCultureIgnoreCase);
    }
}