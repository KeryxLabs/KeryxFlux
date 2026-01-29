
namespace KeryxFlux.Domain.Models
{
    public sealed record EntryProcessTime
    {
        public string EntryName { get; set; }
        public string TimeZoneId { get; set; } 
        public DateTime UtcTime { get; set; } 
        public DateTime ServerTime { get; set; }
            
        public EntryProcessTime() 
        {
            var currentTime = DateTime.UtcNow;
            EntryName  = string.Empty;
            TimeZoneId  = "UTC";
            UtcTime  = currentTime;
            ServerTime  = currentTime;

        }
    }
}
