﻿using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Obskurnee.Models;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Obskurnee.Services
{
    public class DiscussionService
    {
        private readonly ILogger<DiscussionService> _logger;
        private readonly Database _db;
        private static readonly object @lock = new object();
        private readonly IStringLocalizer<Strings> _localizer;
        private readonly IStringLocalizer<NewsletterStrings> _newsletterLocalizer;
        private readonly NewsletterService _newsletter;
        private readonly Config _config;

        public DiscussionService(
            ILogger<DiscussionService> logger,
            Database database,
            IStringLocalizer<Strings> localizer,
            IStringLocalizer<NewsletterStrings> newsletterLocalizer,
            NewsletterService newsletter,
            Config config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _db = database ?? throw new ArgumentNullException(nameof(database));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _newsletter = newsletter ?? throw new ArgumentNullException(nameof(newsletter));
            _newsletterLocalizer = newsletterLocalizer ?? throw new ArgumentNullException(nameof(newsletterLocalizer));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }


        public IEnumerable<Discussion> GetAll()
            => (from discussion in _db.Discussions.Query()
                orderby discussion.CreatedOn descending
                select discussion)
                .ToList();

        public Post NewPost(int discussionId, Post post)
        {
            var discussion = _db.Discussions.FindById(discussionId);
            if (discussion.IsClosed)
            {
                throw new Exception(_localizer["discussionClosed"]);
            }
            post.PostId = 0; //ensure it wasn't sent from the client
            post.DiscussionId = discussionId;

            lock (@lock)
            {
                _db.Posts.Insert(post);
                discussion.Posts = _db.Posts.Find(p => p.DiscussionId == discussionId).OrderBy(p => p.CreatedOn).ToList();
                _db.Discussions.Update(discussion);
            }
            SendNewPostNotification(discussion, post);
            return post;
        }

        private void SendNewPostNotification(Discussion discussion, Post post)
        {
            var link = $"{_config.BaseUrl}/navrhy/{discussion.DiscussionId}";
            var body = "";
            if (discussion.Topic == Topic.Books)
            {
                body = _newsletterLocalizer.FormatAndRender(
                    "newBookPostBodyMarkdown",
                    post.Title,
                    post.Author,
                    post.Text.AddMarkdownQuote(),
                    link,
                    post.PageCount,
                    post.ImageUrl,
                    post.Url);
            }
            else if (discussion.Topic == Topic.Themes)
            {
                body = _newsletterLocalizer.FormatAndRender(
                    "newThemePostBodyMarkdown",
                    post.Title,
                    post.Text,
                    link);
            }
            _newsletter.SendNewsletter(
                Newsletters.AllEvents,
                _newsletterLocalizer.Format("newPostSubject", post.OwnerName),
                body);
        }

        public Discussion GetWithPosts(int discussionId) => _db.Discussions.Include(d => d.Posts).FindById(discussionId);

        public Discussion GetLatestOpen() => _db.Discussions.Find(d => !d.IsClosed).OrderByDescending(d => d.DiscussionId).FirstOrDefault();
    }
}
