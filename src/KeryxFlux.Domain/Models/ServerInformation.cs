using KeryxFlux.Domain.Models.Http;
using YamlDotNet.Serialization;

namespace KeryxFlux.Domain.Models
{
    public record ServerInformation
    {
        [YamlMember(Alias = "name")]
        public required string Name { get; set; }
        
        [YamlMember(Alias = "authorization")]
        public Authorization Authorization { get; set; } = new();
        
        [YamlMember(Alias = "communication_type")]
        public CommunicationType CommunicationType { get; set; }
        
        [YamlMember(Alias = "address")]
        public string Address { get; set; }
        
        [YamlMember(Alias = "port")]
        public int? Port { get; set; }
        
        [YamlMember(Alias = "time_zone_id")]
        public string TimeZoneId { get; set; } = "UTC";
        
        [YamlMember(Alias = "timeout")]
        public int Timeout { get; set; }
        
        [YamlMember(Alias = "retriable_errors")]
        public List<RetriableError> RetriableErrors { get; set; } = [];
        
        [YamlMember(Alias = "http")]
        public HttpServer? Http { get; set; }
        
        [YamlMember(Alias = "sftp")]
        public SftpServer? Sftp { get; set; }
    }
}

