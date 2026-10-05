using System.Net;
using LedgerNest.Desktop.Updates;

namespace LedgerNest.Desktop.Tests.Settings;

[Trait("Category", "Unit")]
public sealed class UpdateServiceTests
{
    [Theory]
    [InlineData(null, UpdateCheckState.NotConfigured)]
    [InlineData("", UpdateCheckState.NotConfigured)]
    [InlineData(" ", UpdateCheckState.NotConfigured)]
    [InlineData("not-a-url", UpdateCheckState.Failed)]
    [InlineData("http://updates.example.test/manifest", UpdateCheckState.Failed)]
    [InlineData("file:///manifest.json", UpdateCheckState.Failed)]
    public async Task CheckUpdates_InvalidConfiguration_NoNetworkRequest(string? url, UpdateCheckState expected)
    {
        using var handler = new StubHandler(_ => throw new InvalidOperationException("Network must not be called"));
        using var client = new HttpClient(handler);
        Assert.Equal(expected, (await new HttpAppUpdateService(client).CheckAsync(url)).State);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("{\"version\":\"999.0.0\",\"downloadUrl\":\"https://updates.example.test/app\"}", UpdateCheckState.Available)]
    [InlineData("{\"version\":\"0.0.0\"}", UpdateCheckState.Current)]
    [InlineData("{\"version\":\"invalid\"}", UpdateCheckState.Failed)]
    [InlineData("{\"version\":\"999.0.0\",\"downloadUrl\":\"http://unsafe.example.test/app\"}", UpdateCheckState.Failed)]
    [InlineData("null", UpdateCheckState.Failed)]
    [InlineData("{broken", UpdateCheckState.Failed)]
    public async Task CheckUpdates_Manifest_ValidatesVersionAndDownloadSafety(string payload, UpdateCheckState expected)
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) });
        using var client = new HttpClient(handler);
        var result = await new HttpAppUpdateService(client).CheckAsync("https://updates.example.test/manifest");
        Assert.Equal(expected, result.State);
        Assert.NotEmpty(result.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task CheckUpdates_ServiceFailure_ReturnsMeaningfulError(HttpStatusCode status)
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(status));
        using var client = new HttpClient(handler);
        var result = await new HttpAppUpdateService(client).CheckAsync("https://updates.example.test/manifest");
        Assert.Equal(UpdateCheckState.Failed, result.State);
        Assert.Contains(((int)status).ToString(), result.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckUpdates_TransportOrTimeoutFailure_ReturnsFailed(bool timeout)
    {
        using var handler = new StubHandler(_ => throw (timeout ? (Exception)new TaskCanceledException("timeout") : new HttpRequestException("offline")));
        using var client = new HttpClient(handler);
        Assert.Equal(UpdateCheckState.Failed, (await new HttpAppUpdateService(client).CheckAsync("https://updates.example.test/manifest")).State);
    }

    [Fact]
    public async Task CheckUpdates_OversizeManifest_Rejected()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string(' ', 128 * 1024 + 1)) });
        using var client = new HttpClient(handler);
        Assert.Equal(UpdateCheckState.Failed, (await new HttpAppUpdateService(client).CheckAsync("https://updates.example.test/manifest")).State);
    }

    [Fact]
    public async Task CheckUpdates_CallerCancellation_Propagated()
    {
        using var handler = new StubHandler(_ => throw new OperationCanceledException());
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new HttpAppUpdateService(client).CheckAsync("https://updates.example.test/manifest", cancellation.Token));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(respond(request));
        }
    }
}
