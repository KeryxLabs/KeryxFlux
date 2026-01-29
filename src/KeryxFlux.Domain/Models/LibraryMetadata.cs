using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeryxFlux.Domain.Models
{
    public record LibraryMetadata
    {
        public LibraryInfo Info { get; set; } = null!;
        public Type Type { get; set; } = null!;

    }
}
