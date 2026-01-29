using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeryxFlux.Domain.Models
{
    public record RetriableError
    {
        public string Content { get; set; } = string.Empty;
        public int MaxRetries { get; set; }
        public double Timeout { get; set; }
    }
}
