﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Obskurnee.Models;
using Obskurnee.Services;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

using System.Threading.Tasks;

namespace Obskurnee.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/reviews")]
    public class ReviewController : Controller
    {
        private readonly ILogger<ReviewController> _logger;
        private readonly ReviewService _reviews;

        public ReviewController(
            ILogger<ReviewController> logger,
            ReviewService reviews)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _reviews = reviews ?? throw new ArgumentNullException(nameof(reviews));
        }

        [HttpGet]
        [Route("book/{bookId:int}")]
        public Task<List<BookclubReview>> GetForBook(int bookId)
            => _reviews.GetBookReviews(bookId);
        
        [HttpGet]
        [Route("user/{userId}")]
        public Task<List<BookclubReview>> GetForUser(string userId)
            => _reviews.GetUserReviews(userId);

        [HttpPost]
        [Route("book/{bookId:int}")]
        public Task<BookclubReview> UpsertReview(int bookId, [FromBody] BookclubReview reviewData)
            => _reviews.UpsertBookclubBookReview(
                bookId,
                User.GetUserId(),
                reviewData.Rating,
                reviewData.ReviewText,
                reviewData.ReviewUrl);
    }
}
