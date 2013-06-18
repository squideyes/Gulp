using HtmlAgilityPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Gulp
{
    public class Spider
    {
        private CancellationTokenSource cts = null;
        private List<Task> tasks = new List<Task>();

        private static ConcurrentDictionary<string, bool> urls;

        public event EventHandler<GenericArgs<LogItem>> OnLog;
        public event EventHandler<GenericArgs<AggregateException>> OnFailure;

        public void Fetch(Link initialLink)
        {
            urls = new ConcurrentDictionary<string, bool>();

            cts = new CancellationTokenSource();

            var cancellable = new ExecutionDataflowBlockOptions()
            {
                CancellationToken = cts.Token
            };

            var hrefRegex = new Regex(@"(?<=<img\s.*?src\s*?=\s*?"").*?(?= "".*?>)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var srcRegex = new Regex(@"(?<=<a\s.*?href\s*?=\s*?"").*?(?= "".*?>)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var scraper = new TransformBlock<Link, Tuple<Link, HtmlDocument>>(
                async link =>
                {
                    if (!urls.TryAdd(link.Uri.AbsoluteUri, true))
                    {
                        Log(LogKind.DupHTML, link.Uri.AbsoluteUri);

                        return new Tuple<Link, HtmlDocument>(link, null);
                    }

                    HtmlDocument doc;

                    try
                    {
                        var response = await link.Uri.GetResponse();

                        if (!IsSuccessStatus(response, link))
                            return new Tuple<Link, HtmlDocument>(link, null);

                        var html = await response.Content.ReadAsStringAsync();

                        doc = new HtmlDocument();
 
                        doc.LoadHtml(html);

                        Log(LogKind.Scrape, link.Uri.AbsoluteUri);
                    }
                    catch (Exception error)
                    {
                        doc = null;

                        LogError(link, error);
                    }

                    return new Tuple<Link, HtmlDocument>(link, doc);
                },
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = cts.Token
                });

            var docCaster = new BroadcastBlock<Tuple<Link, HtmlDocument>>(
                content => content,
                cancellable);

            //(?<=<a\s.*?href\s*?=.*?)".*?"(?=.*?>.*?</a>)

            var hrefParser = new TransformManyBlock<Tuple<Link, HtmlDocument>, Link>(
                tuple =>
                {
                    return (from link in tuple.Item2.DocumentNode.Descendants("a")
                            where link.Attributes.Contains("href")
                            let href = link.GetAttributeValue("href", "")
                            where href.IsLink(tuple.Item1.Uri, LinkKind.HTML, UriFilter.None)
                            select new Link(tuple.Item1.Uri, href, LinkKind.HTML)).Distinct();
                },
                cancellable);

            var srcParser = new TransformManyBlock<Tuple<Link, HtmlDocument>, Link>(
                tuple =>
                {
                    return (from img in tuple.Item2.DocumentNode.Descendants("img")
                            where img.Attributes.Contains("src")
                            let src = img.GetAttributeValue("src", "")
                            where src.IsLink(tuple.Item1.Uri, LinkKind.Image, UriFilter.None)
                            select new Link(tuple.Item1.Uri, src, LinkKind.Image)).Distinct();
                },
                cancellable);

            var fetcher = new ActionBlock<Link>(
               async link =>
               {
                   try
                   {
                       var fileName = link.GetFileName("Downloads", false);

                       if (File.Exists(fileName))
                       {
                           Log(LogKind.DupMedia, link.Uri.AbsoluteUri);

                           return;
                       }

                       var response = await link.Uri.GetResponse();

                       if (!IsSuccessStatus(response, link))
                           return;

                       EnsurePathExists(fileName);

                       var webStream = await response.Content.ReadAsStreamAsync();

                       using (var fileStream = File.OpenWrite(fileName))
                           await webStream.CopyToAsync(fileStream);

                       Log(LogKind.Fetch, Path.GetFileName(fileName));
                   }
                   catch (Exception error)
                   {
                       LogError(link, error);
                   }
               },
               cancellable);

            scraper.Completion.SetOnlyOnFaultedCompletion(error => HandleFailure(error));
            docCaster.Completion.SetOnlyOnFaultedCompletion(error => HandleFailure(error));
            hrefParser.Completion.SetOnlyOnFaultedCompletion(error => HandleFailure(error));
            srcParser.Completion.SetOnlyOnFaultedCompletion(error => HandleFailure(error));
            fetcher.Completion.SetOnlyOnFaultedCompletion(error => HandleFailure(error));

            scraper.LinkTo(docCaster);
            docCaster.LinkTo(hrefParser, tuple => tuple.Item2 != null);
            docCaster.LinkTo(srcParser, tuple => tuple.Item2 != null);
            docCaster.LinkTo(DataflowBlock.NullTarget<Tuple<Link, HtmlDocument>>());
            hrefParser.LinkTo(scraper);
            srcParser.LinkTo(fetcher);

            tasks.Add(scraper.Completion);
            tasks.Add(docCaster.Completion);
            tasks.Add(hrefParser.Completion);
            tasks.Add(srcParser.Completion);
            tasks.Add(fetcher.Completion);

            scraper.Post(initialLink);
        }

        public async Task Cancel()
        {
            cts.Cancel();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void EnsurePathExists(string fileName)
        {
            var path = Path.GetDirectoryName(fileName);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private void Log(LogKind kind, string format, params object[] args)
        {
            if (OnLog != null)
                OnLog(this, new GenericArgs<LogItem>(new LogItem(kind, format, args)));
        }

        private void LogError(Link link, Exception error)
        {
            if (error.InnerException != null)
                Log(LogKind.Error, "{0} Error: {1}", link.Kind, error.InnerException.Message);
            else
                Log(LogKind.Error, "{0} Error: {1}", link.Kind, error.Message);
        }

        private void HandleFailure(AggregateException errors)
        {
            if (OnFailure != null)
                OnFailure(this, new GenericArgs<AggregateException>(errors));
        }

        private bool IsSuccessStatus(HttpResponseMessage response, Link link)
        {
            if (response.IsSuccessStatusCode)
                return true;

            Log(LogKind.Warning, "{0} (Kind; {1}, URL: {2})",
                response.StatusCode, link.Kind, link.Uri);

            return false;
        }
    }
}
