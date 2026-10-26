
using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using RedfishController.Helpers;
using RedfishController.Models;
using System.Collections.Generic;
using RedfishController.Controllers;
using RedfishController.Views;
using System.Net;
using System.Linq;

namespace RedfishController
{
    public partial class MainWindow : Window
    {
        private readonly RedfishController.Controllers.RedfishController
             _controller = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Connect_Click(
            object sender,
            RoutedEventArgs e)
        {
            string endpoint = EndpointBox.Text.Trim().TrimEnd('/');

            try
            {
                DetailTitle.Text = "Connecting...";
                DetailSubtitle.Text = endpoint;
                RawJsonBox.Text = "Discovering Redfish resources...";

                string json = await _controller.ConnectAsync(endpoint);

                Uri serviceRoot =
                    _controller.GetResourceUri("/redfish/v1/");

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


        
        private async void GracefulRestartButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_controller.IsConnected)
            {
                MessageBox.Show("Connect to the emulator first.");
                return;
            }

            if (MessageBox.Show(
                "Restart the simulated HPE server?",
                "Confirm Restart",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            GracefulRestartButton.IsEnabled = false;

            try
            {
                var result = await _controller.ResetSystemAsync(
                    "/redfish/v1/Systems/1",
                    "GracefulRestart");

                var responseWindow = new ApiResponseWindow(
                    "POST",
                    "/redfish/v1/Systems/1/Actions/ComputerSystem.Reset",
                    result.StatusCode,
                    result.Body);

                responseWindow.Owner = this;
                responseWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Restart Failed");
            }
            finally
            {
                GracefulRestartButton.IsEnabled = true;
            }
        }


        private async void ResourceTree_SelectedItemChanged(
            object sender,
            RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is not TreeViewItem item
                || item.Tag is not string url
                || !_controller.IsConnected)
            {
                return;
            }

            try
            {
                DetailTitle.Text =
                    item.Header?.ToString() ?? "Resource";

                DetailSubtitle.Text = url;
                RawJsonBox.Text = "Loading resource...";

                ActionListPanel.Children.Clear();
                ActionStatusText.Text = "Discovering actions...";
                
                string json = await _controller.GetResourceAsync(url);

                // Discover actions advertised by this resource.
                
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    List<RedfishAction> actions =
                        RedfishParser.GetActions(document.RootElement);

                    PopulateActions(actions);
                }


                item.Items.Clear();
                PopulateChildren(item, json);


                ShowJson(
                    item.Header?.ToString() ?? "Resource",
                    url,
                    json);
            }
            catch (Exception ex)
            {
                DetailTitle.Text = "Resource request failed";
                DetailSubtitle.Text = url;
                RawJsonBox.Text = ex.Message;
            }
        }


        private async System.Threading.Tasks.Task ExecuteDiscoveredActionAsync(
            RedfishAction action,
            Button executeButton)
        {
            if (!_controller.IsConnected)
            {
                MessageBox.Show("Connect to the emulator first.");
                return;
            }

            if (action.Name != "ComputerSystem.Reset" ||
                action.Target != "/redfish/v1/Systems/1/Actions/ComputerSystem.Reset" ||
                !action.Parameters.TryGetValue("ResetType", out var allowedTypes) ||
                !allowedTypes.Contains("GracefulRestart"))
            {
                MessageBox.Show(
                    "This action is not yet supported by the Action Center.",
                    "Unsupported Action");
                return;
            }

            var confirmation = MessageBox.Show(
                "Send GracefulRestart to the simulated HPE server?",
                "Confirm Redfish Action",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            executeButton.IsEnabled = false;

            try
            {
                var result = await _controller.ResetSystemAsync(
                    "/redfish/v1/Systems/1",
                    "GracefulRestart");

                var responseWindow = new ApiResponseWindow(
                    "POST",
                    action.Target,
                    result.StatusCode,
                    result.Body);

                responseWindow.Owner = this;
                responseWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Action Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                executeButton.IsEnabled = true;
            }
        }


        private void PopulateActions(List<RedfishAction> actions)
        {
            // Remove actions belonging to the previously selected resource.
            ActionListPanel.Children.Clear();

            if (actions.Count == 0)
            {
                ActionStatusText.Text =
                    "No actions advertised by this resource.";
                return;
            }

            ActionStatusText.Text =
                $"{actions.Count} action(s) available.";

            foreach (RedfishAction action in actions)
            {
                var actionCard = new StackPanel
                {
                    Margin = new Thickness(0, 0, 0, 14)
                };

                actionCard.Children.Add(new TextBlock
                {
                    Text = action.Name,
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = System.Windows.Media.Brushes.DarkSlateBlue
                });

                actionCard.Children.Add(new TextBlock
                {
                    Text = $"POST  {action.Target}",
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 8)
                });

                foreach (var parameter in action.Parameters)
                {
                    actionCard.Children.Add(new TextBlock
                    {
                        Text = parameter.Key,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 4, 0, 4)
                    });

                    var dropdown = new ComboBox
                    {
                        MinWidth = 200,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 0, 0, 8)
                    };

                    foreach (string value in parameter.Value)
                    {
                        dropdown.Items.Add(value);
                    }

                    if (dropdown.Items.Count > 0)
                        dropdown.SelectedIndex = 0;

                    actionCard.Children.Add(dropdown);
                }


                    var executeButton = new Button
                    {
                        Content = "Execute Action",
                        Padding = new Thickness(14, 8, 14, 8),
                        Margin = new Thickness(0, 8, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        IsEnabled = action.Name == "ComputerSystem.Reset" &&
                                    action.Target == "/redfish/v1/Systems/1/Actions/ComputerSystem.Reset" &&
                                    action.Parameters.TryGetValue("ResetType", out var allowedTypes) &&
                                    allowedTypes.Contains("GracefulRestart")
                    };

                    executeButton.Click += async (sender, e) =>
                    {
                        await ExecuteDiscoveredActionAsync(action, executeButton);
                    };

                    actionCard.Children.Add(executeButton);


                ActionListPanel.Children.Add(actionCard);
            }
        }


        private void PopulateChildren(
            TreeViewItem parent,
            string json)
        {
            List<ResourceLink> links =
                RedfishParser.GetResourceLinks(json);

            foreach (ResourceLink link in links)
            {
                AddResourceNode(
                    parent,
                    link.Name,
                    link.Path);
            }
        }

        private void AddResourceNode(
            TreeViewItem parent,
            string name,
            string resourcePath)
        {
            if (!_controller.IsConnected)
                return;

            Uri resourceUri = _controller.GetResourceUri(resourcePath);

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

        private void ShowJson(
            string title,
            string url,
            string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

                string version = RedfishParser.GetString(
                    root, "RedfishVersion");

            DetailTitle.Text = title;
            DetailSubtitle.Text = !string.IsNullOrEmpty(version)
                ? $"Redfish {version} | {url}"
                : url;

            // Clear the previous resource's information.
            SummaryCards.Children.Clear();
            PropertyTree.Items.Clear();

            // Add useful summary cards when these fields exist.
            ResourceViewRenderer.AddCard(
                SummaryCards,
                "Health",
                RedfishParser.GetNestedString(root, "Status", "Health"));

            ResourceViewRenderer.AddCard(
                SummaryCards,
                "Health Rollup",
                RedfishParser.GetNestedString(root, "Status", "HealthRollup"));

            ResourceViewRenderer.AddCard(
                SummaryCards,
                "State",
                RedfishParser.GetNestedString(root, "Status", "State"));

            ResourceViewRenderer.AddCard(
                SummaryCards,
                "Power",
                RedfishParser.GetString(root, "PowerState"));

            ResourceViewRenderer.AddCard(
                SummaryCards,
                "Model",
                RedfishParser.GetString(root, "Model"));

            ResourceViewRenderer.AddCard(
                SummaryCards,
                "Manufacturer",
                RedfishParser.GetString(root, "Manufacturer"));

            // Build an expandable view of every JSON property.
            foreach (JsonProperty property in root.EnumerateObject())
            {
                PropertyTree.Items.Add(
                    ResourceViewRenderer.BuildPropertyNode(
                        property.Name,
                        property.Value));
            }

            // Preserve the complete response for troubleshooting.
            RawJsonBox.Text = JsonSerializer.Serialize(
                root,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }

    
        
private void Refresh_Click(
    object sender,
    RoutedEventArgs e)
{
    Connect_Click(sender, e);
}

    }
}
