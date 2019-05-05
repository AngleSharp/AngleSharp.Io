namespace AngleSharp.Io.Network.Tests.Integration
{
    using NUnit.Framework;
    using System.Collections.Generic;

    [TestFixture]
    public class ResponseTests
    {
        [Test]
        public void ReadFileNameFromContentDisposition()
        {
            var response = new DefaultResponse
            {
                Address = Url.Create("http://example.com/foo.png"),
                Headers = new Dictionary<string, string>
                {
                    { HeaderNames.ContentDisposition, "attachment; filename=\"filename.jpg\"" },
                }
            };
            var file = response.GetAttachedFileName();
            Assert.AreEqual("filename.jpg", file);
        }

        [Test]
        public void ReadFileNameFromUrlDespiteContentDisposition()
        {
            var response = new DefaultResponse
            {
                Address = Url.Create("http://example.com/foo.png"),
                Headers = new Dictionary<string, string>
                {
                    { HeaderNames.ContentDisposition, "attachment" },
                }
            };
            var file = response.GetAttachedFileName();
            Assert.AreEqual("foo.png", file);
        }

        [Test]
        public void ReadFileNameFromUrlWithoutContentDisposition()
        {
            var response = new DefaultResponse
            {
                Address = Url.Create("http://example.com/foo.png"),
                Headers = new Dictionary<string, string>
                {
                }
            };
            var file = response.GetAttachedFileName();
            Assert.AreEqual("foo.png", file);
        }

        [Test]
        public void ReadFileNameFromUrlWithoutEndingAndMimeType()
        {
            var response = new DefaultResponse
            {
                Address = Url.Create("http://example.com/foo"),
                Headers = new Dictionary<string, string>
                {
                }
            };
            var file = response.GetAttachedFileName();
            Assert.AreEqual("foo.a", file);
        }

        [Test]
        public void ReadFileNameFromUrlWithoutEndingButWithMimeType()
        {
            var response = new DefaultResponse
            {
                Address = Url.Create("http://example.com/foo"),
                Headers = new Dictionary<string, string>
                {
                    { HeaderNames.ContentType, "audio/mpeg3" },
                }
            };
            var file = response.GetAttachedFileName();
            Assert.AreEqual("foo.mp3", file);
        }

        [Test]
        public void IsAttachmentWhenContentDispositionHasAttachmentAndFile()
        {
            var response = new DefaultResponse
            {
                Address = Url.Create("http://example.com/foo.png"),
                Headers = new Dictionary<string, string>
                {
                    { HeaderNames.ContentDisposition, "attachment; filename=\"filename.jpg\"" },
                }
            };
            Assert.IsTrue(response.IsAttachment());
        }

        [Test]
        public void IsAttachmentWhenContentDispositionHasOnlyAttachment()
        {
            var response = new DefaultResponse
            {
                Address = Url.Create("http://example.com/foo.png"),
                Headers = new Dictionary<string, string>
                {
                    { HeaderNames.ContentDisposition, "attachment" },
                }
            };
            Assert.IsTrue(response.IsAttachment());
        }

        [Test]
        public void IsNoAttachmentWhenDispositionIsMissing()
        {
            var response = new DefaultResponse
            {
                Address = Url.Create("http://example.com/foo.png"),
                Headers = new Dictionary<string, string>
                {
                }
            };
            Assert.IsFalse(response.IsAttachment());
        }

        [Test]
        public void IsNoAttachmentWhenDispositionIsInline()
        {
            var response = new DefaultResponse
            {
                Address = Url.Create("http://example.com/foo.png"),
                Headers = new Dictionary<string, string>
                {
                    { HeaderNames.ContentDisposition, "inline" },
                }
            };
            Assert.IsFalse(response.IsAttachment());
        }
    }
}
