namespace AngleSharp.Io.Tests.Network
{
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Io.Network;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;

    [TestFixture]
    public class AttachedFileNameTests
    {
        [TestCase("attachment; filename=\"report.pdf\"", "report.pdf")]
        [TestCase("attachment; filename=\"../../Startup/run.bat\"", "run.bat")]
        [TestCase("attachment; filename=\"..\\..\\Startup\\run.bat\"", "run.bat")]
        [TestCase("attachment; filename=\"C:\\Windows\\Temp\\evil.dll\"", "evil.dll")]
        [TestCase("attachment; filename=\"C:evil.dll\"", "C_evil.dll")]
        public void AttachedFileNameIsOnlyAFileName(String disposition, String expected)
        {
            Assert.AreEqual(expected, CreateResponse(disposition).GetAttachedFileName());
        }

        [TestCase("attachment; filename=\"..\"")]
        [TestCase("attachment; filename=\"Startup/..\"")]
        public void AttachedFileNameIsNeverAParentFolder(String disposition)
        {
            var name = CreateResponse(disposition).GetAttachedFileName();

            Assert.AreEqual("_", Path.GetFileNameWithoutExtension(name));
        }

        private static DefaultResponse CreateResponse(String disposition)
        {
            return new DefaultResponse
            {
                Address = Url.Create("https://example.org/download"),
                StatusCode = HttpStatusCode.OK,
                Headers = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
                {
                    [HeaderNames.ContentDisposition] = disposition,
                },
                Content = new MemoryStream(),
            };
        }
    }
}
