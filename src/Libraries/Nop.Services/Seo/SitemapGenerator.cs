using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Forums;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.News;
using Nop.Core.Domain.Security;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Topics;

namespace Nop.Services.Seo
{
    /// <summary>
    /// Represents a sitemap generator
    /// </summary>
    public partial class SitemapGenerator : ISitemapGenerator
    {
        #region Fields

        private readonly BlogSettings _blogSettings;
        private readonly CommonSettings _commonSettings;
        private readonly ForumSettings _forumSettings;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ICategoryService _categoryService;
        private readonly ILanguageService _languageService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IProductService _productService;
        private readonly IProductTagService _productTagService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly ITopicService _topicService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IUrlRecordService _urlRecordService;
        private readonly IWebHelper _webHelper;
        private readonly NewsSettings _newsSettings;
        private readonly SecuritySettings _securitySettings;

        #endregion

        #region Ctor

        public SitemapGenerator(BlogSettings blogSettings,
            CommonSettings commonSettings,
            ForumSettings forumSettings,
            IActionContextAccessor actionContextAccessor,
            ICategoryService categoryService,
            ILanguageService languageService,
            IManufacturerService manufacturerService,
            IProductService productService,
            IProductTagService productTagService,
            ISettingService settingService,
            IStoreContext storeContext,
            ITopicService topicService,
            IUrlHelperFactory urlHelperFactory,
            IUrlRecordService urlRecordService,
            IWebHelper webHelper,
            NewsSettings newsSettings,
            SecuritySettings securitySettings)
        {
            this._blogSettings = blogSettings;
            this._commonSettings = commonSettings;
            this._forumSettings = forumSettings;
            this._actionContextAccessor = actionContextAccessor;
            this._categoryService = categoryService;
            this._languageService = languageService;
            this._manufacturerService = manufacturerService;
            this._productService = productService;
            this._productTagService = productTagService;
            this._settingService = settingService;
            this._storeContext = storeContext;
            this._topicService = topicService;
            this._urlHelperFactory = urlHelperFactory;
            this._urlRecordService = urlRecordService;
            this._webHelper = webHelper;
            this._newsSettings = newsSettings;
            this._securitySettings = securitySettings;
        }

        #endregion

        #region Nested class

        /// <summary>
        /// Represents sitemap URL entry
        /// </summary>
        protected class SitemapUrl
        {
            /// <summary>
            /// Ctor
            /// </summary>
            /// <param name="location">URL of the page</param>
            /// <param name="frequency">Update frequency</param>
            /// <param name="updatedOn">Updated on</param>
            public SitemapUrl(string location, UpdateFrequency frequency, DateTime updatedOn)
            {
                Location = location;
                UpdateFrequency = frequency;
                UpdatedOn = updatedOn;
            }

            /// <summary>
            /// Gets or sets URL of the page
            /// </summary>
            public string Location { get; set; }

            /// <summary>
            /// Gets or sets a value indicating how frequently the page is likely to change
            /// </summary>
            public UpdateFrequency UpdateFrequency { get; set; }

            /// <summary>
            /// Gets or sets the date of last modification of the file
            /// </summary>
            public DateTime UpdatedOn { get; set; }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Get UrlHelper
        /// </summary>
        /// <returns>UrlHelper</returns>
        protected virtual IUrlHelper GetUrlHelper()
        {
            return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
        }

        /// <summary>
        /// Get HTTP protocol
        /// </summary>
        /// <returns>Protocol name as string</returns>
        protected virtual string GetHttpProtocol()
        {
            return _securitySettings.ForceSslForAllPages ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        }

        /// <summary>
        /// Generate all localized URLs for the sitemap
        /// </summary>
        /// <returns>List of localized urls lists</returns>
        protected virtual IList<List<SitemapUrl>> GenerateAllLanguagePagesUrlList()
        {
            var storeId = _storeContext.ActiveStoreScopeConfiguration;
            var localizationSettings = _settingService.LoadSetting<LocalizationSettings>(storeId);

            if (localizationSettings.SeoFriendlyUrlsForLanguagesEnabled)
            {
                //Get lists of localized urls
                var urls = _languageService
                    .GetAllLanguages()
                    .SelectMany(lang => GenerateUrls(lang)
                                        .Select((url, index) => new { url = url, index = index }))
                    .GroupBy(item => item.index)
                    .Select(group => group
                                        .Select(item => item.url)
                                        .ToList())
                    .ToList();

                return urls;
            }
            else
            {
                //Get urls without seo code
                var urls = GenerateUrls()
                    .Select(url => new List<SitemapUrl> { url })
                    .ToList();
                return urls;
            }
        }

        /// <summary>
        /// Generate URLs for the sitemap
        /// </summary>
        /// <param name="lang">Language</param>
        /// <returns>List of URL for the sitemap</returns>
        protected virtual IList<SitemapUrl> GenerateUrls(Language lang = null)
        {
            var sitemapUrls = new List<SitemapUrl>();

            var urlHelper = GetUrlHelper();
            //home page
            var homePageUrl = urlHelper.RouteUrl("HomePage", null, GetHttpProtocol());
            sitemapUrls.Add(new SitemapUrl(homePageUrl, UpdateFrequency.Weekly, DateTime.UtcNow));

            //search products
            var productSearchUrl = urlHelper.RouteUrl("ProductSearch", null, GetHttpProtocol());
            sitemapUrls.Add(new SitemapUrl(productSearchUrl, UpdateFrequency.Weekly, DateTime.UtcNow));

            //contact us
            var contactUsUrl = urlHelper.RouteUrl("ContactUs", null, GetHttpProtocol());
            sitemapUrls.Add(new SitemapUrl(contactUsUrl, UpdateFrequency.Weekly, DateTime.UtcNow));

            //news
            if (_newsSettings.Enabled)
            {
                var url = urlHelper.RouteUrl("NewsArchive", null, GetHttpProtocol());
                sitemapUrls.Add(new SitemapUrl(url, UpdateFrequency.Weekly, DateTime.UtcNow));
            }

            //blog
            if (_blogSettings.Enabled)
            {
                var url = urlHelper.RouteUrl("Blog", null, GetHttpProtocol());
                sitemapUrls.Add(new SitemapUrl(url, UpdateFrequency.Weekly, DateTime.UtcNow));
            }

            //blog
            if (_forumSettings.ForumsEnabled)
            {
                var url = urlHelper.RouteUrl("Boards", null, GetHttpProtocol());
                sitemapUrls.Add(new SitemapUrl(url, UpdateFrequency.Weekly, DateTime.UtcNow));
            }

            //categories
            if (_commonSettings.SitemapIncludeCategories)
                sitemapUrls.AddRange(GetCategoryUrls(lang?.Id));

            //manufacturers
            if (_commonSettings.SitemapIncludeManufacturers)
                sitemapUrls.AddRange(GetManufacturerUrls(lang?.Id));

            //products
            if (_commonSettings.SitemapIncludeProducts)
                sitemapUrls.AddRange(GetProductUrls(lang?.Id));

            //product tags
            if (_commonSettings.SitemapIncludeProductTags)
                sitemapUrls.AddRange(GetProductTagUrls(lang?.Id));

            //topics
            sitemapUrls.AddRange(GetTopicUrls(lang?.Id));
            
            //custom urls have no default seo prefix
            if (lang != null)
            {
                sitemapUrls
                    .ForEach(url => url.Location = url.Location.ReplaceSeoCodeInUrl(urlHelper.ActionContext.HttpContext.Request.PathBase, false, lang));
            }

            //custom URLs
            sitemapUrls.AddRange(GetCustomUrls());

            return sitemapUrls;
        }

        /// <summary>
        /// Get category URLs for the sitemap
        /// </summary>
        /// <param name="languageId">Language</param>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetCategoryUrls(int? languageId = null)
        {
            var urlHelper = GetUrlHelper();

            return _categoryService.GetAllCategories(storeId: _storeContext.CurrentStore.Id).Select(category =>
            {
                var url = urlHelper.RouteUrl("Category", new { SeName = _urlRecordService.GetSeName(category, languageId) }, GetHttpProtocol());
                return new SitemapUrl(url, UpdateFrequency.Weekly, category.UpdatedOnUtc);
            });
        }

        /// <summary>
        /// Get manufacturer URLs for the sitemap
        /// </summary>
        /// <param name="languageId">Language</param>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetManufacturerUrls(int? languageId = null)
        {
            var urlHelper = GetUrlHelper();
            return _manufacturerService.GetAllManufacturers(storeId: _storeContext.CurrentStore.Id).Select(manufacturer =>
            {
                var url = urlHelper.RouteUrl("Manufacturer", new { SeName = _urlRecordService.GetSeName(manufacturer, languageId) }, GetHttpProtocol());
                return new SitemapUrl(url, UpdateFrequency.Weekly, manufacturer.UpdatedOnUtc);
            });
        }

        /// <summary>
        /// Get product URLs for the sitemap
        /// </summary>
        /// <param name="languageId">Language</param>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetProductUrls(int? languageId = null)
        {
            var urlHelper = GetUrlHelper();
            return _productService.SearchProducts(storeId: _storeContext.CurrentStore.Id,
                visibleIndividuallyOnly: true, orderBy: ProductSortingEnum.CreatedOn).Select(product =>
                {
                    var url = urlHelper.RouteUrl("Product", new { SeName = _urlRecordService.GetSeName(product, languageId) }, GetHttpProtocol());
                    return new SitemapUrl(url, UpdateFrequency.Weekly, product.UpdatedOnUtc);
                });
        }

        /// <summary>
        /// Get product tag URLs for the sitemap
        /// </summary>
        /// <param name="languageId">Language</param>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetProductTagUrls(int? languageId = null)
        {
            var urlHelper = GetUrlHelper();
            return _productTagService.GetAllProductTags().Select(productTag =>
            {
                var url = urlHelper.RouteUrl("ProductsByTag", new { SeName = _urlRecordService.GetSeName(productTag, languageId) }, GetHttpProtocol());
                return new SitemapUrl(url, UpdateFrequency.Weekly, DateTime.UtcNow);
            });
        }

        /// <summary>
        /// Get topic URLs for the sitemap
        /// </summary>
        /// <param name="languageId">Language</param>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetTopicUrls(int? languageId = null)
        {
            var urlHelper = GetUrlHelper();
            return _topicService.GetAllTopics(_storeContext.CurrentStore.Id).Where(t => t.IncludeInSitemap).Select(topic =>
            {
                var url = urlHelper.RouteUrl("Topic", new { SeName = _urlRecordService.GetSeName(topic, languageId) }, GetHttpProtocol());
                return new SitemapUrl(url, UpdateFrequency.Weekly, DateTime.UtcNow);
            });
        }

        /// <summary>
        /// Get custom URLs for the sitemap
        /// </summary>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetCustomUrls()
        {
            var storeLocation = _webHelper.GetStoreLocation();

            return _commonSettings.SitemapCustomUrls.Select(customUrl =>
                new SitemapUrl(string.Concat(storeLocation, customUrl), UpdateFrequency.Weekly, DateTime.UtcNow));
        }

        /// <summary>
        /// Write sitemap index file into the stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="sitemapNumber">The number of sitemaps</param>
        protected virtual void WriteSitemapIndex(Stream stream, int sitemapNumber)
        {
            var urlHelper = GetUrlHelper();

            using (var writer = new XmlTextWriter(stream, Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                writer.WriteStartDocument();
                writer.WriteStartElement("sitemapindex");
                writer.WriteAttributeString("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9");
                writer.WriteAttributeString("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xmlns:xhtml", "http://www.w3.org/1999/xhtml");
                writer.WriteAttributeString("xsi:schemaLocation", "http://www.sitemaps.org/schemas/sitemap/0.9 http://www.sitemaps.org/schemas/sitemap/0.9/sitemap.xsd");

                //write URLs of all available sitemaps
                for (var id = 1; id <= sitemapNumber; id++)
                {
                    var url = urlHelper.RouteUrl("sitemap-indexed.xml", new { Id = id }, GetHttpProtocol());
                    var location = XmlHelper.XmlEncode(url);

                    writer.WriteStartElement("sitemap");
                    writer.WriteElementString("loc", location);
                    writer.WriteElementString("lastmod", DateTime.UtcNow.ToString(NopSeoDefaults.SitemapDateFormat));
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }
        }

        /// <summary>
        /// Write sitemap file into the stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="sitemapUrls">List of sitemap URLs</param>
        protected virtual void WriteSitemap(Stream stream, IList<List<SitemapUrl>> sitemapUrls)
        {
            using (var writer = new XmlTextWriter(stream, Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                writer.WriteStartDocument();
                writer.WriteStartElement("urlset");
                writer.WriteAttributeString("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9");
                writer.WriteAttributeString("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xmlns:xhtml", "http://www.w3.org/1999/xhtml");
                writer.WriteAttributeString("xsi:schemaLocation", "http://www.sitemaps.org/schemas/sitemap/0.9 http://www.sitemaps.org/schemas/sitemap/0.9/sitemap.xsd");

                //write URLs from list to the sitemap
                foreach (var localizedUrls in sitemapUrls)
                {
                    //custom urls have no seo prefix and must not duplicate
                    var urls = !localizedUrls.All(u => u.Location == localizedUrls.First().Location)
                        ? localizedUrls
                        : new List<SitemapUrl> { localizedUrls.First() };

                    foreach (var url in urls)
                    {
                        writer.WriteStartElement("url");

                        var loc = XmlHelper.XmlEncode(url.Location);
                        writer.WriteElementString("loc", loc);

                        //Write all languages with current language
                        if (urls.Count > 1)
                        {
                            foreach (var alternate in urls)
                            {
                                var altLoc = XmlHelper.XmlEncode(alternate.Location);
                                var altLocPath = new Uri(XmlHelper.XmlEncode(altLoc)).PathAndQuery.ToString();
                                altLocPath.IsLocalizedUrl(_actionContextAccessor.ActionContext.HttpContext.Request.PathBase, true, out Language lang);

                                if (string.IsNullOrEmpty(lang?.UniqueSeoCode))
                                    continue;

                                writer.WriteStartElement("xhtml:link");
                                writer.WriteAttributeString("rel", "alternate");
                                writer.WriteAttributeString("hreflang", lang.UniqueSeoCode);
                                writer.WriteAttributeString("href", altLoc);
                                writer.WriteEndElement();
                            }
                        }

                        writer.WriteElementString("changefreq", urls.First().UpdateFrequency.ToString().ToLowerInvariant());
                        writer.WriteElementString("lastmod", urls.First().UpdatedOn.ToString(NopSeoDefaults.SitemapDateFormat, CultureInfo.InvariantCulture));
                        writer.WriteEndElement();

                    }
                }

                writer.WriteEndElement();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// This will build an XML sitemap for better index with search engines.
        /// See http://en.wikipedia.org/wiki/Sitemaps for more information.
        /// </summary>
        /// <param name="id">Sitemap identifier</param>
        /// <returns>Sitemap.xml as string</returns>
        public virtual string Generate(int? id)
        {
            using (var stream = new MemoryStream())
            {
                Generate(stream, id);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// This will build an XML sitemap for better index with search engines.
        /// See http://en.wikipedia.org/wiki/Sitemaps for more information.
        /// </summary>
        /// <param name="id">Sitemap identifier</param>
        /// <param name="stream">Stream of sitemap.</param>
        public virtual void Generate(Stream stream, int? id)
        {
            //generate all URLs for the sitemap
            var sitemapUrls = GenerateAllLanguagePagesUrlList();

            //split URLs into separate lists based on the max size 
            var sitemaps = sitemapUrls
                .Select((url, index) => new { Index = index, Value = url })
                .GroupBy(group => group.Index / NopSeoDefaults.SitemapMaxUrlNumber)
                .Select(group => group
                    .Select(url => url.Value)
                    .ToList()
                ).ToList();

            if (!sitemaps.Any())
                return;

            if (id.HasValue)
            {
                //requested sitemap does not exist
                if (id.Value == 0 || id.Value > sitemaps.Count)
                    return;

                //otherwise write a certain numbered sitemap file into the stream
                WriteSitemap(stream, sitemaps.ElementAt(id.Value - 1));
            }
            else
            {
                //URLs more than the maximum allowable, so generate a sitemap index file
                if (sitemapUrls.Count >= NopSeoDefaults.SitemapMaxUrlNumber)
                {
                    //write a sitemap index file into the stream
                    WriteSitemapIndex(stream, sitemaps.Count);
                }
                else
                {
                    //otherwise generate a standard sitemap
                    WriteSitemap(stream, sitemaps.First());
                }
            }
        }

        #endregion
    }
}