using KeryxFlux.Domain.Models.Http;

namespace KeryxFlux.Domain.Models
{
    public class Docket
    {
        public required string Name { get; set; }
        public required string PluginLocation { get; set; }
        public string? CronExpression { get; set; }
        public required ServerInformation ServerInformation { get; set; }

    }
}
