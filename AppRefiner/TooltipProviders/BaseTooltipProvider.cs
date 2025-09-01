namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Base interface for all tooltip providers in the system.
    /// </summary>
    public interface ITooltipProvider
    {
        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Determines if this provider is active and should process tooltips.
        /// </summary>
        bool Active { get; set; }

        /// <summary>
        /// Priority of this tooltip provider. Higher priority providers are called first.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Attempts to get a tooltip for the current position in the editor.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        /// <param name="position">The position in the document where to show the tooltip.</param>
        /// <returns>Tooltip text if available, null otherwise.</returns>
        string? GetTooltip(ScintillaEditor editor, int position);

        /// <summary>
        /// Called when the tooltip is hidden. Can be used for cleanup.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        void OnHideTooltip(ScintillaEditor editor);
    }

    /// <summary>
    /// Abstract base class for simple tooltip providers.
    /// </summary>
    public abstract class BaseTooltipProvider : ITooltipProvider
    {
        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Determines if this provider is active and should process tooltips.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Priority of this tooltip provider. Higher priority providers are called first.
        /// </summary>
        public virtual int Priority { get; } = 0;

        /// <summary>
        /// Attempts to get a tooltip for the current position in the editor.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        /// <param name="position">The position in the document where to show the tooltip.</param>
        /// <returns>Tooltip text if available, null otherwise.</returns>
        public abstract string? GetTooltip(ScintillaEditor editor, int position);

        /// <summary>
        /// Called when the tooltip is hidden. Can be used for cleanup.
        /// </summary>
        /// <param name="editor">The Scintilla editor instance.</param>
        public virtual void OnHideTooltip(ScintillaEditor editor)
        {
            // Default implementation does nothing
        }
    }
}