using System;
using System.Collections.Generic;
using System.Linq;

namespace Gulp
{
    public static class LinkParser
    {
        public static List<Link> GetLinks(this string html, Uri baseUri)
        {
            var links = new List<Link>();

            if (string.IsNullOrWhiteSpace(html))
                return links;

            var parser = new HtmlParser(html);

            HtmlParser.HtmlTag tag;

            while (parser.ParseNext(name => name == "a" || name == "img", out tag))
            {
                string url;

                if (tag.Name == "a")
                {
                    if (tag.Attributes.TryGetValue("href", out url))
                        if (url.IsLink(baseUri, LinkKind.HTML))
                            links.Add(new Link(baseUri, url, LinkKind.HTML));
                }
                else
                {
                    if (tag.Attributes.TryGetValue("src", out url))
                        if (url.IsLink(baseUri, LinkKind.Image))
                            links.Add(new Link(baseUri, url, LinkKind.Image));
                }
            }

            return links.Distinct().OrderBy(link => Guid.NewGuid()).ToList();
        }
    }
}
