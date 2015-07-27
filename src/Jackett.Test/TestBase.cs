using NUnit.Framework;

namespace JackettTest
{
    abstract class TestBase
    {
        [SetUp]
        public void Setup()
        {
            TestUtil.SetupContainer();
        }
    }
}
