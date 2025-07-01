using System.Net;

namespace DisplayLogic.Tests.TestUtils
{
    public class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string content = "")
        {
            _response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}
