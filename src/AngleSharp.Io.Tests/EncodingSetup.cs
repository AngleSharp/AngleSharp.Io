//  in AngleSharp.Io (not .Tests) to also cover the fixtures in AngleSharp.Io.Network.Tests
namespace AngleSharp.Io
{
    using NUnit.Framework;
    using System.Text;

    [SetUpFixture]
    public class EncodingSetup
    {
        [OneTimeSetUp]
        public void RegisterCodePages()
        {
            //  AngleSharp's TextEncoding resolves its encodings once, and only Configuration
            //  registers the code pages; a test using, e.g., Url.Create without any
            //  configuration would otherwise leave all legacy encodings as UTF-8 for the
            //  rest of the run, depending on the order of the tests
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }
}
