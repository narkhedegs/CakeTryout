using NUnit.Framework;

namespace CakeTryout.UnitTests
{
    [TestFixture]
    // ReSharper disable once InconsistentNaming
    public class When_adding_two_integers
    {
        private ICalculator _calculator;

        [SetUp]
        public void SetUp()
        {
            _calculator = new Calculator();
        }

        [Test]
        public void It_should_return_correct_results()
        {
            const int first = 2;
            const int second = 2;

            var result = _calculator.Add(first, second);

            Assert.That(result == 4);
        }
    }
}
