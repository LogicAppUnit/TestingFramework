// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace LogicAppUnit.Hosting
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using LogicAppUnit.Mocking;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.ResponseCompression;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    /// <summary>
    /// The mock HTTP host.
    /// </summary>
    internal class MockHttpHost : IDisposable
    {
        private readonly MockDefinition _mockDefinition;

        /// <summary>
        /// The web host.
        /// </summary>
        public IWebHost Host { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockHttpHost"/> class.
        /// <param name="mockDefinition">The definition of the requests and responses to be mocked.</param>
        /// <param name="url">URL for the mock host to listen on.</param>
        /// </summary>
        public MockHttpHost(MockDefinition mockDefinition, string url = null)
        {
            _mockDefinition = mockDefinition;

            this.Host = WebHost
                .CreateDefaultBuilder()
                .UseSetting(key: WebHostDefaults.SuppressStatusMessagesKey, value: "true")
                .ConfigureLogging(config => config.ClearProviders())
                .ConfigureServices(services =>
                {
                    services.AddSingleton<MockHttpHost>(this);
                })
                .UseStartup<Startup>()
                .UseUrls(url ?? TestEnvironment.FlowV2MockTestHostUri)
                .Build();

            this.Host.Start();
        }

        /// <summary>
        /// Disposes the resources.
        /// </summary>
        public void Dispose()
        {
            this.Host.StopAsync().Wait();
        }

        private class Startup
        {
            /// <summary>
            /// Gets or sets the request pipeline manager.
            /// </summary>
            private MockHttpHost Host { get; set; }

            public Startup(MockHttpHost host)
            {
                this.Host = host;
            }

            /// <summary>
            /// Configure the services.
            /// </summary>
            /// <param name="services">The services.</param>
            public void ConfigureServices(IServiceCollection services)
            {
                services
                    .Configure<IISServerOptions>(options =>
                    {
                        options.AllowSynchronousIO = true;
                    })
                    .AddResponseCompression(options =>
                    {
                        options.EnableForHttps = true;
                        options.Providers.Add<GzipCompressionProvider>();
                    })
                    .AddMvc(options =>
                    {
                        options.EnableEndpointRouting = true;
                    });
            }

            /// <summary>
            /// Configures the application.
            /// </summary>
            /// <param name="app">The application.</param>
            public void Configure(IApplicationBuilder app)
            {
                app.UseResponseCompression();

                app.Run(async (context) =>
                {
                    var syncIOFeature = context.Features.Get<IHttpBodyControlFeature>();
                    if (syncIOFeature != null)
                    {
                        syncIOFeature.AllowSynchronousIO = true;
                    }

                    using (var request = GetHttpRequestMessage(context))
                    // TODO: Why is '_mockDefinition' public?
                    using (var responseMessage = await this.Host._mockDefinition.MatchRequestAndBuildResponseAsync(request))
                    {
                        var response = context.Response;

                        response.StatusCode = (int)responseMessage.StatusCode;

                        var responseHeaders = responseMessage.Headers;

                        // Ignore the Transfer-Encoding header if it is just "chunked".
                        // We let the host decide about whether the response should be chunked or not.
                        if (responseHeaders.TransferEncodingChunked == true &&
                            responseHeaders.TransferEncoding.Count == 1)
                        {
                            responseHeaders.TransferEncoding.Clear();
                        }

                        foreach (var header in responseHeaders)
                        {
                            response.Headers.Append(header.Key, header.Value.ToArray());
                        }

                        if (responseMessage.Content != null)
                        {
                            var contentHeaders = responseMessage.Content.Headers;

                            // Copy the response content headers only after ensuring they are complete.
                            // We ask for Content-Length first because HttpContent lazily computes this
                            // and only afterwards writes the value into the content headers.
                            var unused = contentHeaders.ContentLength;

                            foreach (var header in contentHeaders)
                            {
                                response.Headers.Append(header.Key, header.Value.ToArray());
                            }

                            await responseMessage.Content.CopyToAsync(response.Body).ConfigureAwait(false);
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Gets the http request message.
        /// </summary>
        /// <param name="httpContext">The http context.</param>
        public static HttpRequestMessage GetHttpRequestMessage(HttpContext httpContext)
        {
            var feature = httpContext.Features.Get<HttpRequestMessageFeature>();
            if (feature == null)
            {
                feature = new HttpRequestMessageFeature(httpContext);
                httpContext.Features.Set(feature);
            }

            return feature.HttpRequestMessage;
        }
    }
}
