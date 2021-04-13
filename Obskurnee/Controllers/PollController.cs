﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Obskurnee.Models;
using Obskurnee.Services;
using Obskurnee.ViewModels;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
using Microsoft.Extensions.Localization;
using System.Linq;
using System.Threading.Tasks;

namespace Obskurnee.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/polls")]
    public class PollController : Controller
    {
        private readonly ILogger<PollController> _logger;
        private readonly PollService _polls;
        private readonly RoundManagerService _roundManager;
        private readonly UserService _users;

        public PollController(
            ILogger<PollController> logger,
            PollService polls,
            RoundManagerService roundManager,
            UserService users)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _polls = polls ?? throw new ArgumentNullException(nameof(polls));
            _roundManager = roundManager ?? throw new ArgumentNullException(nameof(roundManager));
            _users = users ?? throw new ArgumentNullException(nameof(users));
        }

        [HttpGet]
        public async Task<IEnumerable<Poll>> GetPolls() => await _polls.GetAll();
        
        [HttpGet]
        [Route("{pollId:int}")]
        public async Task<PollInfo> GetPoll(int pollId) => await _polls.GetPollInfo(pollId, User.GetUserId());

        [HttpPost]
        [Route("{pollId:int}/vote")]
        public async Task<RoundUpdateResults> CastVote(int pollId, Vote vote)
        {
            vote.PollId = pollId;
            var poll = await _polls.CastPollVote(vote.SetOwner(User));
            _logger.LogInformation("User {userId} voted in poll {pollId}", User.GetUserId(), pollId);
            if (poll.Results.AlreadyVoted.Count == (await _users.GetAllUsers()).Count)
            {
                return _roundManager.ClosePoll(pollId, User.GetUserId());
            }
            return new RoundUpdateResults { Poll = poll };
        }
    }
}
