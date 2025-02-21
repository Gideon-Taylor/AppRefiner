using AppRefiner.PeopleCode;
using SqlParser.Dialects;
using SqlParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner.Linters
{
    public abstract class BaseLintRule : PeopleCodeParserBaseListener
    {
        public bool Active = false;
        public string Description = "Description not set";
        public ReportType Type;
        public List<Report>? Reports;
        public abstract void Reset();

    }


    class PeopleSoftSQLDialect : GenericDialect
    {
        public override bool IsIdentifierStart(char character)
        {
            return char.IsLetter(character) ||
                   character is Symbols.Underscore
                       or Symbols.Num
                       or Symbols.At
                       or Symbols.Percent;
        }
    }
}
