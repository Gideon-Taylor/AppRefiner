using Antlr4.Runtime.Tree;

namespace AppRefiner.PeopleCode
{
    public class MultiParseTreeWalker : ParseTreeWalker
    {
        List<IParseTreeListener> listeners = new();
        public MultiParseTreeWalker()
        {
        }
        public void AddListener(IParseTreeListener listener)
        {
            listeners.Add(listener);
        }
        public void Walk(IParseTree t)
        {
            if (t is IErrorNode)
            {
                foreach (var l in listeners)
                {
                    l.VisitErrorNode((IErrorNode)t);
                }
                return;
            }

            if (t is ITerminalNode)
            {
                foreach (var listener in listeners)
                {
                    listener.VisitTerminal((ITerminalNode)t);
                }
                return;
            }

            IRuleNode ruleNode = (IRuleNode)t;
            foreach (var l in listeners)
            {
                EnterRule(l, ruleNode);
            }
            int childCount = ruleNode.ChildCount;
            for (int i = 0; i < childCount; i++)
            {
                Walk(ruleNode.GetChild(i));
            }
            foreach (var l in listeners)
            {
                ExitRule(l, ruleNode);
            }
        }
    }
}
