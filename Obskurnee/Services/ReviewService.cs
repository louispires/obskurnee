﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Obskurnee.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Obskurnee.Models.GoodreadsReview.ReviewKind;

namespace Obskurnee.Services
{
    public class ReviewService
    {
        private readonly ILogger<ReviewService> _logger;
        private readonly GoodreadsScraper _scraper;
        private readonly ApplicationDbContext _db2;

        public ReviewService(
            ILogger<ReviewService> logger,
            GoodreadsScraper scraper,
            ApplicationDbContext db2)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scraper = scraper ?? throw new ArgumentNullException(nameof(scraper));
            _db2 = db2 ?? throw new ArgumentNullException(nameof(db2));
        }

        public IList<GoodreadsReview> GetCurrentlyReading(string userId) 
            => _db2.GoodreadsReviews.Where(r => r.OwnerId == userId && r.Kind == CurrentlyReading).ToList();

        public async Task FetchReviewUpdates(Bookworm user)
        {
            try
            {
                _logger.LogInformation("Updating GR review from shelf RSS for {userId}", user.Id);
                _db2.GoodreadsReviews.RemoveRange(
                    _db2.GoodreadsReviews
                        .Where(r => r.OwnerId == user.Id
                                    && r.Kind == CurrentlyReading));
                await _db2.SaveChangesAsync();
                var reading = _scraper.GetCurrentlyReadingBooks(user).ToList();
                _logger.LogInformation("Adding {count} Currently Reading for {userId}", reading.Count, user.Id);
                await _db2.GoodreadsReviews.AddRangeAsync(reading);
                var readBooks = _scraper.GetReadBooks(user).ToList();
                var ids = readBooks.Select(r => r.ReviewId);
                var existing = _db2.GoodreadsReviews.Where(r => ids.Contains(r.ReviewId)).ToArray();
                _logger.LogInformation("Deleting {count} Read for {userId}", existing.Length, user.Id);
                _db2.GoodreadsReviews.RemoveRange(existing);
                await _db2.SaveChangesAsync();
                _logger.LogInformation("Adding {count} Read for {userId}", readBooks.Count, user.Id);
                await _db2.GoodreadsReviews.AddRangeAsync(readBooks);
                await _db2.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update of GR profile for user {userId} failed", user.Id);
            }
        }

        public Task<List<BookclubReview>> GetBookReviews(int bookId)
            => _db2.BookReviewsWithData.Where(br => br.Book.BookId == bookId).ToListAsync();

        public Task<List<BookclubReview>> GetUserReviews(string userId)
            => _db2.BookReviewsWithData.Where(br => br.OwnerId == userId).ToListAsync();

        public async Task<BookclubReview> UpsertBookclubBookReview(
            int bookId,
            string userId,
            ushort rating,
            string reviewText,
            string reviewUrl)
        {
            var review = new BookclubReview(userId)
            {
                ReviewId = $"{bookId}-{userId}",
                BookId = bookId,
                ReviewText = reviewText,
                Rating = rating,
                ReviewUrl = reviewUrl,
            };
            await _db2.BookReviews.AddAsync(review);
            return review;
        }
    }
}
