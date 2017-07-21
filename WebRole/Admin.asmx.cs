using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;
using ClassLibrary1;

namespace WebRole1
{
    /// <summary>
    /// Summary description for WebService1
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class WebService1 : System.Web.Services.WebService
    {
        private CloudQueue queue;
        private CloudQueue startQueue;
        private CloudQueue lastTen;
        private CloudQueue state;
        private CloudQueue errors;
        private CloudQueue queryStats;
        private CloudTable table;
        private CloudTable cpuTable;
        private static Dictionary<string, List<string>> cache = new Dictionary<string, List<string>>();
        
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string startCrawling()
        {
            setupStorageAccount();
            startQueue.AddMessage(new CloudQueueMessage("start"));
            return "Queue has started crawling";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string stopCrawling()
        {
            setupStorageAccount();
            startQueue.AddMessage(new CloudQueueMessage("stop"));
            return "Queue has stopped crawling";
        }

        [WebMethod]
        public string delete()
        {
            setupStorageAccount();
            table.DeleteIfExists();
            cpuTable.DeleteIfExists();
            queue.DeleteIfExists();
            lastTen.DeleteIfExists();
            errors.DeleteIfExists();
            Thread.Sleep(120000);
            table.Create();
            cpuTable.Create();
            queue.Create();
            lastTen.Create();
            errors.Create();
            return "Everything has been deleted";
        }

        [WebMethod]
        public List<string> getErrors()
        {
            setupStorageAccount();
            List<string> errorList = new List<string>();
            errors.FetchAttributes();
            if (errors.ApproximateMessageCount == 0)
            {
                errorList.Add("No errors yet");
            }
            else
            {
                for (int i = 0; i < errors.ApproximateMessageCount; i++)
                {
                    CloudQueueMessage message = errors.GetMessage();
                    errors.DeleteMessage(message);
                    errors.AddMessage(message);
                    string url = message.AsString;
                    errorList.Add(url);
                }
            }
            return errorList;
        }

<<<<<<< HEAD:Admin.asmx.cs
        //Returns the title of an article if it is in the database, and "Page not found" if not
        //
=======
>>>>>>> 57d4460e7885a4b0f48fdb3f9ca06e73c5efc8f0:WebRole/Admin.asmx.cs
        [WebMethod]
        public string getSingleTitle(string input, string word)
        {
            setupStorageAccount();
            string[] split = word.Split(' ');
            byte[] encodedURL = new UTF8Encoding().GetBytes(input);
            byte[] hash = ((HashAlgorithm)CryptoConfig.CreateFromName("MD5")).ComputeHash(encodedURL);
            string newUrl = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
            TableOperation retrieve = TableOperation.Retrieve<Page>(split[0], newUrl);
            TableResult retrievedResult = table.Execute(retrieve);
            string result = "";
            if (retrievedResult.Result != null)
            {
                result += ((Page)retrievedResult.Result).title;
            }
            else
            {
                result = "Page not found";
            }
            return result;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<string> getPageTitle(string title)
        {
            setupStorageAccount();
            title = title.ToLower();
            string[] split = title.Split(' ');
            string[] unique = split.Distinct().ToArray();
            if (cache == null || cache.Count >= 100)
            {
                cache = new Dictionary<string, List<string>>();
            }
            if (cache.ContainsKey(title))
            {
                return cache[title];
            } else
            {
                Dictionary<Page, int> results = new Dictionary<Page, int>();
                foreach (string word in unique)
                {
                    TableQuery<Page> rangeQuery = new TableQuery<Page>()
                        .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, word)).Take(1000);
                    foreach (Page entity in table.ExecuteQuery(rangeQuery))
                    {
                        int times = 0;
                        foreach (string check in unique)
                        {
                            if (entity.title.ToLower().Contains(check))
                            {
                                times++;
                            }
                        }
                        results[entity] = times;
                    }
                }
                var order = results.Select(x => x)
                    .OrderByDescending(x => x.Value)
                    .ThenByDescending(x => x.Key.date)
                    .Select(x => x.Key.urlName)
                    .Distinct()
                    .Take(20)
                    .ToList();
                if (order.Count == 0)
                {
                    order.Add("No pages found");
                }
                cache[title] = order;
                return order;
            }
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<string> getLastTen()
        {
            setupStorageAccount();
            List<string> urls = new List<string>();
            lastTen.FetchAttributes();
            if (lastTen.ApproximateMessageCount == 0)
            {
                urls.Add("No URL's have been crawled yet");
            }
            else
            {
                for (int i = 0; i < lastTen.ApproximateMessageCount; i++)
                {
                    CloudQueueMessage message = lastTen.GetMessage();
                    lastTen.DeleteMessage(message);
                    lastTen.AddMessage(message);
                    if (i < 10)
                    {
                        string url = message.AsString;
                        urls.Add(url);
                    }
                }
            }
            return urls;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getState()
        {
            setupStorageAccount();
            state.FetchAttributes();
            CloudQueueMessage message = state.PeekMessage();
            if (message != null)
            {
                return message.AsString;
            }
            return "The state of the crawler is unavailable at this time";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getCPU()
        {
            setupStorageAccount();
            TableOperation retrieve = TableOperation.Retrieve<CPU>("cpu", "cpu");
            TableResult retrievedResult = cpuTable.Execute(retrieve);
            if (retrievedResult.Result != null)
            {
                return ((CPU)retrievedResult.Result).cpu;
            }
            return "0";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public double getRAM()
        {
            PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            return ramCounter.NextValue();
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public int getSizeOfQueue()
        {
            setupStorageAccount();
            queue.FetchAttributes();
            return (int)queue.ApproximateMessageCount;
        }

        [WebMethod]
        public int getCrawled()
        {
            setupStorageAccount();
            TableOperation retrieve = TableOperation.Retrieve<Counter>("crawlentry", "crawlentry");
            TableResult retrievedResult = table.Execute(retrieve);
            if (retrievedResult.Result != null)
            {
                return ((Counter)retrievedResult.Result).count;
            }
            return 0;
        }

        [WebMethod]
        public int getSizeOfIndex()
        {
            setupStorageAccount();
            TableOperation retrieve = TableOperation.Retrieve<Counter>("counterentry", "counterentry");
            TableResult retrievedResult = table.Execute(retrieve);
            if (retrievedResult.Result != null)
            {
                return ((Counter)retrievedResult.Result).count;
            }
            return 0;
        }

        [WebMethod]
        public string queryResults()
        {
            setupStorageAccount();
            return queryStats.PeekMessage().AsString;
        }

        private void setupStorageAccount()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            queue = queueClient.GetQueueReference("urls");
            queue.CreateIfNotExists();

            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            table = tableClient.GetTableReference("urls");
            table.CreateIfNotExists();

            cpuTable = tableClient.GetTableReference("cpu");
            cpuTable.CreateIfNotExists();

            startQueue = queueClient.GetQueueReference("command");
            startQueue.CreateIfNotExists();

            lastTen = queueClient.GetQueueReference("lastten");
            lastTen.CreateIfNotExists();

            state = queueClient.GetQueueReference("state");
            state.CreateIfNotExists();

            errors = queueClient.GetQueueReference("error");
            errors.CreateIfNotExists();

            queryStats = queueClient.GetQueueReference("query");
            queryStats.CreateIfNotExists();
        }
    }
}
