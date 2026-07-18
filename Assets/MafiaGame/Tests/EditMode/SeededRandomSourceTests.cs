using System.Collections.Generic;
using MafiaGame.Domain.Randomness;
using NUnit.Framework;

namespace MafiaGame.Tests.EditMode
{
    public sealed class SeededRandomSourceTests
    {
        [Test]
        public void SameSeed_ProducesIdenticalShuffle()
        {
            var a = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
            var b = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };

            new SeededRandomSource(1234).Shuffle(a);
            new SeededRandomSource(1234).Shuffle(b);

            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Shuffle_IsAPermutationOfInput()
        {
            var list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            new SeededRandomSource(99).Shuffle(list);

            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, list);
        }

        [Test]
        public void NextInt_StaysWithinRequestedRange()
        {
            var random = new SeededRandomSource(7);

            for (int i = 0; i < 100; i++)
            {
                int value = random.NextInt(5, 10);
                Assert.GreaterOrEqual(value, 5);
                Assert.Less(value, 10);
            }
        }
    }
}
