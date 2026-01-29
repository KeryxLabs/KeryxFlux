using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeryxFlux.Domain.Models
{
    public record LibraryInfo
    {
        public LibraryName LibraryName { get; set; }
        public LibraryPath LibraryPath { get; set; }
        public Version? LibraryVersion { get; set; }
    }
}
