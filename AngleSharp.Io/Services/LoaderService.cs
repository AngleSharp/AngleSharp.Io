namespace AngleSharp.Io.Services
{
    using AngleSharp.Dom;
    using AngleSharp.Network;
    using AngleSharp.Network.Default;
    using AngleSharp.Services;
    using System.Collections.Generic;

    /// <summary>
    /// The adjusted loader service to use.
    /// </summary>
    public class LoaderService : ILoaderService
    {
        readonly IEnumerable<IRequester> _requesters;

        /// <summary>
        /// Creates a new loader service with the provided requesters.
        /// </summary>
        /// <param name="requesters">The requesters to use.</param>
        public LoaderService(IEnumerable<IRequester> requesters)
        {
            _requesters = requesters;
        }

        /// <summary>
        /// Gets the available requesters.
        /// </summary>
        public IEnumerable<IRequester> Requesters
        {
            get { return _requesters; }
        }

        /// <summary>
        /// Gets the appropriate requester for the provided address.
        /// </summary>
        /// <param name="address">
        /// The address the requester needs to be able to handle.
        /// </param>
        /// <returns>The requester or null.</returns>
        public IRequester GetRequester(Url address)
        {
            foreach (var requester in _requesters)
            {
                if (requester.SupportsProtocol(address.Scheme))
                    return requester;
            }

            return default(IRequester);
        }

        /// <summary>
        /// Creates the document loader for the given context.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <returns>The created document loader.</returns>
        public IDocumentLoader CreateDocumentLoader(IBrowsingContext context)
        {
            return new DocumentLoader(_requesters, context);
        }

        /// <summary>
        /// Creates the resource loader for the given document.
        /// </summary>
        /// <param name="document">The document to host the loading.</param>
        /// <returns>The created resource loader.</returns>
        public IResourceLoader CreateResourceLoader(IDocument document)
        {
            return new ResourceLoader(_requesters, document);
        }
    }
}
