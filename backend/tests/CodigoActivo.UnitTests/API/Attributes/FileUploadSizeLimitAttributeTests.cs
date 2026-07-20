using System.Text;
using AwesomeAssertions;
using CodigoActivo.API.Attributes;
using CodigoActivo.Domain.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.API.Attributes;

public sealed class FileUploadSizeLimitAttributeTests
{
    private const long MultipartOverheadBytes = 64 * 1024;
    private const string Boundary = "upload-size-limit-test";

    private static IAuthorizationFilter BuildFilter(long maxSizeBytes)
    {
        var provider = new ServiceCollection()
            .AddSingleton(new FileStorageOptions { MaxSizeBytes = maxSizeBytes })
            .BuildServiceProvider();
        return (IAuthorizationFilter)new FileUploadSizeLimitAttribute().CreateInstance(provider);
    }

    private static AuthorizationFilterContext BuildContext(HttpContext httpContext)
    {
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    private static DefaultHttpContext MultipartContext(int payloadBytes)
    {
        var body =
            $"--{Boundary}\r\n"
            + "Content-Disposition: form-data; name=\"file\"\r\n\r\n"
            + new string('a', payloadBytes)
            + $"\r\n--{Boundary}--\r\n";
        var bytes = Encoding.ASCII.GetBytes(body);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.ContentType = $"multipart/form-data; boundary={Boundary}";
        httpContext.Request.Body = new MemoryStream(bytes);
        httpContext.Request.ContentLength = bytes.Length;
        return httpContext;
    }

    [Fact]
    public void OnAuthorization_WritableBodySizeFeature_SetsConfiguredMaxPlusMultipartOverhead()
    {
        const long ConfiguredMax = 20 * 1024 * 1024;
        var feature = new FakeMaxRequestBodySizeFeature();
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);

        BuildFilter(ConfiguredMax).OnAuthorization(BuildContext(httpContext));

        feature.MaxRequestBodySize.Should().Be(ConfiguredMax + MultipartOverheadBytes);
    }

    [Fact]
    public void OnAuthorization_ReadOnlyBodySizeFeature_LeavesFeatureUntouched()
    {
        var feature = new FakeMaxRequestBodySizeFeature { IsReadOnly = true };
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);

        BuildFilter(maxSizeBytes: 1024).OnAuthorization(BuildContext(httpContext));

        feature.MaxRequestBodySize.Should().BeNull();
    }

    [Fact]
    public void OnAuthorization_FormAlreadyRead_KeepsExistingFormFeature()
    {
        var existing = new FormFeature(FormCollection.Empty);
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IFormFeature>(existing);

        BuildFilter(maxSizeBytes: 1024).OnAuthorization(BuildContext(httpContext));

        httpContext.Features.Get<IFormFeature>().Should().BeSameAs(existing);
    }

    [Fact]
    public async Task OnAuthorization_MultipartBodyAboveConfiguredMax_FailsFormRead()
    {
        var httpContext = MultipartContext(payloadBytes: 128 * 1024);

        BuildFilter(maxSizeBytes: 1024).OnAuthorization(BuildContext(httpContext));

        var act = () => httpContext.Request.ReadFormAsync(TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task OnAuthorization_MultipartBodyWithinConfiguredMax_ReadsForm()
    {
        var httpContext = MultipartContext(payloadBytes: 128 * 1024);

        BuildFilter(maxSizeBytes: 256 * 1024).OnAuthorization(BuildContext(httpContext));

        var form = await httpContext.Request.ReadFormAsync(
            TestContext.Current.CancellationToken
        );
        form["file"].ToString().Should().HaveLength(128 * 1024);
    }

    private sealed class FakeMaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        private long? maxRequestBodySize;

        public bool IsReadOnly { get; init; }

        public long? MaxRequestBodySize
        {
            get => maxRequestBodySize;
            set
            {
                if (IsReadOnly)
                    throw new InvalidOperationException(
                        "MaxRequestBodySize cannot be set after reading has started."
                    );
                maxRequestBodySize = value;
            }
        }
    }
}
