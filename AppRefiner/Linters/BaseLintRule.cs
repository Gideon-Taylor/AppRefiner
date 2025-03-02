using AppRefiner.PeopleCode;
using SqlParser.Dialects;
using SqlParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using AppRefiner.Database;

namespace AppRefiner.Linters
{
    public abstract class BaseLintRule : PeopleCodeParserBaseListener
    {
        public bool Active = false;
        public string Description = "Description not set";
        public ReportType Type;
        public List<Report>? Reports;
        public virtual DataManagerRequirement DatabaseRequirement => DataManagerRequirement.NotRequired;
        public IDataManager? DataManager;

        // Add collection to store comments from lexer
        public IList<IToken>? Comments;
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
