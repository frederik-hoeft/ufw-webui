using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Parsing;
using Ufw.Shared.Parsing.Parsers;
using Ufw.Shared.Parsing.SyntaxNodes;
using Ufw.Shared.Parsing.Visitors;

namespace Ufw.Shared.Tests.Parsing;

[TestClass]
public sealed class ParserCombinatorTests
{
    [TestMethod]
    public void CanAccept_NestedGrammar_RecursivelyValidatesVisitorType()
    {
        IParser grammar = Grammar.Sequence(new CharacterParser('a'), Grammar.Optional(new CharacterParser('b')), Grammar.Repeat(new CharacterParser('c')));

        Assert.IsTrue(grammar.CanAccept<CountingVisitor>());
        Assert.IsFalse(grammar.CanAccept<OtherVisitor>());
        Assert.ThrowsExactly<InvalidOperationException>(() => grammar.RequireVisitor<OtherVisitor>());
    }

    [TestMethod]
    public void Repeat_ParsesAndVisitsEveryOccurrence()
    {
        IParser grammar = Grammar.Repeat(new CharacterParser('x'), minimumCount: 1).RequireVisitor<CountingVisitor>();

        bool parsed = grammar.TryParse("xxx", 0, out ISyntaxNode? syntaxNode, out int charsConsumed);
        CountingVisitor visitor = new();
        syntaxNode?.Accept(visitor);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(syntaxNode);
        Assert.AreEqual(3, charsConsumed);
        Assert.AreEqual(3, visitor.Count);
    }

    [TestMethod]
    public void Repeat_ZeroWidthParser_ThrowsInsteadOfLoopingForever()
    {
        Repeat repeat = new(new ZeroWidthParser());

        Assert.ThrowsExactly<InvalidOperationException>(() => repeat.TryParse("x", 0, out _, out _));
    }

    [TestMethod]
    public void Optional_MissingValue_CreatesIndependentSyntaxNodes()
    {
        Optional optional = new(new CharacterParser('x'));

        Assert.IsTrue(optional.TryParse("a", 0, out ISyntaxNode? first, out int firstConsumed));
        Assert.IsTrue(optional.TryParse("a", 0, out ISyntaxNode? second, out int secondConsumed));

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreNotSame(first, second);
        Assert.AreEqual(0, firstConsumed);
        Assert.AreEqual(0, secondConsumed);
    }

    [TestMethod]
    public void ResultSyntaxNode_WithoutVisitorBinding_AcceptsAnyVisitorAsNoOp()
    {
        ResultNode node = new(42);

        node.Accept(new OtherVisitor());

        Assert.AreEqual(42, node.Evaluate());
    }

    [TestMethod]
    public void VisitorBoundSyntaxNode_RejectsIncompatibleVisitor()
    {
        CountingSyntaxNode node = new(value: 1);

        Assert.ThrowsExactly<ArgumentException>(() => node.Accept(new OtherVisitor()));
    }

    private sealed class CountingVisitor : INodeVisitor
    {
        public int Count { get; set; }
    }

    private sealed class OtherVisitor : INodeVisitor;

    private sealed class CountingSyntaxNode(int value) : SyntaxNodeBase<CountingVisitor, int>(name: null, value)
    {
        protected override void Accept(CountingVisitor visitor) => visitor.Count++;
    }

    private sealed class ResultNode(int value) : ResultSyntaxNodeBase<int>(name: null, value);

    private sealed class NeutralSyntaxNode() : SyntaxNodeBase(name: null);

    private sealed class CharacterParser(char character) : ParserBase<CountingVisitor>
    {
        public override string? Name => null;

        public override IParser NamedCopy(string name) => new CharacterParser(character);

        public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
        {
            if ((uint)offset < (uint)input.Length && input[offset] == character)
            {
                syntaxNode = new CountingSyntaxNode(character);
                charsConsumed = 1;
                return true;
            }

            syntaxNode = null;
            charsConsumed = 0;
            return false;
        }
    }

    private sealed class ZeroWidthParser : ParserBase
    {
        public override string? Name => null;

        public override IParser NamedCopy(string name) => new ZeroWidthParser();

        public override bool TryParse(string input, int offset, [NotNullWhen(true)] out ISyntaxNode? syntaxNode, out int charsConsumed)
        {
            syntaxNode = new NeutralSyntaxNode();
            charsConsumed = 0;
            return true;
        }
    }
}
