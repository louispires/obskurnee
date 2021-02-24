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

        public DiscussionService(
            ILogger<DiscussionService> logger,
            Database database,
            IStringLocalizer<Strings> localizer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _db = database ?? throw new ArgumentNullException(nameof(database));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
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
            return post;
        }

        internal Discussion GetWithPosts(int discussionId) => _db.Discussions.Include(d => d.Posts).FindById(discussionId);
    }
}
