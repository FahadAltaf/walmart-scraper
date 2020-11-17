using CsvHelper;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalmartScraper
{
    class Program
    {
        static Settings settings = new Settings();
        static List<Product> products = new List<Product>();
        static void Main(string[] args)
        {
            try
            {
                HtmlWeb web = new HtmlWeb();

                settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("settings.json"));
                if (ValidateSettings(settings))
                {
                    foreach (var url in settings.urls)
                    {
                        try
                        {
                            //Lets first fetch data from first page
                            var initialPageData = ExtractDataFromListing(web, url);
                            Console.WriteLine("Total Products: {0}", initialPageData.searchContent.preso.requestContext.itemCount.total);
                            int pages = (initialPageData.searchContent.preso.requestContext.itemCount.total + initialPageData.searchContent.preso.requestContext.itemCount.pageSize - 1) / initialPageData.searchContent.preso.requestContext.itemCount.pageSize;
                            for (int i = 2; i <= pages; i++)
                            {
                                try
                                {
                                    ExtractDataFromListing(web, url + "&page=" + i);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }
                            }

                            for (int i = 0; i < products.Count; i++)
                            {
                                try
                                {
                                    ExtractDataFromDetailsPage(web, products[i].URL, i);
                                }
                                catch (Exception ex)
                                {

                                    Console.WriteLine(ex.Message);
                                }
                            }


                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error: {0}, URL:{1}", "Unable to extract data from initial url.", url);
                        }
                    }

                    ExportProducts();
                }

                Console.WriteLine("Opration Completed. Press any key to exit.");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to continue. Reason: " + ex.Message);
            }
        }
        private static bool ValidateSettings(Settings settings)
        {
            if (settings.urls.Count < 1)
            {
                Console.WriteLine("Settings>>> Please provide initial URL in settings.json.");
                return false;
            }

            return true;
        }
        private static void ExportProducts()
        {
            Console.WriteLine("Exporting...");
            var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var subFolderPath = Path.Combine(path, "www.walmart.com");
            Directory.CreateDirectory(subFolderPath);
            var today = DateTime.Now;
            var span = string.Format("{0}{1}{2}{3}{4}{5}.csv", today.Year, today.Month, today.Day, today.Hour, today.Minute, today.Second);
            using (var writer = new StreamWriter(Path.Combine(subFolderPath, span)))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(products);
            }
            //List<string> entries = new List<string>();
            //entries.Add(string.Format("\"{0}\";\"{1}\";\"{2}\";\"{3}\";\"{4}\";\"{5}\"", "Brand", "Title", "Price", "WalmartNumber", "UPC", "URL"));
            //foreach (var product in products)
            //{
            //    entries.Add(string.Format("\"{0}\";\"{1}\";\"{2}\";\"{3}\";\"{4}\";\"{5}\"", product.Brand, product.Title, product.Price, product.WalmartNumber, product.UPC, product.URL));
            //}

            //var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            //var subFolderPath = Path.Combine(path, "www.walmart.com");
            //Directory.CreateDirectory(subFolderPath);
            //var today = DateTime.Now;
            //var span = string.Format("{0}{1}{2}{3}{4}{5}.csv", today.Year, today.Month, today.Day, today.Hour, today.Minute, today.Second);
            //File.WriteAllLines(Path.Combine(subFolderPath, span), entries);
        }
        public static void ExtractDataFromDetailsPage(HtmlWeb web, string url, int index)
        {
            Console.WriteLine("Loading \"{0}\"", url);
            var doc = web.Load(url);

            var parts = doc.DocumentNode.InnerHtml.Split(new string[] { "<script id=\"item\" class=\"tb-optimized\" type=\"application/json\">" }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                var json = parts[1].Substring(0, parts[1].IndexOf("</script>"));
                var data = JsonConvert.DeserializeObject<ProductDetails>(json);
                products[index].Title = data.item.product.buyBox.products[0].productName;
                products[index].Price = data.item.product.midasContext.Price;
                products[index].Brand = data.item.product.buyBox.products[0].brandName;
                products[index].WalmartNumber = data.item.product.buyBox.products[0].walmartItemNumber;
                products[index].UPC = data.item.product.buyBox.products[0].upc;

            }
            else
                throw new Exception(string.Format("Error: {0}, URL:{1}", "Unable to extract data from product details page", url));

        }
        public static Root ExtractDataFromListing(HtmlWeb web, string url)
        {
            Console.WriteLine("Loading \"{0}\"", url);
            var doc = web.Load(url);

            var parts = doc.DocumentNode.InnerHtml.Split(new string[] { "<script id=\"searchContent\" type=\"application/json\">" }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                var json = parts[1].Substring(0, parts[1].IndexOf("</script>"));
                var data = JsonConvert.DeserializeObject<Root>(json);
                foreach (var product in data.searchContent.preso.items)
                {
                    var p = new Product
                    {
                        URL = "https://www.walmart.com/" + product.productPageUrl,
                        UPC = product.upc
                    };
                    products.Add(p);
                }
                return data;
            }
            else
                throw new Exception(string.Format("Error: {0}, URL:{1}", "Unable to extract data from listing", url));
        }
    }

    #region Product Details
    public class Product2
    {
        public string brandName { get; set; }
        public string productName { get; set; }
        public string walmartItemNumber { get; set; }
        public string upc { get; set; }

    }

    public class BuyBox
    {
        public List<Product2> products { get; set; }

    }

    public class ProductX
    {
        public BuyBox buyBox { get; set; }
        public MidasContext midasContext { get; set; }

    }

    public class MidasContext
    {
        public string Price { get; set; }
    }

    public class ItemX
    {
        public ProductX product { get; set; }


    }

    public class ProductDetails
    {
        public ItemX item { get; set; }

    }
    #endregion

    #region Custom
    public class Settings
    {
        public List<string> urls { get; set; } = new List<string>();
        public bool headless { get; set; }
        public int threads { get; set; }
    }
    public class Product
    {
        public string Title { get; set; }
        public string Brand { get; set; }
        public string Price { get; set; }
        public string WalmartNumber { get; set; }
        public string UPC { get; set; }
        public string URL { get; set; }
    }

    #endregion

    #region Listing
    public class PrimaryOffer
    {
        public string offerId { get; set; }
        public double minPrice { get; set; }
        public double maxPrice { get; set; }
        public bool showMinMaxPrice { get; set; }
        public string unitPriceDisplayCondition { get; set; }
        public double? offerPrice { get; set; }
        public string currencyCode { get; set; }
        public double? listPrice { get; set; }
        public double? savingsAmount { get; set; }
        public bool? showWasPrice { get; set; }

    }

    public class Fulfillment
    {
        public bool isS2H { get; set; }
        public bool isS2S { get; set; }
        public bool isSOI { get; set; }
        public bool isPUT { get; set; }
        public List<string> s2SDisplayFlags { get; set; }
        public List<string> s2HDisplayFlags { get; set; }
        public int? thresholdAmount { get; set; }
        public string thresholdCurrencyCode { get; set; }

    }

    public class Inventory
    {
        public bool availableOnline { get; set; }
        public List<string> displayFlags { get; set; }

    }

    public class VariantMeta
    {
        public string name { get; set; }
        public string type { get; set; }
        public string rank { get; set; }

    }

    public class VariantValue
    {
        public string name { get; set; }
        public string value { get; set; }
        public string rank { get; set; }
        public string displayName { get; set; }

    }

    public class VariantData
    {
        public string productId { get; set; }
        public string usItemId { get; set; }
        public string title { get; set; }
        public string productImageUrl { get; set; }
        public string productPageUrl { get; set; }
        public string isAvailable { get; set; }
        public string skuCoverage { get; set; }
        public string ownedInventory { get; set; }
        public List<VariantValue> variantValues { get; set; }
        public string swatchSrcSet { get; set; }
        public string productSrcSet { get; set; }

    }

    public class Variants
    {
        public List<VariantMeta> variantMeta { get; set; }
        public List<VariantData> variantData { get; set; }

    }

    public class ImageProps
    {
        public string src { get; set; }
        public string srcSet { get; set; }

    }

    public class Ppu
    {
        public string unit { get; set; }
        public double amount { get; set; }
        public string currencyCode { get; set; }

    }

    public class Wpa
    {
        public string wpa_bd { get; set; }
        public string wpa_pg_seller_id { get; set; }
        public string wpa_ref_id { get; set; }
        public string wpa_tag { get; set; }
        public string wpa_aux_info { get; set; }
        public int wpa_pos { get; set; }

    }

    public class Item
    {
        public string productId { get; set; }
        public string productType { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string imageUrl { get; set; }
        public string productPageUrl { get; set; }
        public string upc { get; set; }
        public string department { get; set; }
        public double customerRating { get; set; }
        public int numReviews { get; set; }
        public string specialOfferBadge { get; set; }
        public string specialOfferText { get; set; }
        public string specialOfferLink { get; set; }
        public string sellerId { get; set; }
        public string sellerName { get; set; }
        public bool enableAddToCart { get; set; }
        public bool canAddToCart { get; set; }
        public List<string> cta { get; set; }
        public bool showPriceAsAvailable { get; set; }
        public string seeAllName { get; set; }
        public string seeAllLink { get; set; }
        public string itemClassId { get; set; }
        public PrimaryOffer primaryOffer { get; set; }
        public Fulfillment fulfillment { get; set; }
        public Inventory inventory { get; set; }
        public string isbn { get; set; }
        public int quantity { get; set; }
        public Variants variants { get; set; }
        public List<string> brand { get; set; }
        public string geoItemClassification { get; set; }
        public bool fsaEligible { get; set; }
        public List<string> pcsType { get; set; }
        public string wmtgPricePerUnitQuantity { get; set; }
        public List<string> pcsVolumeCapacity { get; set; }
        public List<string> standardUpc { get; set; }
        public bool isHeartable { get; set; }
        public bool preOrderAvailable { get; set; }
        public bool virtualPack { get; set; }
        public bool premiumBrand { get; set; }
        public bool wfsEnabled { get; set; }
        public bool marketPlaceItem { get; set; }
        public bool blitzItem { get; set; }
        public bool twoDayShippingEligible { get; set; }
        public bool shippingPassEligible { get; set; }
        public bool pickupDiscountEligible { get; set; }
        public bool is_limited_qty { get; set; }
        public ImageProps imageProps { get; set; }
        public bool isBluRay { get; set; }
        public bool isDvd { get; set; }
        public bool isVuduDigital { get; set; }
        public List<object> visibleSwatches { get; set; }
        public bool shouldHaveSponsoredItemMargin { get; set; }
        public bool shouldHaveSwatchesMargin { get; set; }
        public bool shouldHaveSpecialOfferMargin { get; set; }
        public string usItemId { get; set; }
        public Ppu ppu { get; set; }
        public string sourceSystem { get; set; }
        public Wpa wpa { get; set; }

    }

    public class ItemCount
    {
        public int total { get; set; }
        public int currentSize { get; set; }
        public int offset { get; set; }
        public int page { get; set; }
        public int pageSize { get; set; }

    }

    public class RequestContext
    {
        public ItemCount itemCount { get; set; }

    }

    public class Preso
    {
        public int status { get; set; }
        public List<Item> items { get; set; }
        public RequestContext requestContext { get; set; }

    }

    public class SearchContent
    {
        public Preso preso { get; set; }

    }

    public class Root
    {
        public SearchContent searchContent { get; set; }

    }

    #endregion
}
