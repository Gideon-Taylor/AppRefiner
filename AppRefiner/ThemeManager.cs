using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AppRefiner
{
    /// <summary>
    /// Defines available icon themes for autocomplete suggestions
    /// </summary>
    public enum Theme
    {
        Default,
        Hierarchy,
        Hybrid,
        Monogram,
        Semantic,
        Terminal,
        Alphabet,
        Developer,
        Geometric
    }

    /// <summary>
    /// Defines the visual style of icons (outline or filled)
    /// </summary>
    public enum ThemeStyle
    {
        Outline,
        Filled
    }

    /// <summary>
    /// Manages theme application for autocomplete icons across AppDesigner processes.
    /// Handles loading theme-specific RGBA images and writing them to process memory buffers.
    /// </summary>
    public static class ThemeManager
    {
        /// <summary>
        /// Applies the specified theme and style to an AppDesigner process.
        /// If icon buffers already exist, they are reused; otherwise new buffers are allocated.
        /// </summary>
        /// <param name="process">The AppDesigner process to apply the theme to</param>
        /// <param name="theme">The icon theme to apply</param>
        /// <param name="style">The icon style (outline or filled)</param>
        /// <returns>True if all icons were successfully loaded and written; false otherwise</returns>
        public static bool ApplyTheme(AppDesignerProcess process, Theme theme, ThemeStyle style)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            int successCount = 0;
            var iconNames = Enum.GetNames(typeof(AppDesignerProcess.AutoCompleteIcons));

            foreach (var iconName in iconNames)
            {
                try
                {
                    // Extract the icon data for this theme/style combination
                    byte[] iconData = ExtractIcon(iconName, theme, style);

                    if (iconData == null || iconData.Length == 0)
                    {
                        Debug.Log($"ThemeManager: Failed to extract icon '{iconName}' for theme '{theme}_{style}'");
                        continue;
                    }

                    // Always allocate a new buffer to ensure fresh data
                    IntPtr newBuffer = process.GetStandaloneProcessBuffer((uint)iconData.Length);

                    if (newBuffer == IntPtr.Zero)
                    {
                        Debug.Log($"ThemeManager: Failed to allocate buffer for icon '{iconName}'");
                        continue;
                    }

                    // Write the new icon data to the new buffer
                    bool writeSuccess = WinApi.WriteProcessMemory(
                        process.ProcessHandle,
                        newBuffer,
                        iconData,
                        iconData.Length,
                        out int bytesWritten);

                    if (writeSuccess && bytesWritten == iconData.Length)
                    {
                        // Check if there was an old buffer and free it
                        if (process.iconBuffers.TryGetValue(iconName, out IntPtr oldBuffer) && oldBuffer != IntPtr.Zero)
                        {
                            process.FreeStandaloneProcessBuffer(oldBuffer);
                            Debug.Log($"ThemeManager: Freed old buffer for icon '{iconName}'");
                        }

                        // Update the dictionary with the new buffer
                        process.iconBuffers[iconName] = newBuffer;
                        successCount++;
                        Debug.Log($"ThemeManager: Successfully applied new buffer for icon '{iconName}'");
                    }
                    else
                    {
                        // Free the new buffer if write failed
                        process.FreeStandaloneProcessBuffer(newBuffer);
                        Debug.Log($"ThemeManager: Failed to write to new buffer for icon '{iconName}'");
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log($"ThemeManager: Exception processing icon '{iconName}': {ex.Message}");
                }
            }

            bool allSuccess = successCount == iconNames.Length;
            Debug.Log($"ThemeManager: Applied theme '{theme}' with style '{style}' - {successCount}/{iconNames.Length} icons succeeded");
            return allSuccess;
        }

        /// <summary>
        /// Extracts an icon from embedded resources based on theme and style.
        /// Resource naming pattern: AppRefiner.Themes.icons.{theme}_{style}.{iconName}.rgba
        /// Example: AppRefiner.Themes.icons.default_filled.ClassMethod.rgba
        /// </summary>
        /// <param name="iconName">Name of the icon (from AutoCompleteIcons enum)</param>
        /// <param name="theme">The theme to use</param>
        /// <param name="style">The style to use (outline or filled)</param>
        /// <returns>Byte array containing the RGBA image data, or null if not found</returns>
        private static byte[] ExtractIcon(string iconName, Theme theme, ThemeStyle style)
        {
            var assembly = typeof(ThemeManager).Assembly;

            // Build resource name: AppRefiner.Themes.icons.{lowercase_theme}_{lowercase_style}.{IconName}.rgba
            string themePart = theme.ToString().ToLowerInvariant();
            string stylePart = style.ToString().ToLowerInvariant();
            string resourceName = $"AppRefiner.Themes.icons.{themePart}_{stylePart}.{iconName}.rgba";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                Debug.Log($"ThemeManager: Embedded resource '{resourceName}' not found in assembly");
                return null;
            }

            using MemoryStream ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Gets a list of all available themes
        /// </summary>
        public static IEnumerable<Theme> GetAvailableThemes()
        {
            return Enum.GetValues(typeof(Theme)).Cast<Theme>();
        }

        /// <summary>
        /// Gets a list of all available theme styles
        /// </summary>
        public static IEnumerable<ThemeStyle> GetAvailableStyles()
        {
            return Enum.GetValues(typeof(ThemeStyle)).Cast<ThemeStyle>();
        }

        /// <summary>
        /// Validates that all icons for a given theme and style exist as embedded resources
        /// </summary>
        /// <param name="theme">Theme to validate</param>
        /// <param name="style">Style to validate</param>
        /// <returns>List of missing icon resource names, empty if all exist</returns>
        public static List<string> ValidateThemeResources(Theme theme, ThemeStyle style)
        {
            var missingResources = new List<string>();
            var assembly = typeof(ThemeManager).Assembly;
            var iconNames = Enum.GetNames(typeof(AppDesignerProcess.AutoCompleteIcons));

            string themePart = theme.ToString().ToLowerInvariant();
            string stylePart = style.ToString().ToLowerInvariant();

            foreach (var iconName in iconNames)
            {
                string resourceName = $"AppRefiner.Themes.icons.{themePart}_{stylePart}.{iconName}.rgba";
                using var stream = assembly.GetManifestResourceStream(resourceName);

                if (stream == null)
                {
                    missingResources.Add(resourceName);
                }
            }

            return missingResources;
        }
    }
}
