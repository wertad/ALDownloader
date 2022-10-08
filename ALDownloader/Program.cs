using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Http;
using CLI_ProgressBar;
using System.Threading;
using System.Xml;
using System.Diagnostics;

namespace ALDownloader
{
    internal class Program
    {
        static List<string> links = new List<string>();
        static List<string> fileLinks = new List<string>();
        static List<string> megaLinks = new List<string>();
        static List<string> skippedFiles = new List<string>();
        static List<string> garage = new List<string>();
        static XmlDocument xmlDoc = new XmlDocument();
        static double megaSize = 0;
        static string megaClient = AppDomain.CurrentDomain.BaseDirectory + "MEGAclient.exe";
        static string megaDBFile = AppDomain.CurrentDomain.BaseDirectory + "megalink.db";

        private Process myProcess;
        private TaskCompletionSource<bool> eventHandled;

        static void Main(string[] args)
        {
            if (!(File.Exists(megaDBFile)))
                using (File.Create(megaDBFile)) ;

            //Task DelayedTask = DownloadFileAsync();

            //GetLinksFromXML();
            //SaveHtml();

            CreateLinks();

            Console.WriteLine();

            //CalculateSizes();

            LoadCarsIntoGarage();

            Task DelayedTask = DownloadFileAsync();

            Console.ReadLine();
        }

        static async Task DownloadFileAsync()
        {
            // végigmegy a kigyűjtött assettolands-en tárolt file-ok linkgyűjteményén
            foreach (var link in fileLinks)
            {
                try
                {
                    bool exit = false;
                    string fileName = Path.GetFileName(link);

                    // ellenőrzi, hogy le van-e már töltve az adott fájl, a fájlneve alapján
                    // ha igen, akkor a garage listában benne kell lennie, és az exit-et igazra állítja
                    foreach (var car in garage)
                    {
                        if (fileName == car)
                        {
                            garage.Remove(car);
                            exit = true;
                            break;
                        }
                    }

                    // ha exit igaz, akkor megy a következő linkre/fájlra
                    if (exit)
                        continue;
                    // lbl_filename.Text = fileName;
                    using (WebClient client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;

                        using (client.OpenRead(link))
                        {
                            Int64 bytesTotal = Convert.ToInt64(client.ResponseHeaders["Content-Length"]);
                            Int64 onePercent = bytesTotal / 100;
                            ProgressBar progress = new ProgressBar();

                            Console.WriteLine(fileName);
                            client.DownloadProgressChanged += (s, e) =>
                            {
                                progress.Report(Convert.ToDouble(e.BytesReceived / onePercent) / 100);
                            };

                            if (bytesTotal >= 1024 && bytesTotal < 1048576)
                                Console.Write(Convert.ToString(bytesTotal / 1024) + "KB | ");
                            else if (bytesTotal >= 1048576 && bytesTotal < 1073741824)
                                Console.Write(Convert.ToString(bytesTotal / 1024 / 1024) + "MB | ");
                            else if (bytesTotal >= 1073741824)
                                Console.Write(Convert.ToString(bytesTotal / 1024 / 1024 / 1024) + "GB | ");
                            else
                                Console.Write(Convert.ToString(bytesTotal) + " bytes | ");

                            await client.DownloadFileTaskAsync(new Uri(link), @"w:\games\Assetto Corsa mods\Cars\" + fileName);

                            Thread.Sleep(200);
                            Console.WriteLine("\n");
                            progress.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    continue;
                }
            }

            var dbLines = File.ReadLines(megaDBFile);

            foreach (var link in megaLinks)
            {
                try
                {
                    bool inGarage = false;

                    foreach (string line in dbLines)
                    {
                        if (line.Contains(link))
                        {
                            inGarage = true;
                            break;
                        }
                    }
                    if (!inGarage)
                    {
                        Program myPrintProcess = new Program();
                        await myPrintProcess.MegaCmdDownloader(link);
                        Console.WriteLine(link + " is downloaded.");
                        using (StreamWriter sw = new StreamWriter(megaDBFile, true))
                            sw.WriteLine(link);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            Console.WriteLine("Done.");
        }

        public async Task MegaCmdDownloader(string link)
        {
            eventHandled = new TaskCompletionSource<bool>();

            using (myProcess = new Process())
            {
                try
                {
                    // Start a process to print a file and raise an event when done.
                    myProcess.StartInfo.FileName = megaClient;
                    //myProcess.StartInfo.Verb = "Edit";
                    myProcess.StartInfo.Arguments = "get " + link;
                    myProcess.StartInfo.CreateNoWindow = true;
                    myProcess.EnableRaisingEvents = true;
                    myProcess.Exited += new EventHandler(myProcess_Exited);
                    myProcess.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred trying to print \"{link}\":\n{ex.Message}");
                    return;
                }

                // Wait for Exited event, but not more than 30 seconds.
                await Task.WhenAny(eventHandled.Task);
            }
        }

        private void myProcess_Exited(object sender, System.EventArgs e)
        {
            Console.WriteLine(
                $"Exit time    : {myProcess.ExitTime}\n" +
                $"Exit code    : {myProcess.ExitCode}\n" +
                $"Elapsed time : {Math.Round((myProcess.ExitTime - myProcess.StartTime).TotalMilliseconds)}");
            eventHandled.TrySetResult(true);
        }

        static void GetLinksFromXML()
        {
            xmlDoc.Load(@"D:\tmp\wp-sitemap-posts-page-1.xml");

            foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
            {
                string text = node.InnerText; //or loop through its children as well
                Console.WriteLine(text);
                if (text != "http://assettoland.net/cars/" && text != @"http://assettoland.net/about/" && text != @"http://assettoland.net/contact/" && text != @"http://assettoland.net/")
                    links.Add(text);
            }
        }

        static void SaveHtml()
        {
            WebClient client = new WebClient();
            foreach (var link in links)
            {
                client.Encoding = Encoding.UTF8;
                string html = client.DownloadString(link);
                string fileName = link.Split('/')[3];
                File.WriteAllText(@"D:\tmp\scraper\" + fileName + ".html", html);
                Console.WriteLine(link + " is downloaded.");
            }
        }

        static void CreateLinks()
        {
            // TODO: át kéne írni ezt a scraper mappás elérést máshova
            DirectoryInfo d = new DirectoryInfo(@"d:\tmp\scraper\AL_CARS\");
            FileInfo[] files = d.GetFiles();

            foreach (var file in files)
            {
                GetLinks(file.FullName);
            }
        }

        static void LoadCarsIntoGarage()
        {
            DirectoryInfo d = new DirectoryInfo(@"w:\games\Assetto Corsa mods\Cars\");
            FileInfo[] files = d.GetFiles();

            foreach (var file in files)
            {
                garage.Add(file.Name);
            }
        }

        static bool garageInspection(string car)
        {
            return true;
        }

        static void CalculateSizes()
        {

            /*DirectoryInfo d = new DirectoryInfo(@"d:\tmp\scraper\AL_CARS\");
            FileInfo[] files = d.GetFiles();

            foreach (var file in files)
            {
                GetLinks(file.FullName);
            }*/

            Int64 bytesTotal = 0;

            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;

                foreach (var file in fileLinks)
                {

                    try
                    {
                        using (client.OpenRead(file))
                        {
                            Console.WriteLine(file);
                            bytesTotal += Convert.ToInt64(client.ResponseHeaders["Content-Length"]);
                        }
                    }
                    catch (WebException)
                    { 
                        skippedFiles.Add(file);
                        continue;
                    }
                }

                Console.WriteLine("Megafiles Total Sizes: {0} GB", Convert.ToString(megaSize / 1024));
            }

            Console.WriteLine(Convert.ToString(bytesTotal / 1024 / 1024 / 1024) + "GB");

            /*using (WebClient client = new WebClient())
            {
                using (client.OpenRead(link))
                {
                }
            }*/
        }

        static void GetLinks(string htmlFile)
        {
            foreach (string line in File.ReadLines(htmlFile))
            {   
                if ((line.IndexOf("mega.nz/") != -1))
                {
                    int indexOfStart = line.IndexOf("https://mega.nz/");
                    int indexOfEnd = line.IndexOf("\"", indexOfStart);
                    string link = line.Substring(indexOfStart, indexOfEnd - indexOfStart);
                    megaLinks.Add(link);
                    
                    /* Összeadja a megalinkes file-ok méreteit

                    indexOfEnd = line.IndexOf("MB)");
                    indexOfStart = line.IndexOf("Download (");

                    string sizeStr = line.Substring(indexOfStart + 10, indexOfEnd - indexOfStart - 10);
                    megaSize += Convert.ToDouble(sizeStr, System.Globalization.CultureInfo.InvariantCulture);*/
                }
                else if (line.LastIndexOf(".rar") != -1)
                {
                    LinksByExtension(line, ".rar", 4);
                }
                else if (line.LastIndexOf(".7z") != -1)
                {
                    LinksByExtension(line, ".7z", 3);
                }
                else
                    continue;
            }
        }

        static void LinksByExtension(string line, string extension, int extLength)
        {
            int indexOfStart;
            int indexOfEnd = line.LastIndexOf(extension) + extLength;

            if (indexOfEnd >= 300)
                indexOfStart = line.IndexOf("http://assettoland.net/", indexOfEnd - 300);
            else
                return;

            string link = line.Substring(indexOfStart, indexOfEnd - indexOfStart).Replace("&amp;", "&");
            /*if (name.Contains("Ã©"))
                name = name.Replace("Ã©", "é");
            if (name.Contains("Ã«"))
                name = name.Replace("Ã«", "ë");
            if (name.Contains("&#039;"))
                name = name.Replace("&#039;", "'");
            if (name.Contains("â€Ž"))
                return;*/

            if (link.Contains("http://assettoland.net/mods"))
                return;

            fileLinks.Add(link);

            Console.WriteLine(link); //debug
        }
    }
}
