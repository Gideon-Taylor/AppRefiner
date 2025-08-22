using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace PeopleCodeParser.Tests.ParserTests
{
    public class ParserTests
    {
        [Fact]
        public void RunBasicTest()
        {
            // This test will run the basic parser test defined in PeopleCodeParser.SelfHosted.ParserTest
            ParserTest.RunBasicTest();
        }


    }
}
