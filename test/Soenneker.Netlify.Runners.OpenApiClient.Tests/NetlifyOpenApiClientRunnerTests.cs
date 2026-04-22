using Soenneker.Tests.HostedUnit;

namespace Soenneker.Netlify.Runners.OpenApiClient.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class NetlifyOpenApiClientRunnerTests : HostedUnitTest
{
    public NetlifyOpenApiClientRunnerTests(Host host) : base(host)
    {
    }

    [Test]
    public void Default()
    {

    }
}
