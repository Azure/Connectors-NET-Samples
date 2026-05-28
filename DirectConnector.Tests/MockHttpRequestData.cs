//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;

namespace DirectConnector.Tests;

/// <summary>
/// A concrete <see cref="HttpRequestData"/> implementation for unit testing.
/// Wraps a <see cref="MemoryStream"/> body and supports query-string parameters.
/// </summary>
internal sealed class MockHttpRequestData : HttpRequestData
{
    private readonly MemoryStream _body;

    public MockHttpRequestData(
        FunctionContext context,
        string? body = null,
        string? method = "GET",
        string? url = "https://localhost/api/test")
        : base(context)
    {
        this._body = body != null
            ? new MemoryStream(Encoding.UTF8.GetBytes(body))
            : new MemoryStream();

        this.Method = method ?? "GET";
        this.Url = new Uri(url ?? "https://localhost/api/test");
    }

    public override Stream Body => this._body;

    public override HttpHeadersCollection Headers { get; } = [];

    public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = [];

    public override Uri Url { get; }

    public override IEnumerable<ClaimsIdentity> Identities { get; } = [];

    public override string Method { get; }

    public override HttpResponseData CreateResponse()
    {
        return new MockHttpResponseData(this.FunctionContext);
    }
}

/// <summary>
/// A concrete <see cref="HttpResponseData"/> implementation for unit testing.
/// Captures written body content for assertion.
/// </summary>
internal sealed class MockHttpResponseData : HttpResponseData
{
    public MockHttpResponseData(FunctionContext context)
        : base(context)
    {
        this.Body = new MemoryStream();
    }

    public override HttpStatusCode StatusCode { get; set; }

    public override HttpHeadersCollection Headers { get; set; } = [];

    public override Stream Body { get; set; }

    public override HttpCookies Cookies { get; }
        = Mock.Of<HttpCookies>();

    /// <summary>
    /// Reads the response body as a UTF-8 string (resets the stream position).
    /// </summary>
    public string GetBodyAsString()
    {
        this.Body.Position = 0;
        using var reader = new StreamReader(this.Body, leaveOpen: true);
        return reader.ReadToEnd();
    }
}
