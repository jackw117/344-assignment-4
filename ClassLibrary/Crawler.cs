using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ClassLibrary1
{
    public class Crawler
    {
        private List<string> initialSitemap = new List<string>();

        public Crawler() { }

        public List<string> getSitemap()
        {
            return initialSitemap;
        }

        public List<string> searchForSitemaps(XmlNodeList sitemaps, XmlNamespaceManager nsmgr, DateTime start)
        {
            List<string> returnedSitemaps = new List<string>();
            foreach (XmlNode map in sitemaps)
            {
                
                var urlnode = map.SelectSingleNode("sm:loc", nsmgr);
                var modNode = map.SelectSingleNode("sm:lastmod", nsmgr);
                string sitemapUrl = urlnode.InnerText;
                DateTime modDate = DateTime.Parse(modNode.InnerText);
                if (modDate > start)
                {
                    returnedSitemaps.Add(sitemapUrl);
                }
            }
            return returnedSitemaps;
        }

        public HashSet<string> createBlacklist(string url, HashSet<string> blacklist)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.UserAgent = "A .NET Web Crawler";
            WebResponse response = request.GetResponse();
            Stream stream = response.GetResponseStream();
            StreamReader reader = new StreamReader(stream);
            while (reader.EndOfStream == false)
            {
                string line = reader.ReadLine();
                if (line.StartsWith("Disallow: "))
                {
                    string block = line.Substring(10);
                    blacklist.Add(block);
                }
                else if (line.StartsWith("Sitemap: "))
                {
                    string block = line.Substring(9);
                    if (url.Contains("bleacher"))
                    {
                        if (block.Contains("nba"))
                        {
                            initialSitemap.Add(block);
                        }
                    }
                    else
                    {
                        initialSitemap.Add(block);
                    }
                }
            }
            return blacklist;
        }
    }
}
