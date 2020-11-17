using HtmlAgilityPack;
using Newtonsoft.Json;
using OpenQA.Selenium.Support;
using OpenQA.Selenium.PhantomJS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using OpenQA.Selenium;
using System.Net;

namespace Upwork.DiegoFerreira.Walmart
{
    public class Product
    {
        public string Title { get; set; }
        public string Brand { get; set; }
        public string Price { get; set; }
        public string WalmartNumber { get; set; }
        public string UPC { get; set; }
        public string URL { get; set; }
    }

    public class Settings
    {
        public List<string> urls { get; set; } = new List<string>();
        public bool headless { get; set; }
        public int threads { get; set; }
    }
    class Program
    {
        static List<Product> products = new List<Product>();
        static Settings settings = new Settings();
        static void Main(string[] args)
        {
            try
            {
                settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("settings.json"));
                HtmlWeb web = new HtmlWeb();
                //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                //var docx = web.Load(settings.urls.FirstOrDefault());
                if (ValidateSettings(settings))
                {
                    foreach (var url in settings.urls)
                    {
                        List<string> productUrls = new List<string>();

                        var service = PhantomJSDriverService.CreateDefaultService();
                        service.HideCommandPromptWindow = true;
                        service.IgnoreSslErrors = true;
                        service.LoadImages = false;

                        using (var driver = new PhantomJSDriver(service))
                        {
                            //code

                            //Let load the url
                            Console.WriteLine("Loading \"{0}\"", url);
                            driver.Navigate().GoToUrl(url);

                            ////Make sure page is loaded completely
                            Thread.Sleep(3000);

                            //Now let find out the listing
                            int pages = 1;
                            HtmlDocument doc = new HtmlDocument();
                            doc.LoadHtml(driver.PageSource);

                            var paginationNode = doc.DocumentNode.SelectSingleNode("//ul[@class='paginator-list']");
                            if (paginationNode != null)
                            {
                                var list = paginationNode.ChildNodes.Where(x => x.Name == "li");
                                if (list.Count() > 0)
                                {
                                    pages = Convert.ToInt32(list.LastOrDefault().InnerText);
                                }
                            }
                            else
                                Console.WriteLine("No pagination");

                            //get all product urls
                            //get urls for first page
                            ExtractProductLinks(doc, productUrls);

                            //get urls for rest of the pages
                            for (int i = 2; i <= pages; i++)
                            {
                                try
                                {
                                    Console.WriteLine("Loading \"{0}\"", url + "&page=" + i);
                                    driver.Navigate().GoToUrl(url + "&page=" + i);
                                    doc.LoadHtml(driver.PageSource);
                                    ExtractProductLinks(doc, productUrls);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Exception: Unable to load page. Reason: {0}, URL:{1}", ex.Message, url + "&page=" + i);
                                }
                            }
                            Console.WriteLine("Total Products URLs: " + productUrls.Count);


                            driver.Close();
                            driver.Quit();
                        }

                        //Now get products details
                        int pages1 = (productUrls.Count + 30 - 1) / 30;
                        List<Task> taskList = new List<Task>();
                        for (int index = 1; index <= pages1; ++index)
                        {
                            int x = index - 1;
                            Task task = Task.Factory.StartNew(() => ExtractProductDetails(productUrls.Skip(x * 30).Take(30).ToList()));
                            taskList.Add(task);
                            if (index % settings.threads == 0 || index == pages1)
                            {
                                foreach (Task task2 in taskList)
                                {
                                    while (!task2.IsCompleted)
                                    { }
                                }
                            }
                        }

                    }
                    Console.WriteLine("Exporting...");
                    ExportProducts();
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: Unable to continue, {0}", ex.Message);
            }
            Console.WriteLine("Operation Completed. Press any key to exit.");
            Console.ReadKey();
        }

        private static bool ValidateSettings(Settings settings)
        {
            if (settings.urls.Count < 1)
            {
                Console.WriteLine("Settings>>> Please provide initial URL in settings.json.");
                return false;
            }

            if (!(settings.threads > 0 && settings.threads <= 15))
            {
                Console.WriteLine("Settings>>> Threads count must be between(0-15).");
                return false;
            }


            return true;
        }

        private static void ExportProducts()
        {
            List<string> entries = new List<string>();
            entries.Add(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\"", "Brand", "Title", "Price", "WalmartNumber", "UPC", "URL"));
            foreach (var product in products)
            {
                entries.Add(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\"", product.Brand, product.Title, product.Price, product.WalmartNumber, product.UPC, product.URL));
            }

            var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var subFolderPath = Path.Combine(path, "www.walmart.com");
            Directory.CreateDirectory(subFolderPath);
            var today = DateTime.Now;
            var span = string.Format("{0}{1}{2}{3}{4}{5}.csv", today.Year, today.Month, today.Day, today.Hour, today.Minute, today.Second);
            File.WriteAllLines(Path.Combine(subFolderPath, span), entries);
        }

        public static void ExtractProductDetails(List<string> urls)
        {
            try
            {
                var service = PhantomJSDriverService.CreateDefaultService();
                service.HideCommandPromptWindow = true;
                service.LoadImages = false;

                using (var driver = new PhantomJSDriver(service))
                {
                    foreach (var url in urls)
                    {
                    task:
                        int count = 0;
                        try
                        {
                            Console.WriteLine("Loading \"{0}\"", url);
                            Product product = new Product() { URL = url };
                            driver.Navigate().GoToUrl(url);
                            Thread.Sleep(3000);

                            HtmlDocument doc = new HtmlDocument();
                            doc.LoadHtml(driver.PageSource);

                            var brandNode = doc.DocumentNode.SelectSingleNode("//*[@id=\"product-overview\"]/div/div[3]/div/a/span");
                            if (brandNode != null)
                            {
                                product.Brand = HttpUtility.HtmlDecode(brandNode.InnerText).Trim();
                            }
                            else
                                Console.WriteLine("Warning:{0}, URL:{1}", "Brand not found.", url);

                            var priceNode = doc.DocumentNode.SelectSingleNode("//*[@id=\"price\"]");
                            if (priceNode != null)
                            {
                                var sub = new HtmlDocument();
                                sub.LoadHtml(priceNode.InnerHtml);

                                var priceNodes = sub.DocumentNode.SelectNodes("//span[@class='visuallyhidden']");
                                List<string> ps = new List<string>();
                                foreach (var item in priceNodes)
                                {
                                    if (!ps.Exists(x => x == item.InnerText))
                                        ps.Add(item.InnerText);
                                }

                                product.Price = HttpUtility.HtmlDecode(string.Join("-", ps)).Trim();
                            }
                            else
                                Console.WriteLine("Warning:{0}, URL:{1}", "Price not found.", url);

                            var titleNode = doc.DocumentNode.SelectSingleNode("//*[@id=\"product-overview\"]/div/div[3]/div/h1");
                            if (titleNode != null)
                            {
                                product.Title = HttpUtility.HtmlDecode(titleNode.InnerText).Trim();
                            }
                            else
                                Console.WriteLine("Warning:{0}, URL:{1}", "Title not found.", url);

                            var wNumberNode = doc.DocumentNode.SelectSingleNode("//*[@id=\"product-overview\"]/div/div[3]/div/div[1]/div[3]");
                            if (wNumberNode != null)
                            {
                                product.WalmartNumber = HttpUtility.HtmlDecode(wNumberNode.InnerText.Replace("Walmart #  ", "")).Trim();
                            }
                            else
                                Console.WriteLine("Warning:{0}, URL:{1}", "Walmart Number not found.", url);

                            var parts = driver.PageSource.Split(new string[] { "\"upc\":" }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Count() > 1)
                            {
                                product.UPC = parts[1].Split(',')[0].Replace("\"", "");
                            }
                            else
                                Console.WriteLine("Warning:{0}, URL:{1}", "UPC not found.", url);

                            products.Add(product);
                            Console.WriteLine("Success:{0}, URL:{1}", "Product Scraped.", url);
                        }
                        catch (Exception ex)
                        {
                            count += 1;
                            Console.WriteLine("Error: {0}, Url:{1}", "Unable to fetch details of product", url);
                            if (count != 3)
                                goto task;
                        }
                    }

                    driver.Close();
                    driver.Quit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}, Reason:{1}", "Thread execution failed", ex.Message);
            }

        }


        private static void ExtractProductLinks(HtmlDocument doc, List<string> productUrls)
        {
            var links = doc.DocumentNode.SelectNodes("//a[contains(@class, 'product-title-link line-clamp line-clamp-2 truncate-title')]");
            if (links.Count > 0)
                foreach (var li in links)
                {
                    var link = "https://www.walmart.com/" + li.Attributes.FirstOrDefault(x => x.Name == "href").Value;
                    productUrls.Add(link);
                    Console.WriteLine(link);
                }
        }
    }
}
