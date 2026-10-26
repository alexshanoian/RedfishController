
using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace RedfishController.Views
{
    public partial class ApiResponseWindow : Window
    {
        public ApiResponseWindow(
            string method,
            string endpoint,
            HttpStatusCode statusCode,
            string responseBody)
        {
            InitializeComponent();

            int status = (int)statusCode;

            StatusText.Text = $"HTTP {status} — {statusCode}";

            StatusText.Foreground =
                status >= 200 && status < 300
                    ? Brushes.ForestGreen
                    : Brushes.Firebrick;

            RequestText.Text = $"{method}  {endpoint}";

            ResponseBox.Text = FormatJson(responseBody);
        }

        private static string FormatJson(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "(No response body)";

            try
            {
                using JsonDocument document =
                    JsonDocument.Parse(body);

                return JsonSerializer.Serialize(
                    document.RootElement,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
            }
            catch (JsonException)
            {
                return body;
            }
        }

        private void Copy_Click(
            object sender,
            RoutedEventArgs e)
        {
            Clipboard.SetText(ResponseBox.Text);
        }

        private void Save_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                FileName = "redfish-response",
                DefaultExt = ".json",
                Filter =
                    "JSON files (*.json)|*.json|" +
                    "Text files (*.txt)|*.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(
                        dialog.FileName,
                        ResponseBox.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        ex.Message,
                        "Save Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }
    }
}
