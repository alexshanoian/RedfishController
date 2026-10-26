
using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RedfishController.Views
{
    public static class ResourceViewRenderer
    {
        public static void AddCard(
            Panel container,
            string label,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var border = new Border
            {
                Background = GetCardBackground(label, value),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 10, 10),
                MinWidth = 115,
                MaxWidth = 220
            };

            var content = new StackPanel();

            content.Children.Add(new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DimGray
            });

            content.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetCardForeground(label, value),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0)
            });

            border.Child = content;
            container.Children.Add(border);
        }

        public static TreeViewItem BuildPropertyNode(
            string name,
            JsonElement value)
        {
            string displayValue = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.Null => "null",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Object => "{ ... }",
                JsonValueKind.Array => "[ ... ]",
                _ => value.ToString()
            };

            var node = new TreeViewItem
            {
                Header = $"{name}: {displayValue}"
            };

            if (value.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    node.Items.Add(
                        BuildPropertyNode(property.Name, property.Value));
                }

                node.IsExpanded =
                    name == "Status" ||
                    name == "ProcessorSummary" ||
                    name == "MemorySummary";
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                int index = 0;

                foreach (JsonElement item in value.EnumerateArray())
                {
                    node.Items.Add(
                        BuildPropertyNode($"[{index}]", item));
                    index++;
                }
            }

            return node;
        }

        private static Brush GetCardBackground(
            string label,
            string value)
        {
            if (label.Contains(
                "Health", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Equals("OK", StringComparison.OrdinalIgnoreCase))
                    return Brushes.Honeydew;

                if (value.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                    return Brushes.LemonChiffon;

                if (value.Equals("Critical", StringComparison.OrdinalIgnoreCase))
                    return Brushes.MistyRose;
            }

            return Brushes.WhiteSmoke;
        }

        private static Brush GetCardForeground(
            string label,
            string value)
        {
            if (label.Contains(
                "Health", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Equals("OK", StringComparison.OrdinalIgnoreCase))
                    return Brushes.DarkGreen;

                if (value.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                    return Brushes.DarkGoldenrod;

                if (value.Equals("Critical", StringComparison.OrdinalIgnoreCase))
                    return Brushes.DarkRed;
            }

            return Brushes.DarkSlateGray;
        }
    }
}
