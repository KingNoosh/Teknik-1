namespace Teknik.Migrations
{
    using Areas.Paste;
    using Areas.Upload;
    using Helpers;
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.IO;
    using System.Linq;
    using System.Net;
    using Teknik.Configuration;

    internal sealed class Configuration : DbMigrationsConfiguration<Models.TeknikEntities>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;
        }

        protected override void Seed(Models.TeknikEntities context)
        {
            Config config = Config.Load();
            // Pre-populate with the default stuff

            // Create system blog
            Areas.Profile.Models.User systemUser = new Areas.Profile.Models.User();
            systemUser.Username = Constants.SERVERUSER;
            systemUser.JoinDate = DateTime.Now;
            systemUser.LastSeen = DateTime.Now;
            systemUser.UserSettings = new Areas.Profile.Models.UserSettings();
            systemUser.BlogSettings = new Areas.Profile.Models.BlogSettings();
            systemUser.UploadSettings = new Areas.Profile.Models.UploadSettings();
            context.Users.AddOrUpdate(systemUser);
            context.SaveChanges();

            Areas.Blog.Models.Blog systemBlog = new Areas.Blog.Models.Blog();
            systemBlog.UserId = systemUser.UserId;
            systemBlog.BlogId = config.BlogConfig.ServerBlogId;
            context.Blogs.AddOrUpdate(systemBlog);
            context.SaveChanges();

            // Create roles and groups
            Areas.Profile.Models.Role adminRole = new Areas.Profile.Models.Role();
            adminRole.Name = "Admin";
            adminRole.Description = "Allows complete access to user specific actions";
            context.Roles.AddOrUpdate(adminRole);

            Areas.Profile.Models.Role podcastRole = new Areas.Profile.Models.Role();
            podcastRole.Name = "Podcast";
            podcastRole.Description = "Allows create/edit/delete access to podcasts";
            context.Roles.AddOrUpdate(podcastRole);

            Areas.Profile.Models.Group adminGroup = new Areas.Profile.Models.Group();
            adminGroup.Name = "Administrators";
            adminGroup.Description = "System Administrators with full access";
            adminGroup.Roles = new List<Areas.Profile.Models.Role>();
            adminGroup.Roles.Add(adminRole);
            adminGroup.Roles.Add(podcastRole);
            context.Groups.AddOrUpdate(adminGroup);

            Areas.Profile.Models.Group podcastGroup = new Areas.Profile.Models.Group();
            podcastGroup.Name = "Podcast";
            podcastGroup.Description = "Podcast team members";
            podcastGroup.Roles = new List<Areas.Profile.Models.Role>();
            podcastGroup.Roles.Add(podcastRole);
            context.Groups.AddOrUpdate(podcastGroup);

            Areas.Profile.Models.Group memberGroup = new Areas.Profile.Models.Group();
            memberGroup.Name = "Member";
            memberGroup.Description = "The default member group with basic permissions";
            context.Groups.AddOrUpdate(memberGroup);

            context.SaveChanges();

            if (config.DatabaseConfig.Migrate && !config.DevEnvironment)
            {
                config.DatabaseConfig.Migrate = false;
                Config.Save(config);

                // Convert legacy MySQL DB to new MS SQL DB
                MysqlDatabase db = new MysqlDatabase(config.DatabaseConfig);
                db.MysqlErrorEvent += Db_MysqlErrorEvent;

                // Transfer transactions
                var transRet = db.Query("SELECT * FROM `transactions`");
                foreach (var tran in transRet)
                {
                    switch (tran["trans_type"].ToString())
                    {
                        case "One-Time":
                            Areas.Transparency.Models.OneTime tr = new Areas.Transparency.Models.OneTime();
                            tr.DateSent = DateTime.Parse(tran["date_posted"].ToString());
                            tr.Amount = Double.Parse(tran["amount"].ToString());
                            tr.Currency = tran["currency"].ToString();
                            tr.Recipient = tran["recipient"].ToString();
                            tr.Reason = tran["reason"].ToString();
                            context.Transactions.AddOrUpdate(tr);
                            break;
                        case "Bill":
                            Areas.Transparency.Models.Bill bill = new Areas.Transparency.Models.Bill();
                            bill.DateSent = DateTime.Parse(tran["date_posted"].ToString());
                            bill.Amount = Double.Parse(tran["amount"].ToString());
                            bill.Currency = tran["currency"].ToString();
                            bill.Recipient = tran["recipient"].ToString();
                            bill.Reason = tran["reason"].ToString();
                            context.Transactions.AddOrUpdate(bill);
                            break;
                        case "Donation":
                            Areas.Transparency.Models.Donation don = new Areas.Transparency.Models.Donation();
                            don.DateSent = DateTime.Parse(tran["date_posted"].ToString());
                            don.Amount = Double.Parse(tran["amount"].ToString());
                            don.Currency = tran["currency"].ToString();
                            don.Sender = tran["sender"].ToString();
                            don.Reason = tran["reason"].ToString();
                            context.Transactions.AddOrUpdate(don);
                            break;
                    }
                }
                context.SaveChanges();

                // Transfer Users and Blogs/Posts
                Dictionary<int, int> userMapping = new Dictionary<int, int>();
                Dictionary<int, int> postMapping = new Dictionary<int, int>();
                var userRet = db.Query("SELECT * FROM `users`");
                foreach (var user in userRet)
                {
                    // Create User
                    Areas.Profile.Models.User newUser = new Areas.Profile.Models.User();
                    newUser.UserSettings = new Areas.Profile.Models.UserSettings();
                    newUser.BlogSettings = new Areas.Profile.Models.BlogSettings();
                    newUser.UploadSettings = new Areas.Profile.Models.UploadSettings();
                    newUser.TransferAccount = true;
                    newUser.Username = user["username"].ToString();
                    newUser.HashedPassword = user["password"].ToString();
                    newUser.JoinDate = DateTime.Parse(user["join_date"].ToString());
                    newUser.LastSeen = DateTime.Parse(user["join_date"].ToString());
                    newUser.UserSettings.About = user["about"].ToString();
                    newUser.UserSettings.Website = user["website"].ToString();
                    newUser.UserSettings.Quote = user["quote"].ToString();
                    newUser.BlogSettings.Title = user["blog_title"].ToString();
                    newUser.BlogSettings.Description = user["blog_desc"].ToString();
                    if (user["site_admin"].ToString() == "1")
                    {
                        newUser.Groups.Add(adminGroup);
                    }
                    context.Users.AddOrUpdate(newUser);
                    context.SaveChanges();
                    int oldUserId = Int32.Parse(user["id"].ToString());
                    int userId = newUser.UserId;

                    userMapping.Add(oldUserId, userId);

                    // Create Blog for user
                    Areas.Blog.Models.Blog newBlog = new Areas.Blog.Models.Blog();
                    newBlog.UserId = userId;
                    context.Blogs.AddOrUpdate(newBlog);
                    context.SaveChanges();
                    int blogId = newBlog.BlogId;

                    // Transfer Blog Posts
                    var postRet = db.Query("SELECT * FROM `blog` WHERE `author_id` = {0}", new object[] { oldUserId });
                    if (postRet != null)
                    {
                        foreach (var post in postRet)
                        {
                            // Create new Blog Post
                            Areas.Blog.Models.BlogPost newPost = new Areas.Blog.Models.BlogPost();
                            if (post["user_id"].ToString() == "0")
                            {
                                newPost.BlogId = config.BlogConfig.ServerBlogId;
                                newPost.System = true;
                            }
                            else
                            {
                                newPost.BlogId = blogId;
                            }
                            newPost.DatePosted = DateTime.Parse(post["date_posted"].ToString());
                            DateTime publishDate = DateTime.Now;
                            DateTime.TryParse(post["date_published"].ToString(), out publishDate);
                            if (publishDate < newPost.DatePosted)
                            {
                                publishDate = newPost.DatePosted;
                            }
                            newPost.DatePublished = publishDate;
                            newPost.DateEdited = publishDate;
                            newPost.Published = (post["published"].ToString() == "True");
                            newPost.Title = post["title"].ToString();
                            newPost.Article = post["post"].ToString();
                            context.BlogPosts.AddOrUpdate(newPost);
                            context.SaveChanges();
                            postMapping.Add(Int32.Parse(post["id"].ToString()), newPost.BlogPostId);
                        }
                    }
                }

                // Transfer Blog Comments
                var commentRet = db.Query("SELECT * FROM `comments` WHERE `service` = {0}", new object[] { "blog" });
                foreach (var comment in commentRet)
                {
                    int postId = Int32.Parse(comment["reply_id"].ToString());
                    int userId = Int32.Parse(comment["user_id"].ToString());
                    if (postMapping.ContainsKey(postId) && userMapping.ContainsKey(userId))
                    {
                        Areas.Blog.Models.BlogPostComment newComment = new Areas.Blog.Models.BlogPostComment();
                        newComment.BlogPostId = postMapping[postId];
                        newComment.UserId = userMapping[userId];
                        newComment.Article = comment["post"].ToString();
                        newComment.DatePosted = DateTime.Parse(comment["date_posted"].ToString());
                        newComment.DateEdited = DateTime.Parse(comment["date_posted"].ToString());
                        context.BlogComments.AddOrUpdate(newComment);
                        context.SaveChanges();
                    }
                }

                // Transfer Pastes
                var pasteRet = db.Query("SELECT * FROM `paste`");
                foreach (var paste in pasteRet)
                {
                    // If it's a password protected paste, we just skip it
                    if (paste["password"] == null)
                    {
                        string content = paste["code"].ToString();
                        string title = paste["title"].ToString();
                        DateTime posted = DateTime.Parse(paste["posted"].ToString());
                        int userId = Int32.Parse(paste["user_id"].ToString());
                        Areas.Paste.Models.Paste newPaste = PasteHelper.CreatePaste(content, title);
                        newPaste.DatePosted = posted;
                        newPaste.Url = paste["pid"].ToString();
                        if (userMapping.ContainsKey(userId) && userId != 0)
                        {
                            newPaste.UserId = userMapping[userId];
                        }
                        context.Pastes.AddOrUpdate(newPaste);
                        context.SaveChanges();
                    }
                }

                // Transfer Uploads
                var uploadRet = db.Query("SELECT * FROM `uploads`");
                foreach (var upload in uploadRet)
                {
                    string url = upload["url"].ToString();
                    string fileType = upload["type"].ToString();
                    int contentLength = Int32.Parse(upload["filesize"].ToString());
                    string deleteKey = upload["delete_key"].ToString();
                    int userId = Int32.Parse(upload["user_id"].ToString());
                    DateTime uploadDate = DateTime.Parse(upload["upload_date"].ToString());
                    string fullUrl = string.Format("https://u.teknik.io/{0}", url);
                    string fileExt = Path.GetExtension(fullUrl);

                    // Download the old file and re-upload it
                    using (WebClient client = new WebClient())
                    {
                        try
                        {
                            byte[] fileData = client.DownloadData(fullUrl);
                            // Generate key and iv if empty
                            string key = Utility.RandomString(config.UploadConfig.KeySize / 8);
                            string iv = Utility.RandomString(config.UploadConfig.BlockSize / 8);

                            fileData = AES.Encrypt(fileData, key, iv);
                            if (fileData == null || fileData.Length <= 0)
                            {
                                continue;
                            }
                            Areas.Upload.Models.Upload up = Uploader.SaveFile(fileData, fileType, contentLength, fileExt, iv, key, config.UploadConfig.KeySize, config.UploadConfig.BlockSize);
                            if (userMapping.ContainsKey(userId))
                                up.UserId = userMapping[userId];
                            if (!string.IsNullOrEmpty(deleteKey))
                                up.DeleteKey = deleteKey;
                            up.Url = url;
                            context.Uploads.Add(up);
                            context.SaveChanges();
                        }
                        catch { }
                    }
                }
            }
        }

        private void Db_MysqlErrorEvent(object sender, string e)
        {
            throw new NotImplementedException();
        }
    }
}
