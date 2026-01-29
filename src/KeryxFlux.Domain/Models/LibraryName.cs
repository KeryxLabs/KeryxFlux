using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeryxFlux.Domain.Models
{
    public readonly record struct LibraryName(string Value)
    {
        public string Value { get; init; } = string.IsNullOrWhiteSpace(Value) ? throw new ArgumentNullException(nameof(Value), "Value cannot be null, empty, or whitespace") : Value;
        public LibraryName() : this(string.Empty) { }
        public static implicit operator string(LibraryName libraryName) => libraryName.Value;
    }
}
