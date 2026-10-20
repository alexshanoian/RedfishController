
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RedfishController
{
    public partial class MainWindow : Window
    {
        private HttpClient? _client;
        private Uri? _baseUri;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Connect_Click(
            object sender,
            RoutedEventArgs e)
        {
            string endpoint = EndpointBox.Text.Trim().TrimEnd('/');

            // Validate the endpoint.
            if (!Uri.TryCreate(
                    endpoint, UriKind.Absolute, out Uri? baseUri)
                || baseUri.Scheme != Uri.UriSchemeHttps
                || (baseUri.Host != "localhost"
                    && baseUri.Host != "127.0.0.1")
                || !string.IsNullOrEmpty(baseUri.UserInfo)
                || baseUri.AbsolutePath != "/"
                || !string.IsNullOrEmpty(baseUri.Query)
                || !string.IsNullOrEmpty(baseUri.Fragment))
            {
                MessageBox.Show(
                    "Enter a local HTTPS endpoint, such as https://localhost");
                return;
            }

            try
            {
                DetailTitle.Text = "Connecting...";
                DetailSubtitle.Text = endpoint;
                RawJsonBox.Text = "Discovering Redfish resources...";

                // Read credentials from environment variables.
                string username =
                    Environment.GetEnvironmentVariable("REDFISH_USERNAME")
                    ?? throw new InvalidOperationException(
                        "REDFISH_USERNAME is not set.");

                string password =
                    Environment.GetEnvironmentVariable("REDFISH_PASSWORD")
                    ?? throw new InvalidOperationException(
                        "REDFISH_PASSWORD is not set.");

                // Local emulator only: temporarily accept its certificate.
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        (_, _, _, _) => true
                };

                using var newClient = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(15)
                };

        string credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{username}:{password}"));

        newClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        Uri serviceRoot = new Uri(baseUri, "/redfish/v1/");

        // Verify the connection before replacing the active client.
        using HttpResponseMessage response =
            await newClient.GetAsync(serviceRoot);

        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();

        // Validate the JSON before displaying it.
        using (JsonDocument document = JsonDocument.Parse(json))
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Expected a Redfish JSON object.");
        }

        // Connection succeeded.
        _client?.Dispose();
        _client = new HttpClient(handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                (_, _, _, _) => true
        })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        _baseUri = baseUri;

        ResourceTree.Items.Clear();

        var rootNode = new TreeViewItem
        {
            Header = "Redfish Service Root",
            Tag = serviceRoot.AbsoluteUri,
            IsExpanded = true
        };

        ResourceTree.Items.Add(rootNode);

        PopulateChildren(rootNode, json);

        ShowJson(
            "Redfish Service Root",
            serviceRoot.AbsoluteUri,
            json);
    }
    catch (Exception ex)
    {
        DetailTitle.Text = "Connection failed";
        DetailSubtitle.Text = endpoint;
        RawJsonBox.Text = ex.Message;
    }
}


        private async Task<string> GetResourceJsonAsync(Uri uri)
        {
            if (_client == null)
                throw new InvalidOperationException(
                    "Connect to the Redfish service first.");

            using HttpResponseMessage response =
                await _client.GetAsync(uri);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        private async void ResourceTree_SelectedItemChanged(
            object sender,
            RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is not TreeViewItem item
                || item.Tag is not string url
                || _client == null)
            {
                return;
            }

            try
            {
                Uri uri = new Uri(url);
                DetailTitle.Text = item.Header?.ToString() ?? "Resource";
                DetailSubtitle.Text = uri.AbsoluteUri;
                RawJsonBox.Text = "Loading resource...";

                string json = await GetResourceJsonAsync(uri);

                // Rebuild this node's children from the latest response.
                item.Items.Clear();
                PopulateChildren(item, json);

                ShowJson(
                    item.Header?.ToString() ?? "Resource",
                    uri.AbsoluteUri,
                    json);
            }
            catch (Exception ex)
            {
                DetailTitle.Text = "Resource request failed";
                DetailSubtitle.Text = url;
                RawJsonBox.Text = ex.Message;
            }
        }

        private void PopulateChildren(
            TreeViewItem parent,
            string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            foreach (JsonProperty property in root.EnumerateObject())
            {
                // Collection resources contain a Members array.
                if (property.Name == "Members"
                    && property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement member in
                             property.Value.EnumerateArray())
                    {
                        if (TryGetOdataId(member, out string memberPath))
                        {
                            AddResourceNode(
                                parent,
                                GetFriendlyName(memberPath),
                                memberPath);
                        }
                    }

                    continue;
                }

                // Discover linked resources such as Systems and Chassis.
                if (property.Value.ValueKind == JsonValueKind.Object
                    && TryGetOdataId(
                        property.Value, out string resourcePath))
                {
                    AddResourceNode(
                        parent, property.Name, resourcePath);
                }
            }
        }

        private void AddResourceNode(
            TreeViewItem parent,
            string name,
            string resourcePath)
        {
            if (_baseUri == null)
                return;

            Uri resourceUri = new Uri(_baseUri, resourcePath);

            var node = new TreeViewItem
            {
                Header = name,
                Tag = resourceUri.AbsoluteUri
            };

            // A placeholder makes the node expandable before its
            // children have been retrieved from the API.
            node.Items.Add(new TreeViewItem
            {
                Header = "Load resource...",
                IsEnabled = false
            });

            parent.Items.Add(node);
        }

        private static bool TryGetOdataId(
            JsonElement element,
            out string path)
        {
            path = "";

            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(
                    "@odata.id", out JsonElement id)
                && id.ValueKind == JsonValueKind.String)
            {
                path = id.GetString() ?? "";
                return !string.IsNullOrWhiteSpace(path);
            }

            return false;
        }

        private static string GetFriendlyName(string resourcePath)
        {
            string trimmed = resourcePath.TrimEnd('/');
            int lastSlash = trimmed.LastIndexOf('/');

            return lastSlash >= 0
                ? trimmed[(lastSlash + 1)..]
                : trimmed;
        }

        
private void ShowJson(
    string title,
    string url,
    string json)
{
    using JsonDocument document = JsonDocument.Parse(json);
    JsonElement root = document.RootElement;

    string version = GetString(root, "RedfishVersion");

    DetailTitle.Text = title;
    DetailSubtitle.Text = !string.IsNullOrEmpty(version)
        ? $"Redfish {version} | {url}"
        : url;

    // Clear the previous resource's information.
    SummaryCards.Children.Clear();
    PropertyTree.Items.Clear();

    // Add useful summary cards when these fields exist.
    AddCard("Health", GetNestedString(root, "Status", "Health"));
    AddCard("Health Rollup",
        GetNestedString(root, "Status", "HealthRollup"));
    AddCard("State", GetNestedString(root, "Status", "State"));
    AddCard("Power", GetString(root, "PowerState"));
    AddCard("Model", GetString(root, "Model"));
    AddCard("Manufacturer", GetString(root, "Manufacturer"));

    // Build an expandable view of every JSON property.
    foreach (JsonProperty property in root.EnumerateObject())
    {
        PropertyTree.Items.Add(
            BuildPropertyNode(property.Name, property.Value));
    }

    // Preserve the complete response for troubleshooting.
    RawJsonBox.Text = JsonSerializer.Serialize(
        root,
        new JsonSerializerOptions
        {
            WriteIndented = true
        });
}

private void AddCard(string label, string value)
{
    // Avoid empty cards for resources that don't expose these fields.
    if (string.IsNullOrWhiteSpace(value))
        return;

    var border = new System.Windows.Controls.Border
    {
        Background = GetCardBackground(label, value),
        CornerRadius = new System.Windows.CornerRadius(8),
        Padding = new System.Windows.Thickness(12),
        Margin = new System.Windows.Thickness(0, 0, 10, 10),
        MinWidth = 115,
        MaxWidth = 220
    };

    var content = new System.Windows.Controls.StackPanel();

    content.Children.Add(new System.Windows.Controls.TextBlock
    {
        Text = label.ToUpperInvariant(),
        FontSize = 10,
        FontWeight = FontWeights.Bold,
        Foreground = System.Windows.Media.Brushes.DimGray
    });

    content.Children.Add(new System.Windows.Controls.TextBlock
    {
        Text = value,
        FontSize = 15,
        FontWeight = FontWeights.SemiBold,
        Foreground = GetCardForeground(label, value),
        TextWrapping = TextWrapping.Wrap,
        Margin = new System.Windows.Thickness(0, 5, 0, 0)
    });

    border.Child = content;
    SummaryCards.Children.Add(border);
}

private static System.Windows.Media.Brush GetCardBackground(
    string label, string value)
{
    if (label.Contains("Health", StringComparison.OrdinalIgnoreCase))
    {
        if (value.Equals("OK", StringComparison.OrdinalIgnoreCase))
            return System.Windows.Media.Brushes.Honeydew;

        if (value.Equals("Warning", StringComparison.OrdinalIgnoreCase))
            return System.Windows.Media.Brushes.LemonChiffon;

        if (value.Equals("Critical", StringComparison.OrdinalIgnoreCase))
            return System.Windows.Media.Brushes.MistyRose;
    }

    return System.Windows.Media.Brushes.WhiteSmoke;
}

private static System.Windows.Media.Brush GetCardForeground(
    string label, string value)
{
    if (label.Contains("Health", StringComparison.OrdinalIgnoreCase))
    {
        if (value.Equals("OK", StringComparison.OrdinalIgnoreCase))
            return System.Windows.Media.Brushes.DarkGreen;

        if (value.Equals("Warning", StringComparison.OrdinalIgnoreCase))
            return System.Windows.Media.Brushes.DarkGoldenrod;

        if (value.Equals("Critical", StringComparison.OrdinalIgnoreCase))
            return System.Windows.Media.Brushes.DarkRed;
    }

    return System.Windows.Media.Brushes.DarkSlateGray;
}

private static string GetNestedString(
    JsonElement element,
    string parentName,
    string propertyName)
{
    if (element.TryGetProperty(parentName, out JsonElement parent)
        && parent.ValueKind == JsonValueKind.Object)
    {
        return GetString(parent, propertyName);
    }

    return "";
}

private TreeViewItem BuildPropertyNode(
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

        node.IsExpanded = name == "Status"
            || name == "ProcessorSummary"
            || name == "MemorySummary";
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


        private static string GetString(
            JsonElement element,
            string propertyName)
        {
            if (element.TryGetProperty(
                    propertyName, out JsonElement value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? "";
            }

            return "";
        }

        private void Refresh_Click(
            object sender,
            RoutedEventArgs e)
        {
            Connect_Click(sender, e);
        }
    }
}
