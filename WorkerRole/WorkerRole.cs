using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Configuration;
using System.IO;
using System.Xml.Linq;
using System.Xml;
using HtmlAgilityPack;
using Microsoft.WindowsAzure.Storage.Table;
using ClassLibrary1;
using System.Text.RegularExpressions;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        private HashSet<string> visitedUrls = new HashSet<string>();
        private HashSet<string> cnnBlacklist = new HashSet<string>();
        private HashSet<string> bleacherBlacklist = new HashSet<string>();
        private List<string> initialSitemap = new List<string>();
        private DateTime start = new DateTime(2016, 04, 01);
        private XmlNamespaceManager mngr = new XmlNamespaceManager(new NameTable());
        private CloudQueue queue;
        private CloudQueue startQueue;
        private CloudQueue lastTen;
        private CloudQueue state;
        private CloudQueue errors;
        private CloudTable table;
        private bool crawl = false;
        private CloudTable cpuTable;
        private Crawler cr = new Crawler();

        public override void Run()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            queue = queueClient.GetQueueReference("urls");
            queue.CreateIfNotExists();

            startQueue = queueClient.GetQueueReference("command");
            startQueue.CreateIfNotExists();

            lastTen = queueClient.GetQueueReference("lastten");
            lastTen.CreateIfNotExists();

            state = queueClient.GetQueueReference("state");
            state.CreateIfNotExists();

            errors = queueClient.GetQueueReference("error");
            errors.CreateIfNotExists();

            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            table = tableClient.GetTableReference("urls");
            table.CreateIfNotExists();

            cpuTable = tableClient.GetTableReference("cpu");
            cpuTable.CreateIfNotExists();

            string cnnRobots = "http://www.cnn.com/robots.txt";
            string bleacherRobots = "http://bleacherreport.com/robots.txt";

            cnnBlacklist = cr.createBlacklist(cnnRobots, cnnBlacklist);
            bleacherBlacklist = cr.createBlacklist(bleacherRobots, bleacherBlacklist);
            initialSitemap = cr.getSitemap();

            while (true)
            {
                getState("idle");
                if (startOrStop("start"))
                {
                    crawl = true;
                }
                for (int i = 0; i < initialSitemap.Count && crawl; i++)
                {
                    getState("loading");
                    searchSitemap(initialSitemap[i], queue);
                }
                while (crawl)
                {
                    if (startOrStop("stop"))
                    {
                        crawl = false;
                    }
                    CloudQueueMessage getMessage = queue.GetMessage();
                    if (getMessage != null)
                    {
                        getState("crawling");
                        string message = getMessage.AsString;
                        try
                        {
                            getHTML(message);
                        }
                        catch
                        {
                            errors.AddMessage(getMessage);
                        }
                        queue.DeleteMessage(getMessage);
                        addSingleEntity("crawlentry", "crawlentry");
                    }
                }
            }
        }

        private void getState(string input)
        {
            CloudQueueMessage stateMessage = state.PeekMessage();
            if (stateMessage != null)
            {
                string smString = stateMessage.AsString;
                if (smString != input)
                {
                    CloudQueueMessage deleteMessage = state.GetMessage();
                    state.DeleteMessage(deleteMessage);
                    state.AddMessage(new CloudQueueMessage(input));
                }
            }
            else
            {
                state.AddMessage(new CloudQueueMessage(input));
            }
        }

        private void getHTML(string input)
        {
            lastTen.AddMessage(new CloudQueueMessage(input));
            lastTen.FetchAttributes();
            if (lastTen.ApproximateMessageCount > 10)
            {
                CloudQueueMessage first = lastTen.GetMessage();
                lastTen.DeleteMessage(first);

            }
            bool stop = false;
            if (input.Length > 6)
            {
                string checkLine = input.Substring(7);
                if (checkLine.Contains("//") && !(checkLine.Contains("videos") || checkLine.Contains("profiles") || checkLine.Contains("news")))
                {
                    stop = true;
                }
            }
            Uri uri;
            if (Uri.TryCreate(input, UriKind.Absolute, out uri) && !stop)
            {
                HtmlDocument htmlDoc = new HtmlWeb().Load(input);
                if (htmlDoc.DocumentNode != null)
                {
                    getCPU();
                    HtmlNode headNode = htmlDoc.DocumentNode.SelectSingleNode("//head");
                    if (headNode != null)
                    {
                        HtmlNode titleNode = headNode.SelectSingleNode("//title");
                        if (titleNode != null)
                        {
                            string title = titleNode.InnerText;
                            HtmlNodeCollection metaList = headNode.SelectNodes("//meta");
                            bool found = false;
                            if (metaList != null)
                            {
                                foreach (HtmlNode node in metaList)
                                {
                                    if (node.GetAttributeValue("name", "not found") == "pubdate" || node.GetAttributeValue("name", "not found") == "DATE")
                                    {
                                        string date = node.Attributes["content"].Value;
                                        addToTable(input, title, date);
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found)
                                {
                                    addToTable(input, title, null);
                                }
                            }
                        }
                    }
                    HtmlNode bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");
                    if (bodyNode != null)
                    {
                        HtmlNodeCollection urlList = bodyNode.SelectNodes("//a");
                        if (urlList != null)
                        {
                            string currentSite = "";
                            if (input.Contains("http://www.cnn.com/"))
                            {
                                currentSite = "http://www.cnn.com/";
                            }
                            else if (input.Contains("http://bleacherreport.com/"))
                            {
                                currentSite = "http://bleacherreport.com/";
                            }
                            foreach (HtmlNode node in urlList)
                            {
                                var href = node.Attributes["href"];
                                if (href != null)
                                {
                                    string newUrl = node.Attributes["href"].Value;
                                    if (newUrl.Length > 1)
                                    {
                                        if (newUrl.StartsWith("/") && checkChar(newUrl[1]))
                                        {
                                            newUrl = currentSite + newUrl;
                                        }
                                        if (currentSite != "")
                                        {
                                            check(newUrl, currentSite);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool checkChar(char c)
        {
            return (c >= 'a' && c <= 'z');
        }

        private void addToTable(string url, string title, string date)
        {
            string newTitle = Regex.Replace(title.ToLower(), @"[^0-9a-z ]+", "");
            string[] words = newTitle.Split(' ');
            string[] uniqueWords = words.Distinct().ToArray();
            foreach (string word in uniqueWords)
            {
                if (word != "")
                {
                    TableOperation insertOperation = TableOperation.Insert(new Page(url, title, date, word));
                    table.Execute(insertOperation);
                    addSingleEntity("counterentry", "counterentry");
                }
            }
        }

        private void searchSitemap(string url, CloudQueue queue)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(url);
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("sm", "http://www.sitemaps.org/schemas/sitemap/0.9");
            nsmgr.AddNamespace("img", "http://www.google.com/schemas/sitemap-image/1.1");
            nsmgr.AddNamespace("news", "http://www.google.com/schemas/sitemap-news/0.9");
            nsmgr.AddNamespace("video", "http://www.google.com/schemas/sitemap-video/1.1");
            nsmgr.AddNamespace("brsm", "http://www.google.com/schemas/sitemap/0.9");
            XmlNode root = doc.DocumentElement;
            XmlNodeList sitemaps = root.SelectNodes("descendant::sm:sitemap", nsmgr);
            XmlNodeList urls = root.SelectNodes("descendant::sm:url", nsmgr);
            XmlNodeList brurls = root.SelectNodes("descendant::brsm:url", nsmgr);
            if (sitemaps.Count != 0)
            {
                foreach (string sitemap in cr.searchForSitemaps(sitemaps, nsmgr, start))
                {
                    if (!initialSitemap.Contains(sitemap))
                    {
                        initialSitemap.Add(sitemap);
                    }
                }
            }
            else if (urls.Count != 0)
            {
                foreach (XmlNode node in urls)
                {
                    var urlNode = node.SelectSingleNode("sm:loc", nsmgr);
                    string urlText = urlNode.InnerText;
                    if (urlText.Contains("http://www.cnn.com/"))
                    {
                        check(urlText, "http://www.cnn.com/");
                    }
                }
            }
            else if (brurls.Count != 0)
            {
                foreach (XmlNode node in brurls)
                {
                    var urlNode = node.SelectSingleNode("brsm:loc", nsmgr);
                    string urlText = urlNode.InnerText;
                    if (urlText.Contains("http://bleacherreport.com/"))
                    {
                        check(urlText, "http://bleacherreport.com/");
                    }
                }
            }
        }

        private string check(string line, string site)
        {

            if (line.StartsWith(site))
            {
                if (site == "http://www.cnn.com/")
                {
                    foreach (string url in cnnBlacklist)
                    {
                        if (line.Contains(url))
                        {
                            return "";
                        }
                    }
                }
                else if (site == "http://bleacherreport.com/")
                {
                    site += "articles";
                    foreach (string url in bleacherBlacklist)
                    {
                        if (line.Contains(url))
                        {
                            return "";
                        }
                    }
                }
                if (!visitedUrls.Contains(line) && line.StartsWith(site))
                {
                    visitedUrls.Add(line);
                    queue.AddMessage(new CloudQueueMessage(line));
                    return line;
                }
                else
                {
                    return "";
                }
            }
            return "";
        }

        private void addSingleEntity(string partition, string row)
        {
            TableOperation retrieve = TableOperation.Retrieve<Counter>(partition, row);
            TableResult retrievedResult = table.Execute(retrieve);
            if (retrievedResult.Result == null)
            {
                TableOperation insertNewCounter = TableOperation.Insert(new Counter(0, partition, row));
                table.Execute(insertNewCounter);
            }
            else
            {
                int newCount = ((Counter)retrievedResult.Result).count;
                TableOperation delete = TableOperation.Delete((Counter)retrievedResult.Result);
                table.Execute(delete);
                TableOperation insertNewCount = TableOperation.Insert(new Counter(newCount, partition, row));
                table.Execute(insertNewCount);
            }
        }

        private void getCPU()
        {
            PerformanceCounter cpuCounter = new PerformanceCounter();
            cpuCounter.CategoryName = "Processor";
            cpuCounter.CounterName = "% Processor Time";
            cpuCounter.InstanceName = "_Total";
            double first = cpuCounter.NextValue();
            string val = cpuCounter.NextValue().ToString();
            TableOperation retrieveCPU = TableOperation.Retrieve<CPU>("cpu", "cpu");
            TableResult cpuResult = cpuTable.Execute(retrieveCPU);
            if (cpuResult.Result == null)
            {
                TableOperation insertNewCPU = TableOperation.Insert(new CPU(val));
                cpuTable.Execute(insertNewCPU);
            }
            else
            {
                TableOperation delete = TableOperation.Delete((CPU)cpuResult.Result);
                cpuTable.Execute(delete);
                TableOperation insertNewCount = TableOperation.Insert(new CPU(val));
                cpuTable.Execute(insertNewCount);
            }
        }

        private bool startOrStop(string input)
        {
            CloudQueueMessage message = startQueue.GetMessage();
            if (message != null)
            {
                startQueue.DeleteMessage(message);
                string command = message.AsString.ToLower();
                if (command == input)
                {
                    return true;
                }
            }
            return false;
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            Trace.TraceInformation("WorkerRole1 has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("WorkerRole1 is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("WorkerRole1 has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following with your own logic.
            while (!cancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Working");
                await Task.Delay(1000);
            }
        }
    }
}
