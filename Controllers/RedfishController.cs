
using System;
using System.Text.Json;
using System.Threading.Tasks;
using RedfishController.Services;
using System.Net;

namespace RedfishController.Controllers
{
    public sealed class RedfishController : IDisposable
    {
        private RedfishClient? _client;

        public bool IsConnected => _client != null;

        public Uri? BaseUri => _client?.BaseUri;

        public async Task<string> ConnectAsync(string endpoint)
        {
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
                throw new ArgumentException(
                    "Enter a local HTTPS endpoint, such as https://localhost");
            }

            string username =
                Environment.GetEnvironmentVariable("REDFISH_USERNAME")
                ?? throw new InvalidOperationException(
                    "REDFISH_USERNAME is not set.");

            string password =
                Environment.GetEnvironmentVariable("REDFISH_PASSWORD")
                ?? throw new InvalidOperationException(
                    "REDFISH_PASSWORD is not set.");

            // Create a candidate client. Keep the existing
            // connection intact if the new connection fails.
            var candidate = new RedfishClient(
                baseUri,
                username,
                password);

            try
            {
                string json = await candidate.GetResourceAsync(
                    "/redfish/v1/");

                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException(
                        "Expected a Redfish JSON object.");
                }

                // Connection verified: transfer ownership.
                RedfishClient? previous = _client;
                _client = candidate;
                previous?.Dispose();

                return json;
            }
            catch
            {
                candidate.Dispose();
                throw;
            }
        }

        public async Task<string> GetResourceAsync(string resourceUrl)
        {
            if (_client == null)
            {
                throw new InvalidOperationException(
                    "Connect to the Redfish service first.");
            }

            return await _client.GetResourceAsync(resourceUrl);
        }

        public Uri GetResourceUri(string resourcePath)
        {
            if (_client == null)
            {
                throw new InvalidOperationException(
                    "Connect to the Redfish service first.");
            }

            return new Uri(_client.BaseUri, resourcePath);
        }
       
         public async Task<(HttpStatusCode StatusCode, string Body)> ResetSystemAsync(
            string systemPath,
            string resetType)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to Redfish.");

            if (systemPath != "/redfish/v1/Systems/1")
                throw new InvalidOperationException(
                    "Power actions are currently restricted to the local test system.");

            string[] allowedResetTypes =
            {
                "On",
                "ForceOff",
                "GracefulShutdown",
                "ForceRestart",
                "Nmi",
                "PushPowerButton",
                "GracefulRestart"
            };

            if (!allowedResetTypes.Contains(resetType))
                throw new ArgumentException("Unsupported reset type.");

            string actionPath =
                "/redfish/v1/Systems/1/Actions/ComputerSystem.Reset";

            var payload = new
            {
                ResetType = resetType
            };

            return await _client.PostActionAsync(actionPath, payload);
        }


        public void Dispose()
        {
            _client?.Dispose();
            _client = null;
        }
    }
}
