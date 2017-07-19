using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace ClassLibrary1
{
    public class Page : TableEntity
    {
        public string urlName { get; set; }
        public string title { get; set; }
        public string date { get; set; }
        public string word { get; set; }

        public Page() { }

        public Page(string url, string title, string date, string word)
        {
            this.urlName = url;
            this.title = title;
            this.date = date;
            this.word = word;
            
            byte[] encodedURL = new UTF8Encoding().GetBytes(url);
            byte[] hash = ((HashAlgorithm)CryptoConfig.CreateFromName("MD5")).ComputeHash(encodedURL);

            this.PartitionKey = word;
            this.RowKey = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
        }
    }
}
