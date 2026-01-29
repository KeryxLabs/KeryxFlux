
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Extensions;

namespace KeryxFlux.Domain.Models.Http
{
    public record HttpEntry(string EndpointName, Docket Docket) : RunnableEntry(EndpointName)
    {
        public EntryProcessTime ProcessingTime { get; set; } = new EntryProcessTime();

        public void UpdateProcessingTime()
        {
            var currentRunningTime = DateTime.UtcNow;
            TimeZoneInfo tZone = TimeZoneInfo.FindSystemTimeZoneById(Docket.ServerInformation.TimeZoneId);
            DateTime serverTime = TimeZoneInfo.ConvertTimeToUtc(currentRunningTime, tZone);
            ProcessingTime = new EntryProcessTime()
            {
                EntryName = Name,
                TimeZoneId = Docket.ServerInformation.TimeZoneId,
                UtcTime = currentRunningTime,
                ServerTime = serverTime
            };

        }

        public new string Name =>
            $"{Docket.Name}-{Docket.ServerInformation.Name}-{Name}";

    }
}
