namespace KeryxFlux.Domain.Models
{
    public enum TemplateArgType
    {
        None = 0,
        Date = 1,
        Value = 2
    }

    public class TemplateArg
    {
        public required string Name { get; set; }
        public TemplateArgType Type { get; set; } = TemplateArgType.None;
        public string? Value { get; set; }
        public DateTemplate? DateTemplate { get; set; }

    }
}
