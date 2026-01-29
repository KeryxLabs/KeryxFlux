using KeryxFlux.Domain.Models.Http;

namespace KeryxFlux.Domain.Models
{
    public record ServerInformation
    {
        public required string Name { get; set; }
        public Authorization Authorization { get; set; } = new();
        public CommunicationType CommunicationType { get; set; }
        public string Address { get; set; }
        public int? Port { get; set; }
        public string TimeZoneId { get; set; } = "UTC";
        public int Timeout { get; set; }
        public List<RetriableError> RetriableErrors { get; set; } = [];
        public HttpServer? Http { get; set; }
        public SftpServer? Sftp { get; set; }

    }
}
