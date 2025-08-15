using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using AppRefiner.Database;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    /// <summary>
    /// Styler that checks for invalid application class references
    /// </summary>
    public class InvalidAppClass : BaseStyler
    {
        private const uint ERROR_COLOR = 0x0000FF60; // Red color for invalid app classes
        
        // Dictionary cache to store app class path validity status (true = valid, false = invalid)
        private static Dictionary<string, bool> AppClassValidity = new();
        
        public InvalidAppClass()
        {
            Description = "Highlights invalid Application Class references";
            Active = true;
        }
        
        /// <summary>
        /// Specifies that this styler requires a database connection
        /// </summary>
        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Required;
        
        /// <summary>
        /// Reset the styler's state, but keep the static cache of valid app class paths
        /// </summary>
        public override void Reset()
        {
            base.Reset();
        }
        
        /// <summary>
        /// Clear the cache of app class path validity status
        /// This should be called when the active editor changes or is set to null
        /// </summary>
        public static void ClearValidAppClassPathsCache()
        {
            AppClassValidity.Clear();
        }
        
        public override void EnterAppClassPath([NotNull] AppClassPathContext context)
        {
            if (context == null || DataManager == null) return;
            
            string appClassPath = context.GetText();
            bool isValid;
            
            // Check if this app class path is already in our cache
            if (AppClassValidity.TryGetValue(appClassPath, out isValid))
            {
                // We already know the validity status
            }
            else
            {
                // Check if app class exists
                isValid = DataManager.CheckAppClassExists(appClassPath);
                
                // Add to cache for future lookups
                AppClassValidity[appClassPath] = isValid;
            }
            
            // If the app class is invalid, highlight it with an error
            if (!isValid)
            {
                AddIndicator(
                    context, 
                    IndicatorType.SQUIGGLE, 
                    ERROR_COLOR,
                    $"Invalid Application Class path: {appClassPath}"
                );
            }
        }
    }
} 