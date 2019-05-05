namespace AngleSharp.Io.Network
{
    using AngleSharp.Dom;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    sealed class DownloadFactory : DefaultDocumentFactory
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly Func<MimeType, IResponse, Boolean> _download;

        public DownloadFactory(IDocumentFactory documentFactory, Func<MimeType, IResponse, Boolean> download)
        {
            _documentFactory = documentFactory;
            _download = download;
        }

        protected override Task<IDocument> CreateDefaultAsync(IBrowsingContext context, CreateDocumentOptions options, CancellationToken cancellationToken)
        {
            if (_download(options.ContentType, options.Response))
            {
                return Task.FromResult<IDocument>(null);
            }

            return _documentFactory.CreateAsync(context, options, cancellationToken);
        }
    }
}
