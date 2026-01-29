using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeryxFlux.Domain.Models
{
    public readonly record struct LibraryPath(string Value)
    {
        public string Value { get; init; } = string.IsNullOrWhiteSpace(Value)
                                            ? throw new ArgumentException(nameof(Value), "Value cannot be null, empty, or whitespace.") : !Value.EndsWith(".dll")
                                            ? throw new ArgumentException(nameof(Value), "Value needs to represent a valid dll file.") : Value;
        public LibraryPath() : this(string.Empty) { }
        public static implicit operator string(LibraryPath libraryPath) => libraryPath.Value;
    }
}
