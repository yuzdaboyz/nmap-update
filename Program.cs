using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace nmap_update
{
    internal class Program
    {
        public static string Appdir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        public class Versions
        {
            public string Id { get; set; }
            public string Version { get; set; }
            public string update { get; set; }
        }
       
        static async Task Main(string[] args)
        {
            Version nver;
            string ntype, dst = "";
            List<string> fList = new List<string>();
            string url = "https://nmap.org/download.html";
            Versions rx = new Versions();
            List<Versions> lv = new List<Versions>();

            Console.WriteLine("Checking for update...");
            var httpClient = new HttpClient();
            string content = await httpClient.GetStringAsync(url);
            Regex linkRegex = new Regex("<a.*?href=\"(?<url>.*?)\".*?>(?<text>.*exe)</a>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            MatchCollection linkMatches = linkRegex.Matches(content);
            foreach (Match match in linkMatches)
            {
                url = match.Groups["url"].Value;
                dst = Path.GetTempPath() + Path.GetFileName(url);
                string[] vx = Path.GetFileName(url).Split('-');
                ntype = vx[0].ToString();
                string nx = vx[1].ToString().Replace(".exe", "");
                nver = new Version(nx);
                if (ShouldUpdate(ntype,nver))
                {

                    fList.Add(dst);
                    rx = new Versions();
                    rx.Id = ntype;
                    rx.Version = nver.ToString();
                    rx.update = "1";
                    lv.Add(rx);

                    using (var client = new HttpClientDownloadWithProgress(url, dst))
                    {
                        client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                        {
                            Console.WriteLine($"{progressPercentage}%");
                        };
                        await client.StartDownload();
                    }
                    Console.WriteLine("Download successful : " + url);
                }
            }
            //run install
            foreach(string f in fList) { 
                Console.WriteLine("Installing " + f);
                Process p = new Process();
                p.StartInfo.FileName = f;
                p.Start();
                p.WaitForExit();
                File.Delete(f);
            }
            //write update version
            if (fList.Count > 0) { updateXML2(lv); }
            Console.WriteLine("Update completed!");
        }
        static bool updateXML2(List<Versions> versions)
        {
            var elements = versions.Select(v =>
                new XElement("installed",
                    new XElement("Id", v.Id),
                    new XElement("Version", v.Version),
                    new XElement("update", v.update)));

            var root = new XElement("versions", elements);
            root.Save(Appdir + "\\nmap.xml");
            return true;
        }
        internal static bool ShouldUpdate(string ntype, Version ver)
        {
            //if not exist, create a new 1
            if (!File.Exists(Appdir + "\\nmap.xml")) {
                var installedElement =
                    new XElement("versions",
                        new XElement("installed",
                            new XElement("Id", "nmap"),
                            new XElement("Version", "0.0"),
                            new XElement("update", "1")),
                        new XElement("installed",
                            new XElement("Id", "npcap"),
                            new XElement("Version", "0.0"),
                            new XElement("update", "1")));
                var xdoc = new XDocument(installedElement);
                xdoc.Save(Appdir + "\\nmap.xml");
            }

             var element = XElement.Load(Appdir + "\\nmap.xml").Elements("installed")
                               .FirstOrDefault(e => e.Element("Id").Value == ntype && e.Element("update").Value == "1");

            if (element != null)
            {
                var versionString = element.Element("Version").Value;
                var installedVersion = new Version(versionString);

                if (installedVersion.CompareTo(ver) < 0)
                {
                    Console.WriteLine(ntype + " Update found! Current : " + installedVersion + " Update : " + ver);
                    return true;
                }
                else
                {
                    Console.WriteLine("Your " + ntype + " version is up to date!");
                    return false;
                }
            }

            return false;
        }

    }
}