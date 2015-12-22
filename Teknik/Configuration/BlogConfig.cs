﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Teknik.Configuration
{
    public class BlogConfig
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public int PostsToLoad { get; set; }
        public int CommentsToLoad { get; set; }

        public BlogConfig()
        {
            SetDefaults();
        }

        public void SetDefaults()
        {
            Title = string.Empty;
            Description = string.Empty;
            PostsToLoad = 10;
            CommentsToLoad = 10;
        }
    }
}