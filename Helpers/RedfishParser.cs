
using System;
using System.Collections.Generic;
using System.Text.Json;
using RedfishController.Models;

namespace RedfishController.Helpers
{
    public static class RedfishParser
    {
        public static List<ResourceLink> GetResourceLinks(
            string json)
        {
            var links = new List<ResourceLink>();

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonException(
                    "Expected a Redfish resource object.");

            foreach (JsonProperty property in root.EnumerateObject())
            {
                // Collections expose resource links in Members.
                if (property.Name == "Members" &&
                    property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement member in
                             property.Value.EnumerateArray())
                    {
                        if (TryGetOdataId(member, out string path))
                        {
                            links.Add(new ResourceLink(
                                GetFriendlyName(path),
                                path));
                        }
                    }

                    continue;
                }

                // Discover direct links to related resources.
                if (property.Value.ValueKind == JsonValueKind.Object &&
                    TryGetOdataId(
                        property.Value,
                        out string resourcePath))
                {
                    links.Add(new ResourceLink(
                        property.Name,
                        resourcePath));
                }
            }

            return links;
        }

        public static bool TryGetOdataId(
            JsonElement element,
            out string path)
        {
            path = "";

            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(
                    "@odata.id",
                    out JsonElement id) &&
                id.ValueKind == JsonValueKind.String)
            {
                path = id.GetString() ?? "";

                return !string.IsNullOrWhiteSpace(path);
            }

            return false;
        }

        public static string GetFriendlyName(
            string resourcePath)
        {
            string trimmed = resourcePath.TrimEnd('/');
            int lastSlash = trimmed.LastIndexOf('/');

            return lastSlash >= 0
                ? trimmed[(lastSlash + 1)..]
                : trimmed;
        }

        public static string GetString(
            JsonElement element,
            string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(
                    propertyName,
                    out JsonElement value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? "";
            }

            return "";
        }

        public static string GetNestedString(
            JsonElement element,
            string parentName,
            string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(
                    parentName,
                    out JsonElement parent) &&
                parent.ValueKind == JsonValueKind.Object)
            {
                return GetString(parent, propertyName);
            }

            return "";
        }

        
        public static List<RedfishAction> GetActions(JsonElement root)
        {
            var actions = new List<RedfishAction>();

            if (!root.TryGetProperty("Actions", out JsonElement actionsElement) ||
                actionsElement.ValueKind != JsonValueKind.Object)
            {
                return actions;
            }

            foreach (JsonProperty actionProperty in actionsElement.EnumerateObject())
            {
                JsonElement actionData = actionProperty.Value;

                if (actionData.ValueKind != JsonValueKind.Object)
                    continue;

                if (!actionData.TryGetProperty("target", out JsonElement targetElement) ||
                    targetElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string? target = targetElement.GetString();

                if (string.IsNullOrWhiteSpace(target))
                    continue;

                var action = new RedfishAction
                {
                    Name = actionProperty.Name.TrimStart('#'),
                    Target = target
                };

                foreach (JsonProperty property in actionData.EnumerateObject())
                {
                    const string suffix = "@Redfish.AllowableValues";

                    if (!property.Name.EndsWith(suffix, StringComparison.Ordinal) ||
                        property.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    string parameterName = property.Name[..^suffix.Length];

                    var values = new List<string>();

                    foreach (JsonElement value in property.Value.EnumerateArray())
                    {
                        if (value.ValueKind == JsonValueKind.String)
                        {
                            string? text = value.GetString();

                            if (text != null)
                                values.Add(text);
                        }
                    }

                    action.Parameters[parameterName] = values;
                }

                actions.Add(action);
            }

            return actions;
        }

    }
}
