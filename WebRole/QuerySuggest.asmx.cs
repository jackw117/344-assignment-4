using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using ClassLibrary1;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.WindowsAzure.Storage;
using System.Configuration;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Diagnostics;
using System.Web.Script.Services;
using System.Web.Script.Serialization;
using Microsoft.WindowsAzure.Storage.Queue;

namespace WebRole1
{
    /// <summary>
    /// Summary description for WebService2
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [ScriptService]
    public class WebService2 : System.Web.Services.WebService
    {

        string originalPath = @"C:\Users\Jack\Downloads\enwiki-20160407-all-titles-in-ns0";
        string outputTest = @"C:\Users\Jack\Downloads\outputTest.txt";
        string outputPath = @"C:\Users\Jack\Downloads\output.txt";
        string outputPathNoUnderscore = @"C:\Users\Jack\Downloads\outputNoUnderscore.txt";

        string path = System.IO.Path.GetTempPath() + "\\outputNoUnderscore.txt";

        public static Trie allWords = new Trie();

        [WebMethod]
        public void onlyAZ()
        {
            using (StreamReader sr = new StreamReader(originalPath))
            {
                using (StreamWriter sw = new StreamWriter(outputPath))
                {
                    while (sr.EndOfStream == false)
                    {
                        string line = sr.ReadLine();
                        if (Regex.IsMatch(line, @"^[a-zA-Z_]+$"))
                        {
                            sw.WriteLine(line);
                        }
                    }
                }
            }
        }

        [WebMethod]
        public void downloadWiki()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("assignment4blob");

            CloudBlockBlob blockBlob = container.GetBlockBlobReference("outputNoUnderscore.txt");
            blockBlob.DownloadToFile(path, FileMode.Create);
        }

        [WebMethod]
        public void replaceUnderscore()
        {
            using (StreamReader sr = new StreamReader(outputPath))
            {
                using (StreamWriter sw = new StreamWriter(outputPathNoUnderscore))
                {
                    while (sr.EndOfStream == false)
                    {
                        string line = sr.ReadLine();
                        line = line.Replace('_', ' ');
                        sw.WriteLine(line);
                    }
                }
            }
        }

        [WebMethod]
        public string addToTrie()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue queryStats = queueClient.GetQueueReference("query");
            queryStats.CreateIfNotExists();

            string line = "";
            int check = 0;
            using (StreamReader sr = new StreamReader(path))
            {
                Process currentProcess = Process.GetCurrentProcess();
                long mem = GC.GetTotalMemory(true);
                while (sr.EndOfStream == false)
                {
                    line = sr.ReadLine();
                    if (check % 10000 == 0)
                    {
                        currentProcess = Process.GetCurrentProcess();
                        mem = GC.GetTotalMemory(false);
                        Debug.Write(mem);
                        Debug.Write(line);
                        if (mem > 903741824)
                        {
                            break;
                        }
                    }
                    allWords.addWord(line);
                    check++;
                }
            }
            CloudQueueMessage getMessage = queryStats.GetMessage();
            if (getMessage != null)
            {
                queryStats.DeleteMessage(getMessage);
            }
            queryStats.AddMessage(new CloudQueueMessage("The trie has " + check + " titles, and the last one added was " + line));
            return check + " " + line;
        }
        
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<string> searchTrie(string search)
        {
            List<string> results = allWords.checkWord(search);
            return results;
        }

        [WebMethod]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Json)]
        public void getPlayer(string callback, string player)
        {
            string jsonData = (new JavaScriptSerializer()).Serialize(player);
            string results = callback + "(" + jsonData + ");";

            Context.Response.Clear();
            Context.Response.ContentType = "application/json";
            Context.Response.Write(results);
            Context.Response.End();
        }
    }
}
