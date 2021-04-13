﻿using LiteDB;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Obskurnee.Models
{
    [Table("Votes")]
    [Index(nameof(PollId))]
    [Index(nameof(OwnerId))]
    public class Vote : HeaderData
    {
        [Key]
        [BsonId]
        public string VoteId { get => $"{PollId}-{OwnerId}"; set { } }

        public int PollId { get; set; }
        public Poll Poll { get; set; }
        /// <summary>
        /// Contains votes - IDs of posts the user voted for
        /// </summary>
        [NotMapped]
        public int[] PostIds
        {
            get => !string.IsNullOrWhiteSpace(PostIdsSerialized)
                    ? JsonConvert.DeserializeObject<int[]>(PostIdsSerialized)
                    : null;
            set
            {
                PostIdsSerialized = JsonConvert.SerializeObject(value);
            }
        }

        public string PostIdsSerialized { get; set; }

        public Vote(string ownerId) : base(ownerId) { }
    }
}
