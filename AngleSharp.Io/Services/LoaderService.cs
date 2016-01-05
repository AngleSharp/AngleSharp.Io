namespace AngleSharp.Io.Services
{
    using AngleSharp.Network;
    using AngleSharp.Network.Default;
    using AngleSharp.Services;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The adjusted loader service to use.
    /// </summary>
    public class LoaderService : ILoaderService
    {
        #region Fields

        readonly IEnumerable<IRequester> _requesters;
        readonly Predicate<IRequest> _filter;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new loader service with the provided requesters.
        /// </summary>
        /// <param name="requesters">The requesters to use.</param>
        /// <param name="filter">The request filter to use, if any.</param>
        public LoaderService(IEnumerable<IRequester> requesters, Predicate<IRequest> filter = null)
        {
            _requesters = requesters;
            _filter = filter;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the filter to use, if any.
        /// </summary>
        public Predicate<IRequest> Filter
        {
            get { return _filter; }
        }

        /// <summary>
        /// Gets the available requesters.
        /// </summary>
        public IEnumerable<IRequester> Requesters
        {
            get { return _requesters; }
        }

        #endregion

        #region Methods

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
                {
                    return requester;
                }
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
            return new DocumentLoader(_requesters, context.Configuration, _filter);
        }

        /// <summary>
        /// Creates the resource loader for the given context.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <returns>The created resource loader.</returns>
        public IResourceLoader CreateResourceLoader(IBrowsingContext context)
        {
            return new ResourceLoader(_requesters, context.Configuration, _filter);
        }

        #endregion
    }
}
