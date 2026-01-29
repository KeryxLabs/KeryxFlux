namespace KeryxFlux.Domain.Models
{
    public enum DateDelay
    {
        None = 0,
        Minute = 1,
        Hour = 2,
        Day = 3,
        Week = 4,
        Month = 5,
        Year = 6
    }

    public record DateTemplate
    {
        public DateDelay DelayType { get; set; }
        public int DelayTime { get; set; }
        public string StringFormat { get; set; } = string.Empty;
    }
}
