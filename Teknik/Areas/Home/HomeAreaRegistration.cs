﻿using System.Collections.Generic;
using System.Web.Mvc;
using System.Web.Optimization;
using Teknik;
using Teknik.Configuration;
using Teknik.Controllers;
using Teknik.Helpers;

namespace Teknik.Areas.Home
{
    public class HomeAreaRegistration : AreaRegistration 
    {
        public override string AreaName 
        {
            get 
            {
                return "Home";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            Config config = Config.Load();

            // Default Routes to be applied everywhere
            context.MapSubdomainRoute(
                 "Default.Favicon", // Route name
                 new List<string>() { "*" }, // Subdomains
                 new List<string>() { config.Host, config.ShortenerConfig.ShortenerHost }, // domains
                 "favicon.ico",    // URL with parameters 
                 new { controller = "Default", action = "Favicon" },  // Parameter defaults 
                 new[] { typeof(DefaultController).Namespace }
             );

            // Default Routes to be applied everywhere
            context.MapSubdomainRoute(
                 "Default.Logo", // Route name
                 new List<string>() { "*" }, // Subdomains
                 new List<string>() { config.Host, config.ShortenerConfig.ShortenerHost }, // domains
                 "Logo",    // URL with parameters 
                 new { controller = "Default", action = "Logo" },  // Parameter defaults 
                 new[] { typeof(DefaultController).Namespace }
             );

            context.MapSubdomainRoute(
                 "Home.Index", // Route name
                 new List<string>() { "www", string.Empty }, // Subdomains
                 new List<string>() { config.Host }, // domains
                 "",    // URL with parameters 
                 new { controller = "Home", action = "Index" },  // Parameter defaults 
                 new[] { typeof(Controllers.HomeController).Namespace }
             );

            // Register Style Bundles
            BundleTable.Bundles.Add(new CdnStyleBundle("~/Content/home", config.CdnHost).Include(
                      "~/Areas/Home/Content/Home.css"));

            // Register Script Bundles
            BundleTable.Bundles.Add(new CdnScriptBundle("~/bundles/home", config.CdnHost).Include(
                      "~/Scripts/PageDown/Markdown.Converter.js",
                      "~/Scripts/PageDown/Markdown.Sanitizer.js"));
        }
    }
}