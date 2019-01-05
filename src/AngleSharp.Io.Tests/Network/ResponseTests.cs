namespace AngleSharp.Io.Tests.Network
{
    using FluentAssertions;
    using NUnit.Framework;
    using System.Net;

    [TestFixture]
    public class ResponseTests
    {
        [Test]
        public void Initialize()
        {
            // ARRANGE, ACT
            var response = new DefaultResponse();

            // ASSERT
            response.Content.Should().BeNull();
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            response.Headers.Should().NotBeNull();
            response.Headers.Should().BeEmpty();
            response.Address.Should().BeNull();
        }
    }
}