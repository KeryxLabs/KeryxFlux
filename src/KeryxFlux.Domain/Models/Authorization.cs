namespace KeryxFlux.Domain.Models
{
    public enum AuthorizationType
    {
        None = 0,
        Basic = 1,
        Bearer = 2,
    }

    public record Authorization
    {
        public AuthorizationType AuthorizationType { get; set; } = AuthorizationType.None;
        public string BaseUrl { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool Refreshes { get; set; }
        public Dictionary<string, string> Credentials { get; set; } = [];

        public Authorization()
        {
            Refreshes = false;
        }
    }
}
