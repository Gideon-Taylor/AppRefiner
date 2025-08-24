using System;

namespace PeopleCodeParser.SelfHosted.Scoped.Models;

/// <summary>
/// Represents a visual indicator that can be applied to code in an editor.
/// This is a simplified version of the AppRefiner's Indicator class.
/// </summary>
public class Indicator
{
    public enum IndicatorType
    {
        SQUIGGLE,
        TEXTCOLOR,
        BACKGROUND,
        OUTLINE
    }

    public int Start { get; set; }
    public int Length { get; set; }
    public IndicatorType Type { get; set; }
    public uint Color { get; set; }
    public string? Tooltip { get; set; }
    public List<(Type RefactorClass, string Description)> QuickFixes { get; set; } = new();

    public override string ToString() => 
        $"{Type} at {Start}-{Start + Length} ({Length} chars): {Tooltip ?? "No tooltip"}";
}
