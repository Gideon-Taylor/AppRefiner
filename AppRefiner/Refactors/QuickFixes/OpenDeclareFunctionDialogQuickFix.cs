namespace AppRefiner.Refactors.QuickFixes
{
    /// <summary>
    /// Marker type for the "Search for function 'X'..." quick-fix entry. Never
    /// instantiated: AutoCompleteService.HandleQuickFixSelection intercepts this type
    /// and opens the Declare Function dialog pre-filled with the function name that
    /// rides in the entry's context payload.
    /// </summary>
    public sealed class OpenDeclareFunctionDialogQuickFix
    {
        private OpenDeclareFunctionDialogQuickFix() { }
    }
}
