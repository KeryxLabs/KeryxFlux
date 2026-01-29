using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeryxFlux.Domain.Models
{
    public class Endpoint
    {
        public required string Name { get; set; }
        public required string Path { get; set; }
        public int MaxAttempts { get; set; }
        public bool IsCached { get; set; }
        public string CachedProperty { get; set; } = string.Empty;
        public List<TemplateArg> TemplateArguments { get; set; } = [];
        public Dictionary<string, string> Customazibles { get; set; } = [];
    }
}
