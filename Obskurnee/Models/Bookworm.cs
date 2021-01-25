﻿using AspNetCore.Identity.LiteDB.Models;

namespace Obskurnee.Models
{
    public class Bookworm : ApplicationUser
    {
        public string GoodreadsProfileUrl { get; set; }
        public string AboutMe { get; set; }
        public string Phone { get; set; }
    }
}
