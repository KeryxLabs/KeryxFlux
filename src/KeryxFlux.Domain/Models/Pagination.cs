namespace KeryxFlux.Domain.Models
{
    public enum PaginationType
    {
        None,
        RecordCount,
        PageCount
    }

    public class Pagination
    {
        public int MaxPageSize { get; set; } = 50;
        public int MaxPageNumber { get; set; } = 5;
        public string PaginationProperty { get; set; } = string.Empty;
        public PaginationType PaginationType { get; set; } = PaginationType.None;
    }
}
