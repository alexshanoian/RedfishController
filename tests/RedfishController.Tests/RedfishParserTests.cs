
using System.Linq;
using RedfishController.Helpers;
using Xunit;

namespace RedfishController.Tests
{
    public class RedfishParserTests
    {
        [Fact]
        public void GetResourceLinks_DiscoversServiceRootLinks()
        {
            string json = """
            {
                "Systems": {
                    "@odata.id": "/redfish/v1/Systems"
                },
                "Chassis": {
                    "@odata.id": "/redfish/v1/Chassis"
                }
            }
            """;

            var links = RedfishParser.GetResourceLinks(json);

            Assert.Equal(2, links.Count);

            Assert.Contains(links, link =>
                link.Name == "Systems" &&
                link.Path == "/redfish/v1/Systems");

            Assert.Contains(links, link =>
                link.Name == "Chassis" &&
                link.Path == "/redfish/v1/Chassis");
        }

        [Fact]
        public void GetResourceLinks_DiscoversCollectionMembers()
        {
            string json = """
            {
                "Members": [
                    {
                        "@odata.id": "/redfish/v1/Systems/1"
                    },
                    {
                        "@odata.id": "/redfish/v1/Systems/2"
                    }
                ]
            }
            """;

            var links = RedfishParser.GetResourceLinks(json);

            Assert.Equal(2, links.Count);
            Assert.Equal("1", links[0].Name);
            Assert.Equal("2", links[1].Name);
        }

        [Fact]
        public void GetResourceLinks_EmptyResource_ReturnsNoLinks()
        {
            var links = RedfishParser.GetResourceLinks("{}");

            Assert.Empty(links);
        }
    }
}
