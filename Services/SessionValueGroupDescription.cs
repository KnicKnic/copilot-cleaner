using System.Globalization;
using System.ComponentModel;
using CopilotCleaner.Models;

namespace CopilotCleaner.Services;

public sealed class SessionValueGroupDescription(string key) : GroupDescription
{
    public override object GroupNameFromItem(object item, int level, CultureInfo culture)
    {
        if (item is not SessionRow row)
        {
            return string.Empty;
        }

        var value = row[key];
        return string.IsNullOrWhiteSpace(value) ? "(blank)" : value;
    }
}