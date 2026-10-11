using System.Diagnostics.CodeAnalysis;

namespace Ufw.Shared.Extensions;

[SuppressMessage("Design", CA1034_NESTED_TYPES_VISIBLE, Justification = CA1034_JUSTIFY_EXTENSION_METHODS)]
public static class SpanExtensions
{
    extension<T>(ReadOnlySpan<T> self)
    {
        /// <summary>
        /// Returns the index of the first occurrence of <paramref name="value"/> in the span, or -1 if not found.
        /// If the value is not found, <paramref name="sliceBoundary"/> will be set to the length of the span.
        /// </summary>
        /// <param name="value">The value to search for.</param>
        /// <param name="sliceBoundary">The boundary of the slice containing the found value, or the length of the span if not found.</param>
        /// <returns>The index of the first occurrence of <paramref name="value"/> in the span, or -1 if not found.</returns>
        public int IndexOf(T value, out int sliceBoundary)
        {
            int index = self.IndexOf(value);
            int mask = index >> 31;
            sliceBoundary = (mask & self.Length) | (~mask & index);
            return index;
        }
    }
}
