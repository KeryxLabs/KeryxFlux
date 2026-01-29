namespace KeryxFlux.Domain.Models.Http
{
    public record HttpServer
    {
        public List<Endpoint> Endpoints { get; set; } = [];
        public Pagination? Pagination { get; set; }

    }
}
