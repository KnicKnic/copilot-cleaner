using System.Collections;
using System.ComponentModel;
using CopilotCleaner.Models;

namespace CopilotCleaner.Services;

public sealed class SessionRowComparer(IReadOnlyList<(string Key, ListSortDirection Direction)> sorts) : IComparer, IComparer<SessionRow>
{
    public int Compare(SessionRow? left, SessionRow? right)
    {
        if (ReferenceEquals(left, right))
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

    public int Compare(object? x, object? y)
    {
        return Compare(x as SessionRow, y as SessionRow);
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
