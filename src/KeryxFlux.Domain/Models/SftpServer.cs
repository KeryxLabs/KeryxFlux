namespace KeryxFlux.Domain.Models
{
    public record SftpServer
    {
        public string RemoteRoot { get; set; }
        public string LocalRoot { get; set; }

    }
}
