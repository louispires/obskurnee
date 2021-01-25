﻿using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Obskurnee.Models
{
    public class Round : HeaderData
    {

        [BsonId] public int RoundId { get; set; }
        public string Title { get; set; }
        public int BookId { get; set; }
        public int ThemeDiscussionId { get; set; }
        public int ThemePollId { get; set; }
        public int BookDiscussionId { get; set; }
        public int BookPollId { get; set; }
        public Topic Kind { get; set; }

        public Round(string ownerId) : base(ownerId)
        {
        }
    }
}
