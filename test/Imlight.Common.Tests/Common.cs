using Microsoft.VisualStudio.TestTools.UnitTesting;
using Imlight.Common;
using System;

namespace Imlight.Test
{
    [TestClass]
    public class Common
    {
        [TestMethod]
        public void RandomGen_All32bitNumericals()
        {
            // Arrange
            sbyte R_sbyte;
            short R_short;
            int R_int;
            float R_float;

            // Act
            try
            {
                R_sbyte = RandomGen.SignedNumber<sbyte>();
                R_short = RandomGen.SignedNumber<short>();
                R_int = RandomGen.SignedNumber<int>();

                Console.WriteLine($"SBYTE OUTPUT OK || VALUE {R_sbyte}");
                Console.WriteLine($"SHORT OUTPUT OK || VALUE {R_short}");
                Console.WriteLine($"INTEGER OUTPUT OK || VALUE {R_int}");

                Assert.IsNotNull(R_sbyte);
                Assert.IsNotNull(R_short);
                Assert.IsNotNull(R_int);

                return;
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
            }
        }
    }
}
