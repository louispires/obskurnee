﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Obskurnee.Models;
using Obskurnee.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Obskurnee.Services
{
    public class RoundManagerService
    {
        private readonly ILogger<RoundManagerService> _logger;
        private readonly ApplicationDbContext _db;
        private readonly object @lock = new object();
        private readonly BookService _bookService;
        private readonly NewsletterService _newsletter;
        private readonly IStringLocalizer<Strings> _localizer;
        private readonly IStringLocalizer<NewsletterStrings> _newsletterLocalizer;
        private readonly Config _config;

        public RoundManagerService(
            ILogger<RoundManagerService> logger,
            ApplicationDbContext database,
            BookService bookService,
            NewsletterService newsletter,
            IStringLocalizer<Strings> localizer,
            IStringLocalizer<NewsletterStrings> newsletterLocalizer,
            Config config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _db = database ?? throw new ArgumentNullException(nameof(database));
            _bookService = bookService ?? throw new ArgumentNullException(nameof(bookService));
            _newsletter = newsletter;
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _newsletterLocalizer = newsletterLocalizer ?? throw new ArgumentNullException(nameof(newsletterLocalizer));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<IList<Round>> AllRounds()
            => await _db.Rounds
                        .Include(r => r.Book)
                        .OrderByDescending(r => r.CreatedOn)
                        .ToListAsync();

        public async Task<Round> NewRound(Topic topic, string title, string description, string ownerId)
        {
            var round = new Round(ownerId)
            {
                Kind = topic,
                Title = title,
            };
            var discussion = new Discussion(ownerId)
            {
                Description = description,
                Topic = topic,
            };

            await _db.Rounds.AddAsync(round);
            await _db.SaveChangesAsync();
            switch (topic)
            {
                case Topic.Books:
                    discussion.Title = _localizer.Format("bookDiscussionTitle", title);
                    discussion.RoundId = round.RoundId;
                    await _db.Discussions.AddAsync(discussion);
                    await _db.SaveChangesAsync();
                    round.BookDiscussion = discussion;
                    break;
                case Topic.Themes:
                    discussion.Title = _localizer.Format("topicDiscussionTitle", title);
                    discussion.RoundId = round.RoundId;
                    await _db.Discussions.AddAsync(discussion);
                    round.ThemeDiscussion = discussion;
                    break;
                default:
                    throw new Exception($"Invalid Topic: {topic}");
            }
            _db.Rounds.Update(round);
            await _db.SaveChangesAsync();
            SendNewDiscussionNotification(discussion);
            return round;
        }

        public RoundUpdateResults CloseDiscussion(int discussionId, string currentUserId)
        {
            var discussion = _db.Discussions.Include(d => d.Posts).First(d => d.DiscussionId == discussionId);
            if (discussion.IsClosed)
            {
                throw new PermissionException(_localizer["discussionClosed"]);
            }
            if (discussion.Posts.Count < 1)
            {
                throw new PermissionException(_localizer["atLeastOneProposalRequired"]);
            }
            //var round = _db.Rounds.FindById(discussion.RoundId);
            //lock (@lock)
            //{
            //    discussion.IsClosed = true;
            //    var posts = (from post in _db.Posts.Query()
            //                 where post.DiscussionId == discussionId
            //                 orderby post.PostId
            //                 select post)
            //                 .ToList();
            //    var poll = new Poll(currentUserId)
            //    {
            //        DiscussionId = discussion.DiscussionId,
            //        Title = _localizer.Format("pollTitle", discussion.Title),
            //        Options = posts,
            //        Topic = discussion.Topic,
            //        RoundId = round.RoundId,
            //    };
            //    _db.Polls.Insert(poll);
            //    discussion.PollId = poll.PollId;
            //    switch (discussion.Topic)
            //    {
            //        case Topic.Books:
            //            round.BookPollId = poll.PollId;
            //            break;
            //        case Topic.Themes:
            //            round.ThemePollId = poll.PollId;
            //            break;
            //    }
            //    _db.Discussions.Update(discussion);
            //    _db.Rounds.Update(round);

            //    SendNewPollNotification(poll);
            //}
            //return new() { Discussion = discussion, Round = round };
            return null;
        }

        public RoundUpdateResults ClosePoll(int pollId, string currentUserId)
        {
            //lock (@lock)
            //{
            //    _logger.LogInformation("Closing poll {pollId}", pollId);
            //    RoundUpdateResults result;
            //    var poll = _db.Polls.FindById(pollId);
            //    var round = _db.Rounds.FindById(poll.RoundId);
            //    Trace.Assert(!poll.IsClosed);
            //    Trace.Assert(poll.Results.Votes.Count > 0);
            //    poll.IsClosed = true;

            //    var winners = poll.FindWinningPosts();
            //    Trace.Assert(winners.Count > 0);

            //    if (winners.Count == 1
            //        || poll.IsTiebreaker)
            //    {
            //        poll.Results.WinnerPostId = winners.OrderBy(p => Guid.NewGuid()).First();
            //        Trace.Assert(poll.Results.WinnerPostId != 0);
            //        result = new RoundUpdateResults { Poll = poll, Round = round };
            //        switch (poll.Topic)
            //        {
            //            case Topic.Books:
            //                result.Book = _bookService.CreateBook(poll, round.RoundId, currentUserId);
            //                round.BookId = result.Book.BookId;
            //                poll.FollowupLink = new Poll.FollowupReference(Poll.LinkKind.Book, result.Book.BookId);
            //                SendNewBookNotification(result);
            //                break;
            //            case Topic.Themes:
            //                result.Discussion = CreateDiscussionFromTopicPoll(currentUserId, poll, round);
            //                round.BookDiscussionId = result.Discussion.DiscussionId;
            //                poll.FollowupLink = new Poll.FollowupReference(Poll.LinkKind.Discussion, result.Discussion.DiscussionId);
            //                SendNewDiscussionNotification(result.Discussion);
            //                break;
            //        }
            //    }
            //    else
            //    {
            //        _logger.LogInformation("There are {count} winning posts with {max} votes. A tiebreaker round is needed.");
            //        var posts = (from post in _db.Posts.Query()
            //                     where winners.Contains(post.PostId)
            //                     orderby post.PostId
            //                     select post)
            //                    .ToList();
            //        var tiebreaker = new Poll(currentUserId)
            //        {
            //            PreviousPollId = poll.PollId,
            //            DiscussionId = poll.DiscussionId,
            //            Title = _localizer.Format("tiebreakerTitle", poll.Title),
            //            Options = posts,
            //            Topic = poll.Topic,
            //            IsTiebreaker = true,
            //            RoundId = round.RoundId,
            //        };
            //        _db.Polls.Insert(tiebreaker);
            //        poll.FollowupLink = new Poll.FollowupReference(Poll.LinkKind.Poll, tiebreaker.PollId);
            //        switch (poll.Topic)
            //        {
            //            case Topic.Books:
            //                round.BookTiebreakerPollId = tiebreaker.PollId;
            //                break;
            //            case Topic.Themes:
            //                round.ThemeTiebreakerPollId = tiebreaker.PollId;
            //                break;
            //            default:
            //                throw new Exception($"Unexpected Topic: {poll.Topic}");
            //        }
            //        result = new RoundUpdateResults { Poll = tiebreaker, Round = round };
            //        SendNewPollNotification(tiebreaker);
            //    }
            //    _db.Polls.Update(poll);
            //    _db.Rounds.Update(round);
            //    return result;
            //}

            return null;
        }

        private async Task<Discussion> CreateDiscussionFromTopicPoll(
            string currentUserId, 
            Poll poll, 
            Round round)
        {
            var winnerPost = _db.Posts.First(p => p.PostId == poll.Results.WinnerPostId);
            var bookDiscussion = new Discussion(currentUserId)
            {
                RoundId = round.RoundId,
                Topic = Topic.Books,
                Title = _localizer.Format("bookDiscussionTitle", round.Title),
                Description = $"**{winnerPost.Title}** - {winnerPost.Text}",
            };
            await _db.Discussions.AddAsync(bookDiscussion);
            await _db.SaveChangesAsync();
            return bookDiscussion;
        }

        private void SendNewDiscussionNotification(Discussion discussion)
        {
            var link = $"{_config.BaseUrl}/navrhy/{discussion.DiscussionId}";
            _newsletter.SendNewsletter(
                Newsletters.BasicEvents,
                _newsletterLocalizer.Format("newRoundSubject", discussion.Title),
                _newsletterLocalizer.FormatAndRender("newRoundBodyMarkdown", link)
                    + (string.IsNullOrWhiteSpace(discussion.Description)
                        ? ""
                        : _newsletterLocalizer.FormatAndRender("additionalDescriptionMarkdown", discussion.Description)));
        }

        private void SendNewPollNotification(Poll poll)
        {
            var link = $"{_config.BaseUrl}/hlasovania/{poll.PollId}";
            _newsletter.SendNewsletter(
                Newsletters.BasicEvents,
                _newsletterLocalizer.Format("newPollSubject", poll.Title),
                _newsletterLocalizer.FormatAndRender("newPollBodyMarkdown", link));
        }

        private void SendNewBookNotification(RoundUpdateResults result)
        {
            var link = $"{_config.BaseUrl}/knihy/{result.Book.BookId}";
            var post = _db.Posts.First(p => p.PostId == result.Book.Post.PostId);
            _newsletter.SendNewsletter(
                Newsletters.BasicEvents,
                _newsletterLocalizer["newBookSubject"],
                _newsletterLocalizer.FormatAndRender("newBookVotedBodyMarkdown", 
                    link,
                    post.Title,
                    post.Author,
                    post.Text.AddMarkdownQuote(),
                    post.PageCount,
                    post.OwnerName,
                    post.Url));
        }
    }
}
