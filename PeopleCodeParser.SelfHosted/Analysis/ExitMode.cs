namespace PeopleCodeParser.SelfHosted.Analysis;

/// <summary>
/// The set of ways control can leave a statement or block. A statement/block can
/// complete in more than one way (e.g. an If whose Then returns and whose Else falls
/// through), so this is a set — the [Flags] value IS the set.
/// <para>
/// <see cref="Normal"/> means control can reach the end of the block (fall-through /
/// "normal completion"). All other members are abrupt completions.
/// </para>
/// </summary>
[Flags]
public enum ExitMode
{
    None     = 0,
    Normal   = 1,   // control reaches the end of the block (falls off)
    Return   = 2,
    Throw    = 4,
    Exit     = 8,
    Error    = 16,
    Break    = 32,
    Continue = 64,
}
