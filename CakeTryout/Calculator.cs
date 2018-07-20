namespace CakeTryout
{
    public interface ICalculator
    {
        int Add(int first, int second);
    }

    public class Calculator : ICalculator
    {
        public int Add(int first, int second)
        {
            return first + second;
        }
    }
}
