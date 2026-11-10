using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AffiliateDataChat.Services.Chat;

public static partial class ToolOutputFormatter
{
    public static string Format(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            var output = new StringBuilder();
            AppendValue(output, document.RootElement, 0);
            return output.ToString();
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static void AppendValue(StringBuilder output, JsonElement element, int indent)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    AppendIndent(output, indent);
                    output.Append(ToLabel(property.Name)).Append(':');
                    if (IsComplex(property.Value))
                    {
                        output.AppendLine();
                        AppendValue(output, property.Value, indent + 2);
                    }
                    else
                    {
                        output.Append(' ').AppendLine(FormatScalar(property.Value));
                    }
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    index++;
                    AppendIndent(output, indent);
                    output.Append(index).AppendLine(".");
                    AppendValue(output, item, indent + 2);
                }
                if (index == 0)
                {
                    AppendIndent(output, indent);
                    output.AppendLine("No results found.");
                }
                break;

            default:
                AppendIndent(output, indent);
                output.AppendLine(FormatScalar(element));
                break;
        }
    }

    private static bool IsComplex(JsonElement element) =>
        element.ValueKind is JsonValueKind.Object or JsonValueKind.Array;

    private static string FormatScalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.True => "Yes",
        JsonValueKind.False => "No",
        JsonValueKind.Null => "Not provided",
        _ => element.GetRawText()
    };

    private static void AppendIndent(StringBuilder output, int indent) =>
        output.Append(' ', indent);

    private static string ToLabel(string propertyName)
    {
        var label = WordBoundaryRegex().Replace(propertyName.Replace('_', ' '), "$1 $2");
        label = WhitespaceRegex().Replace(label, " ").Trim();
        return Regex.Replace(label, @"\b\w", match => match.Value.ToUpperInvariant())
            .Replace(" Id", " ID", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"([a-z0-9])([A-Z])")]
    private static partial Regex WordBoundaryRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
