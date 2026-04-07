using Soenneker.Tests.FixturedUnit;
using Xunit;

namespace Soenneker.Netlify.Runners.OpenApiClient.Tests;

[Collection("Collection")]
public sealed class NetlifyOpenApiClientRunnerTests : FixturedUnitTest
{
    public NetlifyOpenApiClientRunnerTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact]
    public void Default()
    {

    }
}
