using System;
using System.Collections.Generic;
using System.Linq;

namespace NX2512_HotkeyStudio.Models
{
    /// <summary>
    /// Forces array.Reverse() calls in the schema-v5 model to use the enumerable form.
    /// Newer .NET compilers also expose a Span-based Reverse extension returning void;
    /// the exact array receiver keeps mnemonic candidate enumeration unambiguous.
    /// </summary>
    internal static class ArrayReverseCompatibilityExtensions
    {
        public static IEnumerable<T> Reverse<T>(this T[] source)
        {
            return Enumerable.Reverse(source ?? Array.Empty<T>());
        }
    }
}
