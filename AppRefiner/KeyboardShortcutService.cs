using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics; // Added for Debug.Log

namespace AppRefiner
{
    /// <summary>
    /// Manages global keyboard shortcuts using KeyboardHook.
    /// </summary>
    public class KeyboardShortcutService : IDisposable
    {
        // Store hook, action, and the key combination itself for removal
        private readonly Dictionary<string, (KeyboardHook Hook, Action Action, ModifierKeys Modifiers, Keys Key)> registeredHooks 
            = new Dictionary<string, (KeyboardHook Hook, Action Action, ModifierKeys Modifiers, Keys Key)>();
            
        // Track combinations to prevent collisions
        private readonly HashSet<(ModifierKeys Modifiers, Keys Key)> usedCombinations 
            = new HashSet<(ModifierKeys Modifiers, Keys Key)>();

        /// <summary>
        /// Registers a global keyboard shortcut.
        /// </summary>
        /// <param name="name">A unique name for the shortcut (used for potential unregistration).</param>
        /// <param name="modifiers">The modifier keys (Ctrl, Alt, Shift, Win).</param>
        /// <param name="key">The key to associate with the modifiers.</param>
        /// <param name="action">The action to execute when the shortcut is pressed.</param>
        /// <returns>True if registration was successful, false otherwise (e.g., if the name or key combination is already registered).</returns>
        public bool RegisterShortcut(string name, ModifierKeys modifiers, Keys key, Action action)
        {
            if (key == Keys.None) return false; // Cannot register Keys.None
            if (registeredHooks.ContainsKey(name))
            {
                Debug.Log($"Shortcut registration failed: Name '{name}' already exists.");
                return false;
            }

            var combination = (Modifiers: modifiers, Key: key);
            if (usedCombinations.Contains(combination))
            {
                // Find the name of the existing shortcut for a better message (optional but helpful)
                string existingName = "another shortcut";
                foreach(var kvp in registeredHooks)
                {
                    if(kvp.Value.Modifiers == modifiers && kvp.Value.Key == key)
                    {
                        existingName = $"'{kvp.Key}'";
                        break;
                    }
                }
                Debug.Log($"Shortcut registration failed for '{name}': Combination {modifiers}+{key} is already used by {existingName}.");
                return false;
            }

            var hook = new KeyboardHook();
            hook.KeyPressed += (sender, e) => action?.Invoke();
            
            try
            {
                hook.RegisterHotKey(modifiers, key);
                // Store all info
                registeredHooks[name] = (hook, action, modifiers, key);
                // Add combination to tracking set
                usedCombinations.Add(combination);
                Debug.Log($"Registered shortcut '{name}': {modifiers}+{key}");
                return true; // Assume success if RegisterHotKey doesn't throw and we add it
            }
            catch (Exception ex) // Catch potential exceptions during registration
            {
                Debug.Log($"Failed to register hotkey '{name}' ({modifiers}+{key}): {ex.Message}");
                hook.Dispose(); // Clean up hook if registration failed
                return false;
            }
        }

        /// <summary>
        /// Unregisters a specific shortcut by name.
        /// </summary>
        /// <param name="name">The unique name of the shortcut to unregister.</param>
        public void UnregisterShortcut(string name)
        {
            if (registeredHooks.TryGetValue(name, out var hookInfo))
            {
                var combination = (Modifiers: hookInfo.Modifiers, Key: hookInfo.Key);
                
                hookInfo.Hook.Dispose(); // Dispose the hook first
                registeredHooks.Remove(name); // Remove from dictionary
                usedCombinations.Remove(combination); // Remove from tracking set
                 Debug.Log($"Unregistered shortcut '{name}': {combination.Modifiers}+{combination.Key}");
            }
        }

        /// <summary>
        /// Disposes all registered keyboard hooks.
        /// </summary>
        public void Dispose()
        {
            foreach (var hookInfo in registeredHooks.Values)
            {
                hookInfo.Hook.Dispose();
            }
            registeredHooks.Clear();
            usedCombinations.Clear(); // Clear the tracking set
            GC.SuppressFinalize(this);
        }

        ~KeyboardShortcutService()
        {
            Dispose();
        }
    }
} 