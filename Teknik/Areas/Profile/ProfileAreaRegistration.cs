﻿using System.Web.Mvc;

namespace Teknik.Areas.Profile
{
    public class ProfileAreaRegistration : AreaRegistration 
    {
        public override string AreaName 
        {
            get 
            {
                return "Profile";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapSubdomainRoute(
                 "Profile_dev", // Route name
                 "dev",
                 "Profile/{controller}/{action}",    // URL with parameters 
                 new { controller = "Profile", action = "Index" },  // Parameter defaults 
                 new[] { typeof(Controllers.ProfileController).Namespace }
             );
            context.MapSubdomainRoute(
                 "Profile_default", // Route name
                 "profile",
                 "{controller}/{action}",    // URL with parameters 
                 new { controller = "Profile", action = "Index" },  // Parameter defaults 
                 new[] { typeof(Controllers.ProfileController).Namespace }
            );
        }
    }
}