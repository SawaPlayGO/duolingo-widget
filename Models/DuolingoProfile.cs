using System;

namespace DuolingoWidget.Models
{
    public sealed class DuolingoProfile
    {
        public string Username { get; set; } = string.Empty;
        public int Streak { get; set; }
        public DateTime? StreakEndDate { get; set; }
        public string? TimeZoneId { get; set; }
        public string? TimeZoneOffset { get; set; }
    }
}
