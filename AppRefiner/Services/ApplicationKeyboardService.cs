using AppRefiner.Commands;

namespace AppRefiner.Services
{
    public class ApplicationKeyboardService : IShortcutRegistrar, IDisposable
    {
        private readonly Dictionary<string, ShortcutRegistration> _registeredShortcuts;
        private bool _disposed = false;

        public ApplicationKeyboardService()
        {
            _registeredShortcuts = new Dictionary<string, ShortcutRegistration>();
        }

        public bool RegisterShortcut(string name, ModifierKeys modifiers, Keys key, Action action)
        {
            if (string.IsNullOrEmpty(name) || action == null)
                return false;

            // Check for collision
            var combination = new KeyCombination(modifiers, key);
            foreach (var existing in _registeredShortcuts.Values)
            {
                if (existing.Combination.Equals(combination))
                {
                    // Collision detected
                    return false;
                }
            }

            // Register the shortcut
            var registration = new ShortcutRegistration(name, combination, action);
            _registeredShortcuts[name] = registration;

            return true;
        }

        public void UnregisterShortcut(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            _registeredShortcuts.Remove(name);
        }

        /// <summary>
        /// Check if a keyboard shortcut combination is available for registration.
        /// Part of IShortcutRegistrar interface for plugin command support.
        /// </summary>
        public bool IsShortcutAvailable(ModifierKeys modifiers, Keys key)
        {
            var combination = new KeyCombination(modifiers, key);

            foreach (var existing in _registeredShortcuts.Values)
            {
                if (existing.Combination.Equals(combination))
                {
                    return false; // Already registered
                }
            }

            return true; // Available
        }

        /// <summary>
        /// Try to register a keyboard shortcut for a command.
        /// Part of IShortcutRegistrar interface for plugin command support.
        /// </summary>
        public bool TryRegisterShortcut(string commandId, ModifierKeys modifiers, Keys key, Action action)
        {
            if (!IsShortcutAvailable(modifiers, key))
            {
                Debug.Log($"Shortcut {GetShortcutDisplayText(modifiers, key)} already registered, cannot register for {commandId}");
                return false;
            }

            return RegisterShortcut(commandId, modifiers, key, action);
        }

        /// <summary>
        /// Get a formatted string representation of a shortcut for display purposes.
        /// Part of IShortcutRegistrar interface for plugin command support.
        /// </summary>
        public string GetShortcutDisplayText(ModifierKeys modifiers, Keys key)
        {
            var parts = new List<string>();

            if (modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");

            parts.Add(key.ToString());

            return string.Join("+", parts);
        }

        public bool ProcessKeyMessage(int combinedParam)
        {
            // Extract modifier flags and virtual key code
            var modifierFlags = (combinedParam >> 16) & 0xFFFF;
            var virtualKeyCode = combinedParam & 0xFFFF;

            // Convert to our enum types (bit flags match C++ implementation)
            ModifierKeys modifiers = 0;
            if ((modifierFlags & 0x1) != 0) modifiers |= ModifierKeys.Control;  // bit 16
            if ((modifierFlags & 0x2) != 0) modifiers |= ModifierKeys.Shift;    // bit 17
            if ((modifierFlags & 0x4) != 0) modifiers |= ModifierKeys.Alt;      // bit 18

            var key = (Keys)virtualKeyCode;
            var combination = new KeyCombination(modifiers, key);

            // Find matching shortcut and execute
            foreach (var registration in _registeredShortcuts.Values)
            {
                if (registration.Combination.Equals(combination))
                {
                    try
                    {
                        Task.Delay(100).ContinueWith(_ =>
                        {
                            registration.Action.Invoke();
                        });
                        return true; // Handled
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"Error executing shortcut {registration.Name}: {ex.Message}");
                    }
                }
            }

            return false; // Not handled
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _registeredShortcuts.Clear();
                _disposed = true;
            }
        }

        private class ShortcutRegistration
        {
            public string Name { get; }
            public KeyCombination Combination { get; }
            public Action Action { get; }

            public ShortcutRegistration(string name, KeyCombination combination, Action action)
            {
                Name = name;
                Combination = combination;
                Action = action;
            }
        }

        private struct KeyCombination : IEquatable<KeyCombination>
        {
            public ModifierKeys Modifiers { get; }
            public Keys Key { get; }

            public KeyCombination(ModifierKeys modifiers, Keys key)
            {
                Modifiers = modifiers;
                Key = key;
            }

            public bool Equals(KeyCombination other)
            {
                return Modifiers == other.Modifiers && Key == other.Key;
            }

            public override bool Equals(object? obj)
            {
                return obj is KeyCombination other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Modifiers, Key);
            }
        }
    }
}