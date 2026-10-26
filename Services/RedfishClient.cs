
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RedfishController.Services
{
    public sealed class RedfishClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        public Uri BaseUri { get; }

        public RedfishClient(
            Uri baseUri,
            string username,
            string password)
        {
            if (baseUri.Scheme != Uri.UriSchemeHttps ||
                (baseUri.Host != "localhost" &&
                 baseUri.Host != "127.0.0.1") ||
                !string.IsNullOrEmpty(baseUri.UserInfo) ||
                baseUri.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(baseUri.Query) ||
                !string.IsNullOrEmpty(baseUri.Fragment))
            {
                throw new ArgumentException(
                    "Only local HTTPS emulator endpoints are supported.");
            }

            BaseUri = baseUri;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    (_, _, _, _) => true,
                AllowAutoRedirect = false
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            string credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    $"{username}:{password}"));

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Basic", credentials);
        }

        // GET: Retrieve a Redfish resource.
        public async Task<string> GetResourceAsync(
            string resourcePath)
        {
            Uri resourceUri = ValidateResourceUri(resourcePath);

            using HttpResponseMessage response =
                await _httpClient.GetAsync(resourceUri);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        // POST: Send an action and receive its HTTP response.
        public async Task<(HttpStatusCode StatusCode, string Body)>
            PostActionAsync(string actionPath, object payload)
        {
            Uri actionUri = ValidateResourceUri(actionPath);

            string json = JsonSerializer.Serialize(payload);

            using var content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            using HttpResponseMessage response =
                await _httpClient.PostAsync(actionUri, content);

            string responseBody =
                await response.Content.ReadAsStringAsync();

            return (response.StatusCode, responseBody);
        }

        // Ensure requests stay within the local emulator.
        private Uri ValidateResourceUri(string resourcePath)
        {
            Uri resourceUri = new Uri(BaseUri, resourcePath);

            if (resourceUri.Scheme != BaseUri.Scheme ||
                resourceUri.Host != BaseUri.Host ||
                resourceUri.Port != BaseUri.Port ||
                !string.IsNullOrEmpty(resourceUri.UserInfo) ||
                !string.IsNullOrEmpty(resourceUri.Query) ||
                !string.IsNullOrEmpty(resourceUri.Fragment) ||
                !resourceUri.AbsolutePath.StartsWith(
                    "/redfish/v1/",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Resource URL must belong to the local Redfish service.");
            }

            return resourceUri;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
