using Ufw.Shared.Extensions;

namespace Ufw.Shared.Tests.Extensions;

[TestClass]
public sealed class ReadOnlyListExtensionsTests
{
    [TestMethod]
    public void IndexOf_IListBackedList_FindsItem()
    {
        IReadOnlyList<string> values = new List<string> { "alpha", "beta", "gamma" };

        Assert.AreEqual(1, values.IndexOf("beta"));
        Assert.AreEqual(-1, values.IndexOf("missing"));
    }

    [TestMethod]
    public void IndexOf_ReadOnlyListWithoutIList_FindsItem()
    {
        IReadOnlyList<string> values = new ReadOnlyListOnly<string>(["alpha", "beta", "gamma"]);

        Assert.AreEqual(2, values.IndexOf("gamma"));
        Assert.AreEqual(-1, values.IndexOf("missing"));
    }

    private sealed class ReadOnlyListOnly<T>(IReadOnlyList<T> values) : IReadOnlyList<T>
    {
        public T this[int index] => values[index];

        public int Count => values.Count;

        public IEnumerator<T> GetEnumerator() => values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
