
using System.Collections.Generic;

namespace RedfishController.Models
{
    public sealed class RedfishAction
    {
        public string Name { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public Dictionary<string, List<string>> Parameters
            { get; set; } = new();
    }
}
