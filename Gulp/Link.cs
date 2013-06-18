using System;
using System.IO;
using System.Diagnostics.Contracts;

namespace Gulp
{
    public class Link : IComparable<Link>
    {
        internal Link(Uri baseUri, string url, LinkKind kind)
        {
            Contract.Requires(url.IsLink(baseUri, kind));

            Uri = new Uri(url, UriKind.RelativeOrAbsolute);

            if (!Uri.IsAbsoluteUri)
                Uri = new Uri(baseUri, Uri);

            Kind = kind;
        }

        public Uri Uri { get; set; }
        public LinkKind Kind { get; set; }

        public string GetFileName(string basePath, bool includeAuthority)
        {
            var fileName = Uri.ToUrlHash() +
                Path.GetExtension(Uri.LocalPath).ToLower();

            if (includeAuthority)
                return Path.Combine(basePath, Uri.Authority, fileName);
            else
                return Path.Combine(basePath, fileName);
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}", Kind, Uri);
        }

        public int CompareTo(Link other)
        {
            if (Kind == other.Kind)
                return Uri.AbsoluteUri.CompareTo(other.Uri.AbsoluteUri);
            else
                return Kind.CompareTo(other.Kind);
        }
    }
}
