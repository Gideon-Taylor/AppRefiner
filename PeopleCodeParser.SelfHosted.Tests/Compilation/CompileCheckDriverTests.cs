using System.Reflection;
using PeopleCodeParser.SelfHosted.Compilation;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using Xunit;

namespace PeopleCodeParser.SelfHosted.Tests.Compilation;

public class CompileCheckDriverTests
{
    /// <summary>
    /// Mechanically enforces the driver's core invariant: it must override EVERY VisitX
    /// method declared on <see cref="IAstVisitor"/> so dispatch rides the Accept path and
    /// every node type is dispatched exactly once. Where the representative Contains
    /// assertions above only sample node categories, this catches any future IAstVisitor
    /// addition whose override was forgotten in CompileCheckDriver.
    /// </summary>
    [Fact]
    public void Driver_overrides_every_IAstVisitor_VisitX_method()
    {
        var visitMethods = typeof(IAstVisitor)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.StartsWith("Visit", StringComparison.Ordinal))
            .ToList();

        // Guard against a silent no-op if the interface method naming ever changes.
        Assert.NotEmpty(visitMethods);

        var missing = new List<string>();
        foreach (var m in visitMethods)
        {
            var paramTypes = m.GetParameters().Select(p => p.ParameterType).ToArray();

            // Match on name + parameter type so overloads are distinguished.
            var overrideMethod = typeof(CompileCheckDriver).GetMethod(
                m.Name,
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: paramTypes,
                modifiers: null);

            if (overrideMethod?.DeclaringType != typeof(CompileCheckDriver))
            {
                var paramDesc = string.Join(", ", paramTypes.Select(t => t.Name));
                missing.Add($"{m.Name}({paramDesc})");
            }
        }

        Assert.True(missing.Count == 0,
            "CompileCheckDriver is missing overrides for IAstVisitor methods: "
            + string.Join(", ", missing));
    }

    /// <summary>
    /// A recording check that captures every node the driver dispatches to it.
    /// </summary>
    private sealed class RecordingCheck : CompileCheckBase
    {
        public readonly List<AstNode> Seen = new();
        public bool FinishCalled;

        public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
            => Seen.Add(node);

        public override void Finish(CompileCheckContext ctx, IDiagnosticSink sink)
            => FinishCalled = true;
    }

    /// <summary>
    /// A check that throws from every hook, to prove one bad check cannot abort the pass.
    /// </summary>
    private sealed class ThrowingCheck : CompileCheckBase
    {
        public override void OnNode(AstNode node, CompileCheckContext ctx, IDiagnosticSink sink)
            => throw new InvalidOperationException("boom in OnNode");

        public override void Finish(CompileCheckContext ctx, IDiagnosticSink sink)
            => throw new InvalidOperationException("boom in Finish");
    }

    private sealed class NullSink : IDiagnosticSink
    {
        public void Report(CompileDiagnostic diagnostic) { }
    }

    /// <summary>
    /// A rich, valid program that exercises many node categories: imports, an app class
    /// with properties/constants/instance vars, app-class type references (the critical
    /// case — type nodes are Accept()-ed explicitly and are NOT in node.Children walks),
    /// explicit-traversal statements (If/While/For/Try/Return), and a broad expression mix.
    /// </summary>
    private const string RichSource = @"
import TSTPKG:Base;

class Bar
   method Bar();
   method DoIt(&x As string) Returns number;
   property string Name get;
private
   instance number &count;
   Constant &MAX = 10;
end-class;

method Bar
   &count = 0;
end-method;

method DoIt
   Local number &n = 1 + 2;
   Local number &i;
   Local string &s;
   Local TSTPKG:Base &obj;
   &obj = create TSTPKG:Base();
   &s = &obj.SomeProp;
   If &n > 0 Then
      &n = &n - 1;
   Else
      &n = 0;
   End-If;
   While &n < 10
      &n = &n + 1;
   End-While;
   For &i = 1 To 3
      &n = &n + &i;
   End-For;
   try
      &n = Len(&x);
   catch Exception &ex
      &n = 0;
   end-try;
   Return &n;
end-method;

get Name
   Return ""hi"";
end-get;
";

    [Fact]
    public void Driver_dispatches_every_node_exactly_once()
    {
        var (program, errors) = ParseTestHelper.Parse(RichSource);

        // The sample must be a genuinely valid program, otherwise the node-type
        // assertions below prove nothing.
        Assert.Empty(errors);

        var recorder = new RecordingCheck();
        var driver = new CompileCheckDriver(new[] { recorder }, new NullSink());
        driver.Context = new CompileCheckContext(program, null, "Bar", driver);
        driver.Run(program);

        // Exactly-once: no node instance was dispatched twice.
        Assert.Equal(
            recorder.Seen.Count,
            recorder.Seen.Distinct(ReferenceEqualityComparer.Instance).Count());

        // Pre-order: the program root is dispatched first.
        Assert.NotEmpty(recorder.Seen);
        Assert.IsType<ProgramNode>(recorder.Seen[0]);

        // Broad representative sample spanning every node category.

        // Program structure
        Assert.Contains(recorder.Seen, n => n is ProgramNode);
        Assert.Contains(recorder.Seen, n => n is ImportNode);
        Assert.Contains(recorder.Seen, n => n is AppClassNode);

        // Type nodes — THE critical assertions: type nodes are Accept()-ed explicitly
        // by the scoped visitor and never appear in node.Children walks. Their presence
        // proves dispatch rides the Accept traversal, not a Children walk.
        Assert.Contains(recorder.Seen, n => n is AppClassTypeNode);
        Assert.Contains(recorder.Seen, n => n is BuiltInTypeNode);

        // Declarations
        Assert.Contains(recorder.Seen, n => n is MethodNode);
        Assert.Contains(recorder.Seen, n => n is MethodImplNode);
        Assert.Contains(recorder.Seen, n => n is PropertyNode);
        Assert.Contains(recorder.Seen, n => n is ConstantNode);
        Assert.Contains(recorder.Seen, n => n is ProgramVariableNode);

        // Statements (explicit-traversal node types that bypass DefaultVisit)
        Assert.Contains(recorder.Seen, n => n is BlockNode);
        Assert.Contains(recorder.Seen, n => n is IfStatementNode);
        Assert.Contains(recorder.Seen, n => n is WhileStatementNode);
        Assert.Contains(recorder.Seen, n => n is ForStatementNode);
        Assert.Contains(recorder.Seen, n => n is TryStatementNode);
        Assert.Contains(recorder.Seen, n => n is CatchStatementNode);
        Assert.Contains(recorder.Seen, n => n is ReturnStatementNode);
        Assert.Contains(recorder.Seen, n => n is ExpressionStatementNode);

        // Expressions
        Assert.Contains(recorder.Seen, n => n is BinaryOperationNode);
        Assert.Contains(recorder.Seen, n => n is AssignmentNode);
        Assert.Contains(recorder.Seen, n => n is FunctionCallNode);
        Assert.Contains(recorder.Seen, n => n is MemberAccessNode);
        Assert.Contains(recorder.Seen, n => n is ObjectCreationNode);
        Assert.Contains(recorder.Seen, n => n is IdentifierNode);
        Assert.Contains(recorder.Seen, n => n is LiteralNode);
        Assert.Contains(recorder.Seen, n => n is LocalVariableDeclarationNode);
        Assert.Contains(recorder.Seen, n => n is LocalVariableDeclarationWithAssignmentNode);

        // Lifecycle
        Assert.True(recorder.FinishCalled);
        Assert.Empty(driver.Failures);
    }

    [Fact]
    public void Throwing_check_does_not_abort_pass_and_is_recorded_in_failures()
    {
        var (program, errors) = ParseTestHelper.Parse(RichSource);
        Assert.Empty(errors);

        var throwing = new ThrowingCheck();
        var recorder = new RecordingCheck();

        // Throwing check registered FIRST so its exceptions would abort dispatch to the
        // recorder if the driver failed to isolate check failures.
        var driver = new CompileCheckDriver(new ICompileCheck[] { throwing, recorder }, new NullSink());
        driver.Context = new CompileCheckContext(program, null, "Bar", driver);
        driver.Run(program);

        // The healthy check still saw the whole traversal and its Finish still ran.
        Assert.Contains(recorder.Seen, n => n is ProgramNode);
        Assert.Contains(recorder.Seen, n => n is AppClassTypeNode);
        Assert.Contains(recorder.Seen, n => n is ReturnStatementNode);
        Assert.True(recorder.FinishCalled);

        // Every recorded failure belongs to the throwing check, and both hooks failed.
        Assert.NotEmpty(driver.Failures);
        Assert.All(driver.Failures, f => Assert.Same(throwing, f.Check));
        Assert.Contains(driver.Failures, f => f.Exception.Message == "boom in Finish");

        // The recorder itself never failed.
        Assert.DoesNotContain(driver.Failures, f => ReferenceEquals(f.Check, recorder));
    }
}
