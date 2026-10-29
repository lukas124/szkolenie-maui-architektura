using FieldService.Domain.Visits;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Tests.Navigation;

public class DeepLinkParserTests
{
    [Test]
    public void Parse_VisitLink_ReturnsVisitDetailsRoute()
    {
        var id = Guid.NewGuid();

        var route = DeepLinkParser.Parse(new Uri($"fieldservice://visits/{id}"));

        Assert.That(route, Is.EqualTo(new VisitDetailsRoute(new VisitId(id))));
    }

    [Test]
    public void Parse_VisitsLink_ReturnsVisitsListRoute()
    {
        var route = DeepLinkParser.Parse(new Uri("fieldservice://visits"));

        Assert.That(route, Is.EqualTo(new VisitsListRoute()));
    }

    [TestCase("fieldservice://visits/abc")]
    [TestCase("fieldservice://visits/12345")]
    public void Parse_InvalidGuid_ReturnsNull(string link)
    {
        Assert.That(DeepLinkParser.Parse(new Uri(link)), Is.Null);
    }

    [TestCase("https://visits/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [TestCase("otherapp://visits/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [TestCase("fieldservice://orders/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [TestCase("fieldservice://orders")]
    public void Parse_OtherSchemeOrHost_ReturnsNull(string link)
    {
        Assert.That(DeepLinkParser.Parse(new Uri(link)), Is.Null);
    }
}
