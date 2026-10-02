using System.Diagnostics.CodeAnalysis;

namespace Ufw.Shared.Extensions;

/// <summary>
/// Provides collection operations missing from <see cref="IReadOnlyList{T}"/> while preserving its read-only API surface.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "C# 14.0 extension members false positive")]
public static class ReadOnlyListExtensions
{
    extension<T>(IReadOnlyList<T> self)
    {
        /// <summary>
        /// Returns the zero-based index of <paramref name="item"/>, or <c>-1</c> when it is not present.
        /// </summary>
        public int IndexOf(T item) => self switch
        {
            IList<T> list => list.IndexOf(item),
            _ => self.IndexOf(item, EqualityComparer<T>.Default),
        };

        private int IndexOf(T item, EqualityComparer<T> comparer)
        {
            for (int i = 0; i < self.Count; ++i)
            {
                if (comparer.Equals(self[i], item))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
