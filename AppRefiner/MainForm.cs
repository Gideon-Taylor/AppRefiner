using AppRefiner.Commands;
using AppRefiner.Commands.BuiltIn;
using AppRefiner.Database;
using AppRefiner.Database.Models;
using AppRefiner.Dialogs;
using AppRefiner.Events;
using AppRefiner.LanguageExtensions;
using AppRefiner.Linters;
using AppRefiner.Plugins;
using AppRefiner.Properties;
using AppRefiner.Refactors;
using AppRefiner.Services;
using AppRefiner.Snapshots;
using AppRefiner.Stylers;
using AppRefiner.Templates;
using AppRefiner.TooltipProviders;
using DiffPlex.Model;
using Microsoft.Win32;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Inference;
using PeopleCodeTypeInfo.Types;
using System.Data;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using static AppRefiner.AutoCompleteService;
using static AppRefiner.ScintillaEditor;

namespace AppRefiner
{
    public partial class MainForm : Form
    {
        // P/Invoke declarations for sending keystrokes to App Designer
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_CONTROL = 0x11;
        private const int VK_O = 0x4F;

        // Services for handling OS-level interactions
        private WinEventService? winEventService;
        private ApplicationKeyboardService? applicationKeyboardService;
        private DialogCenteringService? dialogCenteringService;
        private StackTraceNavigatorDialog? stackTraceNavigatorDialog;

        // HashSet to track process IDs that already have AppDesignerProcess objects created
        private readonly HashSet<uint> trackedProcessIds = new();
        private LinterManager? linterManager; // Added LinterManager
        private StylerManager? stylerManager; // Added StylerManager
        private AutoCompleteService? autoCompleteService; // Added AutoCompleteService
        private RefactorManager? refactorManager; // Added RefactorManager
        private SettingsService? settingsService; // Added SettingsService
        private FunctionCacheManager? functionCacheManager;
        private CommandManager? commandManager; // Added CommandManager for plugin commands
        private TypeExtensionManager? languageExtensionManager; // Added LanguageExtensionManager
        private AutoSuggestSettings autoSuggestSettings = AutoSuggestSettings.GetDefault(); // Auto suggest configuration 
        private ScintillaEditor? activeEditor = null;
        private AppDesignerProcess? activeAppDesigner = null;
        private Dictionary<uint, AppDesignerProcess> AppDesignerProcesses = [];
        private Dictionary<string, (int FirstLine, int CursorPosition)> lastKnownPositions = new();

        // Current shortcut flags state - tracks which shortcuts are enabled
        private EventHookInstaller.ShortcutType currentShortcutFlags = EventHookInstaller.ShortcutType.All;

        // Flag to track if autocomplete conflict warning has been shown this session
        private bool hasShownAutocompleteConflictWarning = false;

        /// <summary>
        /// Gets the currently active editor
        /// </summary>
        public ScintillaEditor? ActiveEditor => activeEditor;
        /// <summary>
        /// Gets the currently active AppDesigner process
        /// </summary>
        public AppDesignerProcess? ActiveAppDesigner => activeAppDesigner;
        /// <summary>
        /// Gets the language extension manager
        /// </summary>
        public TypeExtensionManager? TypeExtensionManager => languageExtensionManager;

        private List<BaseTooltipProvider> tooltipProviders = new();

        // Static list of available commands
        public static List<Command> AvailableCommands = new();

        // Path for linting report output
        private string? lintReportPath;
        private string? TNS_ADMIN;
        private const int WM_SCN_EVENT_MASK = 0x7000;
        private const int SCN_DWELLSTART = 2016;
        private const int SCN_DWELLEND = 2017;
        private const int SCN_SAVEPOINTREACHED = 2002;
        private const int AR_APP_PACKAGE_SUGGEST = 2500; // New constant for app package suggest
        private const int AR_CREATE_SHORTHAND = 2501; // New constant for create shorthand detection
        private const int AR_TYPING_PAUSE = 2502; // New constant for typing pause detection
        private const int AR_BEFORE_DELETE_ALL = 2503; // New constant for before delete all detection
        private const int AR_FOLD_MARGIN_CLICK = 2504;
        private const int AR_CONCAT_SHORTHAND = 2505; // New constant for concat shorthand detection
        private const int AR_TEXT_PASTED = 2506; // New constant for text pasted detection
        private const int AR_KEY_COMBINATION = 2507; // New constant for key combination detection
        private const int AR_MSGBOX_SHORTHAND = 2508;
        private const int AR_VARIABLE_SUGGEST = 2509; // New constant for variable auto suggest when & is typed
        private const int AR_CURSOR_POSITION_CHANGED = 2510; // New constant for cursor position change detection
        public const int AR_FUNCTION_CALL_TIP = 2511; // Function call tip notification for '(', ')', and ',' characters
        private const int AR_OBJECT_MEMBERS = 2512; // Object member suggestions when '.' is typed
        private const int AR_SYSTEM_VARIABLE_SUGGEST = 2513; // System variable suggestions when '%' is typed
        private const int AR_SCINTILLA_ALREADY_LOADED = 2514; // Scintilla DLL is already loaded
        private const int AR_SCINTILLA_LOAD_SUCCESS = 2515; // Scintilla DLL loaded successfully
        private const int AR_SCINTILLA_LOAD_FAILED = 2516; // Scintilla DLL load failed (wParam contains GetLastError)
        private const int AR_SCINTILLA_IN_USE = 2517; // Scintilla DLL in use (active windows exist, cannot replace)
        private const int AR_SCINTILLA_NOT_FOUND = 2518; // Scintilla DLL file not found at specified path (wParam=(major<<16)|minor, lParam=(build<<16)|revision)
        private const int AR_CONTEXT_MENU_OPTION = 2520; // Context menu option selected (wParam=option ID, lParam=toggle state for checkboxes or 0)
        private const int AR_SUBCLASS_RESULTS_LIST = 1007; // Message to subclass Results list view
        private const int AR_SET_OPEN_TARGET = 1008; // Message to set open target for Results list interception

        // Context menu option IDs
        private const int IDM_COMMAND_PALETTE = 1001;
        private const int IDM_MINIMAP = 1002;
        private const int IDM_PARAM_NAMES = 1003;
        private const int SCN_USERLISTSELECTION = 2014; // User list selection notification
        private const int SCN_CALLTIPCLICK = 2021;
        private const int SCN_AUTOCSELECTION = 2022; // Autocompletion selection notification
        private const int SCN_AUTOCCOMPLETED = 2030; // Autocompletion completed notification
        private const int SCI_REPLACESEL = 0x2170; // Constant for SCI_REPLACESEL

        private bool isLoadingSettings = false;

        // Add a private field for the SnapshotManager
        private SnapshotManager? snapshotManager;

        // Fields for editor management
        private Dictionary<ScintillaEditor, DateTime> lastStylerProcessingTime = new();
        private const int STYLER_PROCESSING_DEBOUNCE_MS = 1000; // Prevent duplicate processing within 100ms

        // Throttling for duplicate shortcut prevention
        private DateTime _lastShortcutTime = DateTime.MinValue;
        private const int SHORTCUT_THROTTLE_MS = 300; // Very short window to catch rapid duplicates

        // Fields for debouncing SAVEPOINTREACHED events
        private readonly object savepointLock = new();
        private DateTime lastSavepointTime = DateTime.MinValue;
        private System.Threading.Timer? savepointDebounceTimer = null;
        private ScintillaEditor? pendingSaveEditor = null;
        private const int SAVEPOINT_DEBOUNCE_MS = 300;

        // Fields for debouncing window focus events
        private readonly object focusEventLock = new();
        private System.Threading.Timer? focusDebounceTimer = null;
        private IntPtr pendingFocusHwnd = IntPtr.Zero;
        private const int FOCUS_DEBOUNCE_MS = 150;

        // Fields for Application Designer validation retry mechanism
        private readonly object validationRetryLock = new();
        private readonly Dictionary<uint, System.Threading.Timer> validationRetryTimers = new();
        private readonly Dictionary<uint, int> validationRetryAttempts = new();
        private readonly Dictionary<uint, IntPtr> validationRetryHandles = new();
        private const int VALIDATION_RETRY_BASE_DELAY_MS = 250; // Start with 250ms
        private const int VALIDATION_RETRY_MAX_ATTEMPTS = 10; // Try up to 10 times
        private const int VALIDATION_RETRY_MAX_DELAY_MS = 3000; // Cap at 2 seconds

        // Add instance of the new TemplateManager
        private TemplateManager templateManager = new();

        // Dictionary to keep track of generated UI controls for template parameters
        private Dictionary<string, Control> currentTemplateInputControls = new();

        // Flag to indicate if What's New dialog should be shown
        private readonly bool shouldShowWhatsNew;

        public MainForm(bool shouldShowWhatsNew = false)
        {
            this.shouldShowWhatsNew = shouldShowWhatsNew;
            InitializeComponent();

        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            AppDesignerProcess.CallbackWindow = this.Handle;

            isLoadingSettings = true; // Prevent immediate saves during initial load

            // Instantiate and start services
            settingsService = new SettingsService(); // Instantiate SettingsService first
            dialogCenteringService = new DialogCenteringService(settingsService);
            applicationKeyboardService = new ApplicationKeyboardService();
            winEventService = new WinEventService();
            winEventService.WindowFocused += HandleWindowFocusEvent;
            //winEventService.WindowCreated += HandleWindowCreationEvent;
            winEventService.WindowShown += HandleWindowShownEvent;
            winEventService.Start();

            // Instantiate LinterManager (passing UI elements)
            // LoadGeneralSettings needs lintReportPath BEFORE LinterManager is created
            var generalSettings = settingsService.LoadGeneralSettings();
            chkCodeFolding.Checked = generalSettings.CodeFolding;
            chkInitCollapsed.Checked = generalSettings.InitCollapsed;
            chkOnlyPPC.Checked = generalSettings.OnlyPPC;
            chkBetterSQL.Checked = generalSettings.BetterSQL;
            chkAutoDark.Checked = generalSettings.AutoDark;
            chkAutoPairing.Checked = generalSettings.AutoPair; // Assuming chkAutoPairing corresponds to AutoPair
            chkPromptForDB.Checked = generalSettings.PromptForDB;
            lintReportPath = generalSettings.LintReportPath;
            TNS_ADMIN = generalSettings.TNS_ADMIN;
            chkEventMapping.Checked = generalSettings.CheckEventMapping;
            chkEventMapXrefs.Checked = generalSettings.CheckEventMapXrefs;
            optClassPath.Checked = generalSettings.ShowClassPath;
            optClassText.Checked = generalSettings.ShowClassText;
            chkRememberFolds.Checked = generalSettings.RememberFolds;
            chkOverrideFindReplace.Checked = generalSettings.OverrideFindReplace;
            chkAutoCenterDialogs.Checked = generalSettings.AutoCenterDialogs;
            chkMultiSelection.Checked = generalSettings.MultiSelection;
            chkOverrideOpen.Checked = generalSettings.OverrideOpen;
            chkLineSelectionFix.Checked = generalSettings.LineSelectionFix;
            chkInlineParameterHints.Checked = generalSettings.ShowParamNames;
            chkDocMinimap.Checked = generalSettings.MiniMapOpen;
            chkUseEnhancedEditor.Checked = generalSettings.UseEnhancedEditor;
            chkInlineParameterHints.Enabled = chkUseEnhancedEditor.Checked;
            txtLintReportDir.Text = generalSettings.LintReportPath;
            txtTnsAdminDir.Text = generalSettings.TNS_ADMIN;

            // Initialize theme combo box with all available themes
            cmbTheme.Items.Clear();
            foreach (var theme in Enum.GetNames(typeof(Theme)))
            {
                cmbTheme.Items.Add(theme);
            }

            // Load theme settings
            if (Enum.TryParse<Theme>(generalSettings.Theme, out var selectedTheme))
            {
                cmbTheme.SelectedItem = selectedTheme.ToString();
            }
            else
            {
                cmbTheme.SelectedItem = Theme.Default.ToString();
            }

            chkFilled.Checked = generalSettings.ThemeFilled;

            // Load AutoSuggest settings
            autoSuggestSettings = settingsService.LoadAutoSuggestSettings();
            chkVariableSuggestions.Checked = autoSuggestSettings.VariableSuggestions;
            chkFunctionSignatures.Checked = autoSuggestSettings.FunctionSignatures;
            chkObjectMembers.Checked = autoSuggestSettings.ObjectMembers;
            chkSystemVariables.Checked = autoSuggestSettings.SystemVariables;

            linterManager = new LinterManager(this, dataGridView1, lintReportPath, settingsService);
            linterManager.InitializeLinterOptions(); // Initialize linters via the manager
            dataGridView1.CellPainting += dataGridView1_CellPainting; // Wire up CellPainting

            // Instantiate StylerManager (passing UI elements)
            stylerManager = new StylerManager(this, dataGridView3, settingsService);
            stylerManager.InitializeStylerOptions(); // Initialize stylers via the manager

            // Instantiate AutoCompleteService
            autoCompleteService = new AutoCompleteService(this);

            // Instantiate RefactorManager
            refactorManager = new RefactorManager(this, gridRefactors);
            refactorManager.InitializeRefactorOptions(); // Initialize refactors via the manager

            // Instantiate CommandManager and discover plugin commands
            commandManager = new CommandManager();
            commandManager.DiscoverAndCacheCommands();

            // Instantiate LanguageExtensionManager
            languageExtensionManager = new TypeExtensionManager(this, gridExtensions, settingsService);
            languageExtensionManager.InitializeLanguageExtensions();

            // Set extension manager references
            TooltipManager.ExtensionManager = languageExtensionManager;
            AutoCompleteService.ExtensionManager = languageExtensionManager;

            // Load templates using the manager and populate ComboBox
            templateManager.LoadTemplates();
            cmbTemplates.Items.Clear();
            templateManager.LoadedTemplates.ForEach(t => cmbTemplates.Items.Add(t));
            if (cmbTemplates.Items.Count > 0)
            {
                cmbTemplates.SelectedIndex = 0;
                // Trigger selection change to initialize UI
                CmbTemplates_SelectedIndexChanged(cmbTemplates, EventArgs.Empty);
            }
            cmbTemplates.SelectedIndexChanged += CmbTemplates_SelectedIndexChanged;

            RegisterCommands(commandManager);

            // Wire up keyboard service with context factory so shortcuts get proper context
            applicationKeyboardService?.SetCommandContextFactory(CreateCommandContext);

            // Initialize shortcuts for plugin commands (after RegisterCommands and context factory setup)
            if (applicationKeyboardService != null)
            {
                commandManager?.InitializeCommandShortcuts(applicationKeyboardService);
            }
            // Initialize the tooltip providers
            TooltipManager.Initialize();
            InitTooltipOptions(); // Needs to run before LoadTooltipStates

            // Load Tooltip states using the service
            settingsService.LoadTooltipStates(tooltipProviders, dataGridViewTooltips);

            // Register keyboard shortcuts for Command Palette (keep this one as it's core functionality)
            applicationKeyboardService?.RegisterShortcut("CommandPalette", AppRefiner.ModifierKeys.Control | AppRefiner.ModifierKeys.Shift, Keys.P, ShowCommandPalette);

            // Note: Better Find still uses special handlers for Ctrl+F and Ctrl+H as they have custom behavior
            applicationKeyboardService?.RegisterShortcut("BetterFind", AppRefiner.ModifierKeys.Control, Keys.F, showBetterFindHandler); // Ctrl + F
            applicationKeyboardService?.RegisterShortcut("BetterFindReplace", AppRefiner.ModifierKeys.Control, Keys.H, showBetterFindReplaceHandler); // Ctrl + H

            // Register refactor shortcuts (refactors use dynamic command generation, not BaseCommand classes)
            RegisterRefactorShortcuts();

            // All other shortcuts are now registered through built-in BaseCommand classes via CommandManager


            // Initialize snapshot manager
            snapshotManager = SnapshotManager.CreateFromSettings();

            // Instantiate FunctionCacheManager
            functionCacheManager = FunctionCacheManager.CreateFromSettings();

            // Attach event handlers for immediate save
            AttachEventHandlersForImmediateSave();

            // Initialize shortcut flags based on current settings
            InitializeShortcutFlags();

            isLoadingSettings = false; // Allow immediate saves now

            // Show/hide What's New link based on file existence and assembly version
            UpdateWhatsNewLinkVisibility();

            /* Scan for existing App Designer processes */
            foreach (var proc in Process.GetProcessesByName("pside"))
            {
                ValidateAndCreateAppDesignerProcess((uint)proc.Id, proc.MainWindowHandle);
                //AppDesignerProcess adp = new AppDesignerProcess((uint)proc.Id, resultsList, GetGeneralSettingsObject());
                //AppDesignerProcesses.Add((uint)proc.Id, adp);
            }

            /* Check for new version */
            bool newVersionAvailable = IsNewVersionAvailable();
            this.Height -= splitContainer1.Panel2.Height;
            splitContainer1.Panel2Collapsed = !newVersionAvailable;

        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (shouldShowWhatsNew)
            {
                ShowWhatsNewDialog();
            }

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var currentVersion = assembly.GetName().Version;
            Text += currentVersion != null ? $" - v{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}" : string.Empty;

            // Check for autocomplete conflicts on startup
            CheckAutocompleteConflict();
        }

        private void UpdateWhatsNewLinkVisibility()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version?.ToString();

                // Check if whats-new.txt exists
                var whatsNewPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whats-new.txt");

                // Hide link if version is null or file doesn't exist
                if (version == null || !File.Exists(whatsNewPath))
                {
                    lnkWhatsNew.Visible = false;
                    Debug.Log($"What's New link hidden: version={version ?? "null"}, file exists={File.Exists(whatsNewPath)}");
                }
                else
                {
                    lnkWhatsNew.Visible = true;
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error updating What's New link visibility: {ex.Message}");
                lnkWhatsNew.Visible = false; // Hide on error
            }
        }

        private void ShowWhatsNewDialog()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                var currentVersion = assembly.GetName().Version;
                var version = currentVersion != null ? $" - v{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}" : string.Empty;
                // Don't show dialog if version is null
                if (version == null)
                {
                    Debug.Log("Cannot show What's New dialog: Assembly version is null");
                    return;
                }

                // Check if whats-new.txt exists before creating dialog
                var whatsNewPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whats-new.txt");
                if (!File.Exists(whatsNewPath))
                {
                    Debug.Log($"Cannot show What's New dialog: whats-new.txt not found at {whatsNewPath}");
                    return;
                }

                var whatsNewDialog = new WhatsNewDialog(version, this.Handle);
                var result = whatsNewDialog.ShowDialog(new WindowWrapper(this.Handle));

                // If user checked "Don't show again", save the preference
                if (whatsNewDialog.DontShowAgain)
                {
                    Properties.Settings.Default.ShowWhatsNewDialog = false;
                    Properties.Settings.Default.Save();
                    Debug.Log("User disabled What's New dialog");
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error showing What's New dialog: {ex.Message}");
                // Silently fail - don't show dialog if any error occurs
            }
        }

        private bool IsNewVersionAvailable()
        {
            try
            {
                // Get current assembly version
                var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (currentVersion == null)
                {
                    Debug.Log("IsNewVersionAvailable: Current assembly version is null");
                    return false;
                }

                // Fetch latest release from GitHub API
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "AppRefiner");
                client.Timeout = TimeSpan.FromSeconds(5); // 5 second timeout to avoid hanging

                var response = Task.Run(async () =>
                    await client.GetAsync("https://api.github.com/repos/Gideon-Taylor/AppRefiner/releases?per_page=1&page=1")
                ).Result;

                if (!response.IsSuccessStatusCode)
                {
                    Debug.Log($"IsNewVersionAvailable: GitHub API request failed with status {response.StatusCode}");
                    return false;
                }

                var json = Task.Run(async () => await response.Content.ReadAsStringAsync()).Result;

                // Parse JSON array
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                {
                    Debug.Log("IsNewVersionAvailable: No releases found");
                    return false;
                }

                // Get first release (most recent)
                var latestRelease = root[0];

                if (!latestRelease.TryGetProperty("tag_name", out var tagNameElement))
                {
                    Debug.Log("IsNewVersionAvailable: tag_name property not found");
                    return false;
                }

                var tagName = tagNameElement.GetString();
                if (string.IsNullOrEmpty(tagName))
                {
                    Debug.Log("IsNewVersionAvailable: tag_name is null or empty");
                    return false;
                }

                // Remove 'v' prefix if present (e.g., "v1.2.3.4" -> "1.2.3.4")
                if (tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                {
                    tagName = tagName.Substring(1);
                }

                // Parse as Version
                if (!Version.TryParse(tagName, out var latestVersion))
                {
                    Debug.Log($"IsNewVersionAvailable: Could not parse tag_name '{tagName}' as Version");
                    return false;
                }

                // Compare versions
                var isNewer = latestVersion > currentVersion;
                Debug.Log($"IsNewVersionAvailable: Current version {currentVersion}, Latest version {latestVersion}, Newer: {isNewer}");

                return isNewer;
            }
            catch (Exception ex)
            {
                Debug.Log($"IsNewVersionAvailable: Exception occurred: {ex.Message}");
                return false;
            }
        }

        private void AttachEventHandlersForImmediateSave()
        {
            chkCodeFolding.CheckedChanged += GeneralSetting_Changed;
            chkInitCollapsed.CheckedChanged += GeneralSetting_Changed;
            chkOnlyPPC.CheckedChanged += GeneralSetting_Changed;
            chkBetterSQL.CheckedChanged += GeneralSetting_Changed;
            chkAutoDark.CheckedChanged += GeneralSetting_Changed;
            chkAutoPairing.CheckedChanged += GeneralSetting_Changed;
            chkPromptForDB.CheckedChanged += GeneralSetting_Changed;
            chkEventMapping.CheckedChanged += GeneralSetting_Changed;
            chkEventMapXrefs.CheckedChanged += GeneralSetting_Changed;
            optClassPath.CheckedChanged += GeneralSetting_Changed;
            optClassText.CheckedChanged += GeneralSetting_Changed;
            chkRememberFolds.CheckedChanged += GeneralSetting_Changed;
            chkOverrideFindReplace.CheckedChanged += GeneralSetting_Changed;
            chkOverrideOpen.CheckedChanged += GeneralSetting_Changed;
            chkAutoCenterDialogs.CheckedChanged += GeneralSetting_Changed;
            chkMultiSelection.CheckedChanged += GeneralSetting_Changed;
            chkLineSelectionFix.CheckedChanged += GeneralSetting_Changed;
            chkInlineParameterHints.CheckedChanged += GeneralSetting_Changed;
            chkDocMinimap.CheckedChanged += GeneralSetting_Changed;
            chkUseEnhancedEditor.CheckedChanged += GeneralSetting_Changed;

            // Theme controls
            cmbTheme.SelectedIndexChanged += ThemeSetting_Changed;
            chkFilled.CheckedChanged += ThemeSetting_Changed;

            // AutoSuggest checkboxes
            chkVariableSuggestions.CheckedChanged += AutoSuggestSetting_Changed;
            chkFunctionSignatures.CheckedChanged += AutoSuggestSetting_Changed;
            chkObjectMembers.CheckedChanged += AutoSuggestSetting_Changed;
            chkSystemVariables.CheckedChanged += AutoSuggestSetting_Changed;

            // DataGridViews CellValueChanged events will also call SaveSettings
        }

        private void GeneralSetting_Changed(object? sender, EventArgs e)
        {
            if (isLoadingSettings) return;

            // Enhanced editor disclaimer — only when toggling ON
            if (sender == chkUseEnhancedEditor)
            {
                if (chkUseEnhancedEditor.Checked)
                {
                    var disclaimer = new EnhancedEditorDisclaimerDialog(this.Handle);
                    var result = disclaimer.ShowDialog(new WindowWrapper(this.Handle));

                    if (result == DialogResult.OK)
                    {
                        chkInlineParameterHints.Enabled = true;
                    }
                    else
                    {
                        // User declined — revert without re-triggering this handler
                        isLoadingSettings = true;
                        chkUseEnhancedEditor.Checked = false;
                        chkInlineParameterHints.Checked = false;
                        chkInlineParameterHints.Enabled = false;
                        isLoadingSettings = false;
                        SaveSettings();
                        return;
                    }
                }
                else
                {
                    // Enhanced editor unchecked directly — disable parameter hints
                    isLoadingSettings = true;
                    chkInlineParameterHints.Checked = false;
                    chkInlineParameterHints.Enabled = false;
                    isLoadingSettings = false;
                }
            }

            // Check if this is a shortcut-related checkbox change
            if (sender == chkOverrideOpen || sender == chkOverrideFindReplace || sender == chkLineSelectionFix)
            {
                UpdateShortcutFlags();
            }
            if (sender == chkOverrideOpen)
            {
                btnConfigSmartOpen.Enabled = chkOverrideOpen.Checked;
            }

            SaveSettings(); // Call the consolidated SaveSettings method
        }

        private void AutoSuggestSetting_Changed(object? sender, EventArgs e)
        {
            if (isLoadingSettings) return;

            // Update the in-memory settings object
            autoSuggestSettings.VariableSuggestions = chkVariableSuggestions.Checked;
            autoSuggestSettings.FunctionSignatures = chkFunctionSignatures.Checked;
            autoSuggestSettings.ObjectMembers = chkObjectMembers.Checked;
            autoSuggestSettings.SystemVariables = chkSystemVariables.Checked;

            // Save to settings service
            settingsService?.SaveAutoSuggestSettings(autoSuggestSettings);
            settingsService?.SaveChanges();

            // Check for autocomplete conflicts after settings change
            CheckAutocompleteConflict();
        }

        /// <summary>
        /// Checks if any AppRefiner autocomplete features are enabled
        /// </summary>
        private bool IsAppRefinerAutocompleteEnabled()
        {
            return autoSuggestSettings.VariableSuggestions ||
                   autoSuggestSettings.FunctionSignatures ||
                   autoSuggestSettings.ObjectMembers ||
                   autoSuggestSettings.SystemVariables;
        }

        /// <summary>
        /// Checks if Application Designer's delivered AutoSuggest feature is enabled via registry
        /// </summary>
        private bool IsApplicationDesignerAutocompleteEnabled()
        {
            try
            {
                // PeopleSoft Application Designer stores settings in:
                // HKEY_CURRENT_USER\Software\PeopleSoft\Application Designer\Settings
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\PeopleSoft\PeopleTools\Release8.40\PSIDE"))
                {
                    if (key != null)
                    {
                        // The setting name is "EnableAutoCompletion" - it's a DWORD value
                        // 1 = enabled, 0 = disabled
                        object? value = key.GetValue("EnableAutoCompletion");
                        if (value != null && value is int intValue)
                        {
                            return intValue == 1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error checking Application Designer AutoSuggest registry setting");
            }

            // If we can't read the registry or the key doesn't exist, assume it's not enabled
            return false;
        }

        /// <summary>
        /// Checks for conflicts between AppRefiner and Application Designer autocomplete features
        /// and displays a warning dialog if both are enabled (only once per session)
        /// </summary>
        private void CheckAutocompleteConflict()
        {
            // Only check if AppRefiner autocomplete is enabled
            if (!IsAppRefinerAutocompleteEnabled())
            {
                return;
            }

            // Check if Application Designer's autocomplete is also enabled
            if (IsApplicationDesignerAutocompleteEnabled())
            {
                // Only show the warning once per session
                if (hasShownAutocompleteConflictWarning)
                {
                    return;
                }

                // Mark that we've shown the warning for this session
                hasShownAutocompleteConflictWarning = true;

                // Show warning dialog
                Task.Delay(100).ContinueWith(_ =>
                {
                    try
                    {
                        string message = "AppRefiner has detected that both AppRefiner's Auto Suggest feature and " +
                                       "Application Designer's delivered AutoSuggest feature are currently enabled.\n\n" +
                                       "These two features conflict with each other and can cause unexpected behavior. " +
                                       "We highly recommend disabling one of them.\n\n" +
                                       "To disable Application Designer's AutoSuggest:\n" +
                                       "1. In Application Designer, go to Tools → Options...\n" +
                                       "2. Click the Editor tab\n" +
                                       "3. Uncheck 'Enable AutoSuggest'\n" +
                                       "4. Click OK\n\n" +
                                       "Alternatively, you can disable AppRefiner's Auto Suggest features in the Settings tab.";

                        var mainHandle = IntPtr.Zero;
                        if (activeAppDesigner != null)
                        {
                            var process = Process.GetProcessById((int)activeAppDesigner.ProcessId);
                            mainHandle = process.MainWindowHandle;
                        }

                        if (mainHandle != IntPtr.Zero)
                        {
                            var handleWrapper = new WindowWrapper(mainHandle);
                            new MessageBoxDialog(
                                message,
                                "AutoComplete Feature Conflict Detected",
                                MessageBoxButtons.OK,
                                mainHandle
                            ).ShowDialog(handleWrapper);
                        }
                        else
                        {
                            // Fallback to showing on the MainForm if no active app designer
                            this.Invoke(() =>
                            {
                                new MessageBoxDialog(
                                    message,
                                    "AutoComplete Feature Conflict Detected",
                                    MessageBoxButtons.OK,
                                    this.Handle
                                ).ShowDialog(new WindowWrapper(this.Handle));
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex, "Error showing autocomplete conflict dialog");
                    }
                });
            }
        }

        /// <summary>
        /// Handles changes to theme-related settings (combo box or checkbox)
        /// </summary>
        private void ThemeSetting_Changed(object? sender, EventArgs e)
        {
            if (isLoadingSettings) return;
            if (cmbTheme.SelectedItem == null) return;

            // Parse the selected theme
            if (!Enum.TryParse<Theme>(cmbTheme.SelectedItem.ToString(), out var selectedTheme))
            {
                Debug.Log("Invalid theme selected");
                return;
            }

            // Determine the theme style based on checkbox
            var themeStyle = chkFilled.Checked ? ThemeStyle.Filled : ThemeStyle.Outline;

            Debug.Log($"Applying theme: {selectedTheme} with style: {themeStyle}");

            // Apply the theme to all known AppDesigner processes
            foreach (var appDesigner in AppDesignerProcesses.Values)
            {
                try
                {
                    bool success = ThemeManager.ApplyTheme(appDesigner, selectedTheme, themeStyle);
                    if (success)
                    {
                        Debug.Log($"Successfully applied theme to process {appDesigner.ProcessId}");
                    }
                    else
                    {
                        Debug.Log($"Failed to fully apply theme to process {appDesigner.ProcessId}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, $"Error applying theme to process {appDesigner.ProcessId}");
                }
            }

            // Save the settings
            SaveSettings();
        }

        /// <summary>
        /// Applies the current theme settings to a newly created AppDesigner process
        /// </summary>
        /// <param name="process">The AppDesigner process to apply the theme to</param>
        private void ApplyCurrentThemeToProcess(AppDesignerProcess process)
        {
            if (process == null || cmbTheme.SelectedItem == null) return;

            if (Enum.TryParse<Theme>(cmbTheme.SelectedItem.ToString(), out var theme))
            {
                var style = chkFilled.Checked ? ThemeStyle.Filled : ThemeStyle.Outline;
                try
                {
                    ThemeManager.ApplyTheme(process, theme, style);
                    Debug.Log($"Applied theme {theme} ({style}) to new process {process.ProcessId}");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, $"Failed to apply theme to process {process.ProcessId}");
                }
            }
        }

        private void UpdateShortcutFlags()
        {
            // Start with Command Palette and LineSelection always enabled? Wait, no, LineSelection is optional
            // From original: CommandPalette and LineExtend always? But now LineSelection is toggleable
            // Original had LineExtend always, but now it's toggleable, so remove always
            currentShortcutFlags = EventHookInstaller.ShortcutType.CommandPalette;

            // Add Open shortcut if checkbox is checked
            if (chkOverrideOpen.Checked)
            {
                currentShortcutFlags |= EventHookInstaller.ShortcutType.Open;
            }

            // Add Search shortcut if checkbox is checked
            if (chkOverrideFindReplace.Checked)
            {
                currentShortcutFlags |= EventHookInstaller.ShortcutType.Search;
            }

            // Add Line Selection shortcut if checkbox is checked
            if (chkLineSelectionFix.Checked)
            {
                currentShortcutFlags |= EventHookInstaller.ShortcutType.LineSelection;
            }

            // Notify all processes of the change
            NotifyMainWindowShortcutsChange(currentShortcutFlags);
        }

        private void InitializeShortcutFlags()
        {
            // Start with Command Palette only, since LineSelection is now toggleable
            currentShortcutFlags = EventHookInstaller.ShortcutType.CommandPalette;

            // Add Open shortcut if checkbox is checked
            if (chkOverrideOpen.Checked)
            {
                currentShortcutFlags |= EventHookInstaller.ShortcutType.Open;
            }

            // Add Search shortcut if checkbox is checked
            if (chkOverrideFindReplace.Checked)
            {
                currentShortcutFlags |= EventHookInstaller.ShortcutType.Search;
            }

            // Add Line Selection if checked
            if (chkLineSelectionFix.Checked)
            {
                currentShortcutFlags |= EventHookInstaller.ShortcutType.LineSelection;
            }

            // Don't notify processes during initialization - they will be notified when they connect
        }

        private GeneralSettingsData GetGeneralSettingsObject()
        {
            return new GeneralSettingsData
            {
                CodeFolding = chkCodeFolding.Checked,
                InitCollapsed = chkInitCollapsed.Checked,
                OnlyPPC = chkOnlyPPC.Checked,
                BetterSQL = chkBetterSQL.Checked,
                AutoDark = chkAutoDark.Checked,
                AutoPair = chkAutoPairing.Checked,
                PromptForDB = chkPromptForDB.Checked,
                LintReportPath = lintReportPath,
                TNS_ADMIN = TNS_ADMIN,
                CheckEventMapping = chkEventMapping.Checked,
                CheckEventMapXrefs = chkEventMapXrefs.Checked,
                ShowClassPath = optClassPath.Checked,
                ShowClassText = optClassText.Checked,
                RememberFolds = chkRememberFolds.Checked,
                OverrideFindReplace = chkOverrideFindReplace.Checked,
                OverrideOpen = chkOverrideOpen.Checked,
                AutoCenterDialogs = chkAutoCenterDialogs.Checked,
                MultiSelection = chkMultiSelection.Checked,
                LineSelectionFix = chkLineSelectionFix.Checked,
                Theme = cmbTheme.SelectedItem?.ToString() ?? Theme.Default.ToString(),
                ThemeFilled = chkFilled.Checked,
                ShowParamNames = chkInlineParameterHints.Checked,
                MiniMapOpen = chkDocMinimap.Checked,
                UseEnhancedEditor = chkUseEnhancedEditor.Checked
            };
        }

        private void SaveSettings()
        {
            if (isLoadingSettings) return; // Prevent saving during initial load
            if (settingsService == null) return;

            // 1. Gather and save General Settings to memory
            var generalSettingsToSave = GetGeneralSettingsObject();

            foreach (var app in AppDesignerProcesses.Values)
            {
                app.Settings = generalSettingsToSave;
            }

            settingsService.SaveGeneralSettings(generalSettingsToSave);

            // 2. Save Linter, Styler, Tooltip, and Language Extension states to memory
            if (linterManager != null) settingsService.SaveLinterStates(linterManager.LinterRules);
            if (stylerManager != null) settingsService.SaveStylerStates(stylerManager.StylerRules);
            settingsService.SaveTooltipStates(tooltipProviders);
            if (languageExtensionManager != null)
            {
                settingsService.SaveLanguageExtensionStates(languageExtensionManager.Extensions);
                settingsService.SaveLanguageExtensionConfigs(languageExtensionManager.Extensions);
            }

            // 3. Persist ALL changes to disk
            settingsService.SaveChanges();
            Debug.Log("All settings saved and persisted immediately.");

            // 4. Notify all hooked editors of auto-pairing setting changes
            NotifyAutoPairingChange(generalSettingsToSave.AutoPair);

            // 5. Notify all hooked editors of multi-selection setting changes
            NotifyMultiSelectionChange(generalSettingsToSave.MultiSelection);
        }

        // Method to notify all hooked editors of auto-pairing setting changes
        private void NotifyAutoPairingChange(bool enabled)
        {
            foreach (var appDesigner in AppDesignerProcesses.Values)
            {
                bool result = EventHookInstaller.SetAutoPairing(appDesigner.MainWindowHandle, enabled);
                Debug.Log($"Set auto-pairing ({enabled}) for process {appDesigner.ProcessId}: {result}");
            }

        }

        // Method to notify all hooked editors of multi-selection setting changes
        private void NotifyMultiSelectionChange(bool enabled)
        {
            foreach (var appDesigner in AppDesignerProcesses.Values)
            {
                foreach (var editor in appDesigner.Editors.Values)
                {
                    ScintillaManager.ToggleMultiSelection(editor, enabled);
                    Debug.Log($"Set multi-selection ({enabled}) for editor in process {appDesigner.ProcessId}");
                }
            }
        }

        // Method to notify all hooked editors of main window shortcuts setting changes
        private void NotifyMainWindowShortcutsChange(EventHookInstaller.ShortcutType shortcuts)
        {
            foreach (var appDesigner in AppDesignerProcesses.Values)
            {
                bool result = appDesigner.UpdateShortcuts(shortcuts);
                Debug.Log($"Sent main window shortcuts update ({shortcuts}) to thread {appDesigner.MainThreadId}: {result}");
            }
        }

        // Renamed from keyboard hook handlers to simple action methods
        private void collapseLevelHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.SetCurrentLineFoldStatus(activeEditor, true);
            UpdateSavedFoldsForEditor(activeEditor);
        }

        private void expandLevelHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.SetCurrentLineFoldStatus(activeEditor, false);
            UpdateSavedFoldsForEditor(activeEditor);
        }

        private void collapseAllHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.CollapseTopLevel(activeEditor);
            UpdateSavedFoldsForEditor(activeEditor);
        }

        private void expandAllHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.ExpandTopLevel(activeEditor);
            UpdateSavedFoldsForEditor(activeEditor);
        }

        private void lintCodeHandler()
        {
            if (activeEditor == null) return;
            linterManager?.ProcessLintersForActiveEditor(activeEditor, activeEditor.DataManager);
        }

        private void showBetterFindHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.ShowBetterFindDialog(activeEditor);
        }

        private void showBetterFindReplaceHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.ShowBetterFindDialog(activeEditor, enableReplaceMode: true);
        }

        internal void showStackTraceNavigatorHandler()
        {
            if (activeAppDesigner == null) return;

            try
            {
                // Check if dialog already exists and is visible
                if (stackTraceNavigatorDialog != null && !stackTraceNavigatorDialog.IsDisposed && stackTraceNavigatorDialog.Visible)
                {
                    // Bring existing dialog to front
                    stackTraceNavigatorDialog.BringToFront();
                    stackTraceNavigatorDialog.Activate();
                    return;
                }

                // Create new dialog
                var mainHandle = activeAppDesigner?.MainWindowHandle ?? IntPtr.Zero;
                stackTraceNavigatorDialog = new StackTraceNavigatorDialog(activeAppDesigner, mainHandle);

                // Handle dialog closed event to clean up reference
                stackTraceNavigatorDialog.FormClosed += (s, e) =>
                {
                    stackTraceNavigatorDialog = null;
                };

                // Show dialog
                stackTraceNavigatorDialog.Show();
            }
            catch (Exception ex)
            {
                Debug.Log($"Error showing Stack Trace Navigator dialog: {ex.Message}");
            }
        }

        private void findNextHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.FindNext(activeEditor);
        }

        private void findPreviousHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.FindPrevious(activeEditor);
        }

        private void goToDefinitionHandler()
        {
            GoToDefinitionCommand();
        }

        private void navigateBackwardHandler()
        {
            NavigateBackwardCommand();
        }

        private void navigateForwardHandler()
        {
            NavigateForwardCommand();
        }

        private void placeBookmarkHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.PlaceBookmark(activeEditor);
        }

        private void goToPreviousBookmarkHandler()
        {
            if (activeEditor == null) return;
            ScintillaManager.GoToPreviousBookmark(activeEditor);
        }

        // Parameterless overload for shortcut service
        private void ShowCommandPalette()
        {
            ShowCommandPalette(null, null);
        }

        // This is called by the Command Palette
        private void ShowCommandPalette(object? sender, KeyPressedEventArgs? e) // Keep original args for now
        {
            if (activeAppDesigner == null) return;
            var mainHandle = activeAppDesigner.MainWindowHandle;
            var handleWrapper = new WindowWrapper(mainHandle);
            // Create the command palette dialog
            var palette = new CommandPaletteDialog(AvailableCommands, mainHandle);

            // Show the dialog
            DialogResult result = palette.ShowDialog(handleWrapper);

            // If a command was selected, execute it directly
            if (result == DialogResult.OK)
            {
                Action? selectedAction = palette.GetSelectedAction();
                if (selectedAction != null)
                {
                    try
                    {
                        selectedAction.Invoke();
                    }
                    catch (Exception ex)
                    {
                        // Handle any exceptions during command execution using AppRefiner pattern
                        Task.Delay(100).ContinueWith(_ =>
                        {
                            var handleWrapper = new WindowWrapper(mainHandle);
                            new MessageBoxDialog($"Error executing command: {ex.Message}", "Command Error", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                        });
                    }
                }
            }
        }

        private void HandleParamNamesToggle(bool enabled)
        {
            Debug.Log($"HandleParamNamesToggle called with enabled={enabled}");
            var paramStyler = stylerManager.StylerRules.Where(r => r is FunctionParameterNames).First();
                
            paramStyler.Active = enabled;
            if (enabled)
            {
                stylerManager.ProcessStylersForEditor(activeEditor);
            } else
            {
                int SCI_INLAYHINTCLEARALL = 2904;
                activeEditor.SendMessage(SCI_INLAYHINTCLEARALL, 0, 0);
            }
            activeEditor.ParameterNamesEnabled = enabled;
        }

        private void expandAllHandler(object? sender, KeyPressedEventArgs e)
        {
            if (activeEditor == null) return;
            ScintillaManager.ExpandTopLevel(activeEditor);
        }

        private void collapseAllHandler(object? sender, KeyPressedEventArgs e)
        {
            if (activeEditor == null) return;
            ScintillaManager.CollapseTopLevel(activeEditor);
        }

        private void expandLevelHandler(object? sender, KeyPressedEventArgs e)
        {
            if (activeEditor == null) return;
            ScintillaManager.SetCurrentLineFoldStatus(activeEditor, false);
        }

        private void collapseLevelHandler(object? sender, KeyPressedEventArgs e)
        {
            if (activeEditor == null) return;
            ScintillaManager.SetCurrentLineFoldStatus(activeEditor, true);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Clean up all hooks to ensure they're properly removed
            AppRefiner.Events.EventHookInstaller.CleanupAllHooks();

            // Clean up the WinEvent hook if active
            if (winEventService != null)
            {
                winEventService.Dispose();
                winEventService = null;
            }

            // Clean up the ApplicationKeyboardService if active
            if (applicationKeyboardService != null)
            {
                applicationKeyboardService.Dispose();
                applicationKeyboardService = null;
            }

            // Dispose the savepoint debounce timer if it exists
            savepointDebounceTimer?.Dispose();
            savepointDebounceTimer = null;

            // Dispose the focus debounce timer if it exists
            focusDebounceTimer?.Dispose();
            focusDebounceTimer = null;

            // Clean up all validation retry timers
            lock (validationRetryLock)
            {
                foreach (var timer in validationRetryTimers.Values)
                {
                    timer.Dispose();
                }
                validationRetryTimers.Clear();
                validationRetryAttempts.Clear();
                validationRetryHandles.Clear();
            }

            // Dispose the Stack Trace Navigator dialog if it exists
            if (stackTraceNavigatorDialog != null && !stackTraceNavigatorDialog.IsDisposed)
            {
                stackTraceNavigatorDialog.Close();
                stackTraceNavigatorDialog.Dispose();
                stackTraceNavigatorDialog = null;
            }

            // Clear the styler processing time dictionary
            lastStylerProcessingTime.Clear();

            SaveSettings();
        }

        /// <summary>
        /// Set the directory where linting reports will be saved
        /// </summary>
        private void SetLintReportDirectory()
        {
            // Create folder browser dialog
            FolderBrowserDialog folderDialog = new()
            {
                Description = "Select directory for linting reports",
                UseDescriptionForTitle = true,
                SelectedPath = lintReportPath ?? string.Empty
            };

            // Show dialog and update path if OK
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                lintReportPath = folderDialog.SelectedPath;
                SaveSettings(); // Save all settings

                MessageBox.Show($"Lint reports will be saved to: {lintReportPath}",
                    "Lint Report Directory Updated",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void InitTooltipOptions()
        {
            // Get all tooltip providers from the TooltipManager
            tooltipProviders = TooltipManager.Providers.ToList();

            // Update the DataGridView with the tooltip providers
            foreach (var provider in tooltipProviders)
            {
                int rowIndex = dataGridViewTooltips.Rows.Add(provider.Active, provider.Description);
                dataGridViewTooltips.Rows[rowIndex].Tag = provider;
            }
        }

        private void dataGridViewTooltips_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            dataGridViewTooltips.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dataGridViewTooltips_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            if (e.ColumnIndex != 0)
            {
                return;
            }
            if (dataGridViewTooltips.Rows[e.RowIndex].Tag == null)
            {
                return;
            }
            if (dataGridViewTooltips.Rows[e.RowIndex].Tag is BaseTooltipProvider provider)
            {
                provider.Active = (bool)dataGridViewTooltips.Rows[e.RowIndex].Cells[0].Value;
                SaveSettings(); // Call the consolidated SaveSettings method
            }
        }

        private void EnableUIActions()
        {
            this.Invoke(() =>
            {
                btnClearLint.Enabled = true;
                btnApplyTemplate.Text = "Apply Template";

                btnConnectDB.Text = activeEditor?.DataManager == null ? "Connect DB..." : "Disconnect DB";

            });

        }

        private void dataGridView3_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            stylerManager?.HandleStylerGridCellContentClick(sender, e);
        }

        private void dataGridView3_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            stylerManager?.HandleStylerGridCellValueChanged(sender, e);
            SaveSettings(); // Call the consolidated SaveSettings method
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            linterManager?.HandleLinterGridCellValueChanged(sender, e);
            SaveSettings(); // Call the consolidated SaveSettings method
        }

        private void gridRefactors_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            refactorManager?.HandleRefactorGridCellContentClick(sender, e);
        }

        private void gridExtensions_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            languageExtensionManager?.HandleExtensionGridCellContentClick(sender, e);
        }

        private void gridExtensions_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            languageExtensionManager?.HandleExtensionGridCellValueChanged(sender, e);
            SaveSettings(); // Call the consolidated SaveSettings method
        }

        private void btnClearLint_Click(object sender, EventArgs e)
        {
            linterManager?.ClearLintResults(activeEditor);
        }


        private void btnConnectDB_Click(object sender, EventArgs e)
        {
            if (activeAppDesigner == null || activeEditor == null) return;
            if (activeAppDesigner.DataManager != null)
            {
                activeAppDesigner.DataManager.Disconnect();
                foreach (var editor in activeAppDesigner.Editors.Values)
                {
                    editor.DataManager = null;
                }


                btnConnectDB.Text = "Connect DB...";
                return;
            }

            var mainHandle = activeAppDesigner.MainWindowHandle;
            var handleWrapper = new WindowWrapper(mainHandle);
            DBConnectDialog dialog = new(mainHandle, activeAppDesigner.DBName);

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                IDataManager? manager = dialog.DataManager;
                if (manager != null)
                {
                    activeAppDesigner.DataManager = manager;
                    foreach (var editor in activeAppDesigner.Editors.Values)
                    {
                        editor.DataManager = manager;
                    }
                    activeEditor.DataManager = manager;
                    btnConnectDB.Text = "Disconnect DB";

                    // Force refresh all editors to allow DB-dependent stylers to run
                    RefreshAllEditorsAfterDatabaseConnection();
                }
            }
        }

        private void CmbTemplates_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbTemplates.SelectedItem is Template selectedTemplate)
            {
                templateManager.ActiveTemplate = selectedTemplate;
                GenerateTemplateUI(); // Call helper to generate UI
            }
            else
            {
                templateManager.ActiveTemplate = null;
                pnlTemplateParams.Controls.Clear(); // Clear panel if no template selected
                currentTemplateInputControls.Clear();
            }
        }

        /// <summary>
        /// Generates the UI controls for the currently active template's parameters.
        /// </summary>
        private void GenerateTemplateUI()
        {
            pnlTemplateParams.Controls.Clear();
            currentTemplateInputControls.Clear(); // Clear previous controls

            if (templateManager.ActiveTemplate == null) return;

            var definitions = templateManager.GetParameterDefinitionsForActiveTemplate();

            if (definitions == null || definitions.Count == 0)
            {
                return;
            }

            const int labelWidth = 150;
            const int controlWidth = 200;
            const int verticalSpacing = 30;
            const int horizontalPadding = 10;
            int currentY = 10;

            foreach (var definition in definitions)
            {
                // Create label for parameter
                Label label = new()
                {
                    Text = definition.Label + ":",
                    Location = new Point(horizontalPadding, currentY + 3),
                    Size = new Size(labelWidth, 20),
                    AutoSize = false,
                    Visible = definition.IsVisible,
                    Tag = definition.Id // Store input ID in Tag for easy reference
                };
                pnlTemplateParams.Controls.Add(label);

                // Create input control based on parameter type
                Control inputControl;
                switch (definition.Type.ToLower())
                {
                    case "boolean":
                        var chkBox = new CheckBox
                        {
                            Checked = definition.CurrentValue.Equals("true", StringComparison.OrdinalIgnoreCase),
                            Location = new Point(labelWidth + (horizontalPadding * 2), currentY),
                            Size = new Size(controlWidth, 23),
                            Text = "", // No text needed since we have the label
                            Visible = definition.IsVisible,
                            Tag = definition.Id // Store input ID in Tag
                        };
                        // Add event handler to update manager and regenerate UI
                        chkBox.CheckedChanged += TemplateControl_ValueChanged;
                        inputControl = chkBox;
                        break;

                    default: // Default to TextBox for string, number, etc.
                        var txtBox = new TextBox
                        {
                            Text = definition.CurrentValue,
                            Location = new Point(labelWidth + (horizontalPadding * 2), currentY),
                            Size = new Size(controlWidth, 23),
                            Visible = definition.IsVisible,
                            Tag = definition.Id // Store input ID in Tag
                        };
                        // Add event handler to update manager and regenerate UI
                        txtBox.TextChanged += TemplateControl_ValueChanged;
                        inputControl = txtBox;
                        break;
                }

                // Add tooltip if description is available
                if (!string.IsNullOrEmpty(definition.Description))
                {
                    ToolTip tooltip = new();
                    tooltip.SetToolTip(inputControl, definition.Description);
                    tooltip.SetToolTip(label, definition.Description);
                }

                pnlTemplateParams.Controls.Add(inputControl);
                currentTemplateInputControls[definition.Id] = inputControl; // Store reference

                if (definition.IsVisible)
                {
                    currentY += verticalSpacing;
                }
            }

            // Reflow controls after initial generation
            ReflowTemplateUI();
        }

        /// <summary>
        /// Event handler for when a template parameter control's value changes.
        /// Updates the TemplateManager and potentially regenerates the UI.
        /// </summary>
        private void TemplateControl_ValueChanged(object? sender, EventArgs e)
        {
            if (sender is Control control && control.Tag is string inputId)
            {
                string newValue = "";
                if (control is CheckBox chk)
                {
                    newValue = chk.Checked ? "true" : "false";
                }
                else if (control is TextBox txt)
                {
                    newValue = txt.Text;
                }
                // Add other control types if needed

                templateManager.UpdateParameterValue(inputId, newValue);

                // Regenerate UI to handle potential changes in display conditions
                GenerateTemplateUI();
            }
        }

        /// <summary>
        /// Reflows the template parameter controls in the panel to remove gaps from hidden controls.
        /// </summary>
        private void ReflowTemplateUI()
        {
            const int verticalSpacing = 30;
            int currentY = 10;

            // Order controls based on their original definition order if possible,
            // otherwise iterate through the panel's controls.
            // Assuming controls were added in order: Label then Input for each parameter.
            var orderedIds = templateManager.GetParameterDefinitionsForActiveTemplate()?.Select(d => d.Id).ToList() ?? new List<string>();

            foreach (string id in orderedIds)
            {
                // Find the Label and Control pair for this ID
                var label = pnlTemplateParams.Controls.OfType<Label>().FirstOrDefault(lbl => lbl.Tag as string == id);
                var control = currentTemplateInputControls.TryGetValue(id, out var ctrl) ? ctrl : null;

                if (label != null && control != null)
                {
                    if (label.Visible) // Assume if label is visible, control should be too
                    {
                        label.Location = new Point(label.Location.X, currentY + 3);
                        control.Location = new Point(control.Location.X, currentY);
                        currentY += verticalSpacing;
                    }
                }
            }
        }


        private void btnApplyTemplate_Click(object? sender, EventArgs e)
        {
            if (activeEditor == null)
            {
                MessageBox.Show("No active editor to apply template to.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (templateManager.ActiveTemplate == null)
            {
                MessageBox.Show("No template selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validate inputs using the manager
            if (!templateManager.ValidateInputs())
            {
                MessageBox.Show("Please fill in all required fields.", "Required Fields Missing",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Check for replacement warning only if it's not insert mode
            if (!templateManager.ActiveTemplate.IsInsertMode && !string.IsNullOrWhiteSpace(ScintillaManager.GetScintillaText(activeEditor)))
            {
                var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                var handleWrapper = new WindowWrapper(mainHandle);
                using var confirmDialog = new TemplateConfirmationDialog(
                    "Applying this template will replace all content in the current editor. Do you want to continue?",
                    mainHandle);

                if (confirmDialog.ShowDialog(handleWrapper) != DialogResult.Yes)
                {
                    return; // User cancelled replacement
                }
            }

            // Apply the template using the manager
            templateManager.ApplyActiveTemplateToEditor(activeEditor);
        }

        internal void ShowDeclareFunctionDialog()
        {
            if (activeAppDesigner?.DataManager == null)
            {
                // Show message that database connection is required
                Task.Delay(100).ContinueWith(_ =>
                {
                    var mainHandle = activeAppDesigner?.MainWindowHandle ?? IntPtr.Zero;
                    if (mainHandle != IntPtr.Zero)
                    {
                        var handleWrapper = new WindowWrapper(mainHandle);
                        new MessageBoxDialog("Declare function requires a database connection. Please connect to database first.",
                            "Database Required", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                    }
                });
                return;
            }

            if (functionCacheManager == null)
            {
                // Show message that database connection is required
                Task.Delay(100).ContinueWith(_ =>
                {
                    var mainHandle = activeAppDesigner?.MainWindowHandle ?? IntPtr.Zero;
                    if (mainHandle != IntPtr.Zero)
                    {
                        var handleWrapper = new WindowWrapper(mainHandle);
                        new MessageBoxDialog("There was an issue initializing the FunctionCacheManager. ",
                            "Function Cache Manager Failure", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                    }
                });
                return;
            }

            var dataManager = activeAppDesigner.DataManager;
            var dialog = new DeclareFunctionDialog(functionCacheManager, activeAppDesigner,
                activeAppDesigner?.MainWindowHandle ?? IntPtr.Zero);

            try
            {
                var mainHandle = activeAppDesigner?.MainWindowHandle ?? IntPtr.Zero;
                var handleWrapper = new WindowWrapper(mainHandle);
                var result = dialog.ShowDialog(handleWrapper);
                if (result == DialogResult.OK)
                {
                    var selectedFunction = dialog.SelectedFunction;
                    if (selectedFunction != null && activeAppDesigner != null)
                    {
                        var refactorClass = new DeclareFunction(activeEditor, selectedFunction);
                        refactorManager.ExecuteRefactor(refactorClass, activeEditor, false);
                    }

                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error showing Declare Function dialog: {ex.Message}");
            }
            finally
            {
                dialog?.Dispose();
            }
        }

        internal void ShowSmartOpenDialog()
        {
            if (activeAppDesigner?.DataManager == null)
            {
                // Show message that database connection is required
                Task.Delay(100).ContinueWith(_ =>
                {
                    var mainHandle = activeAppDesigner?.MainWindowHandle ?? IntPtr.Zero;
                    if (mainHandle != IntPtr.Zero)
                    {
                        var handleWrapper = new WindowWrapper(mainHandle);
                        new MessageBoxDialog("Smart Open requires a database connection. Please connect to database first.",
                            "Database Required", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                    }
                });
                return;
            }

            var dataManager = activeAppDesigner.DataManager;
            var dialog = new SmartOpenDialog(
                (options) => dataManager.GetOpenTargets(options),
                activeAppDesigner?.MainWindowHandle ?? IntPtr.Zero,
                BypassSmartOpen);

            try
            {
                var mainHandle = activeAppDesigner?.MainWindowHandle ?? IntPtr.Zero;
                var handleWrapper = new WindowWrapper(mainHandle);
                var result = dialog.ShowDialog(handleWrapper);
                if (result == DialogResult.OK)
                {
                    var selectedTarget = dialog.GetSelectedTarget();
                    if (selectedTarget != null && activeAppDesigner != null)
                    {
                        // Build the open target string based on the target type and object data
                        string openTargetString = BuildOpenTargetString(selectedTarget);
                        activeAppDesigner.SetOpenTarget(openTargetString);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error showing Smart Open dialog: {ex.Message}");
            }
            finally
            {
                dialog?.Dispose();
            }
        }

        private void BypassSmartOpen()
        {
            if (activeAppDesigner == null)
            {
                Debug.Log("BypassSmartOpen: No active AppDesigner process");
                return;
            }

            var originalShortcuts = currentShortcutFlags;
            try
            {
                Debug.Log("BypassSmartOpen: Starting bypass sequence");

                // Step 1: Temporarily disable SHORTCUT_OPEN for all AppDesigner processes
                var bypassShortcuts = currentShortcutFlags & ~EventHookInstaller.ShortcutType.Open;

                Debug.Log($"BypassSmartOpen: Temporarily disabling SHORTCUT_OPEN ({originalShortcuts} -> {bypassShortcuts})");
                NotifyMainWindowShortcutsChange(bypassShortcuts);
                // Step 2: Send Ctrl+O to the active App Designer main window
                var mainWindowHandle = activeAppDesigner.MainWindowHandle;
                Thread.Sleep(100);
                WinApi.SetForegroundWindow(mainWindowHandle);
                if (mainWindowHandle != IntPtr.Zero)
                {
                    Debug.Log($"BypassSmartOpen: Sending Ctrl+O to App Designer window {mainWindowHandle:X}");

                    // Send the key combination using keybd_event for global effect
                    keybd_event(VK_CONTROL, 0, 0, 0); // Ctrl down
                    keybd_event(VK_O, 0, 0, 0);       // O down  
                    keybd_event(VK_O, 0, 2, 0);       // O up (KEYEVENTF_KEYUP = 2)
                    keybd_event(VK_CONTROL, 0, 2, 0); // Ctrl up
                }
                else
                {
                    Debug.Log("BypassSmartOpen: Main window handle is null");
                }

                // Step 3: Re-enable SHORTCUT_OPEN after a short delay
                Task.Delay(100).ContinueWith(_ =>
                {
                    Debug.Log($"BypassSmartOpen: Re-enabling SHORTCUT_OPEN ({bypassShortcuts} -> {originalShortcuts})");
                    NotifyMainWindowShortcutsChange(originalShortcuts);
                });
            }
            catch (Exception ex)
            {
                Debug.Log($"BypassSmartOpen: Error during bypass: {ex.Message}");

                // Ensure shortcuts are restored even if there's an error
                NotifyMainWindowShortcutsChange(originalShortcuts);
            }
        }


        private string BuildOpenTargetString(OpenTarget target, SourceSpan? span = null)
        {
            // Build the target string based on the type
            StringBuilder sb = new();
            for (var x = 0; x < target.ObjectIDs.Length; x++)
            {
                if (target.ObjectIDs[x] == PSCLASSID.NONE) break;
                if (x > 0)
                {
                    sb.Append('.');
                }
                sb.Append(Enum.GetName(typeof(PSCLASSID), target.ObjectIDs[x]));
                sb.Append('.');
                sb.Append(target.ObjectValues[x]);
            }

            if (span.HasValue)
            {
                sb.Append($".SOURCETOKEN.{span.Value.Start.ByteIndex}");
            }

            return sb.ToString();
        }

        private void RegisterCommands(CommandManager? commandManager)
        {
            // Clear any existing commands
            AvailableCommands.Clear();

            // Add built-in commands from CommandManager (includes both built-in and plugin commands)
            if (commandManager != null)
            {
                foreach (var (commandId, commandInstance) in commandManager.GetCommands())
                {
                    // Capture the instance for the lambda
                    var currentCommand = commandInstance;

                    AvailableCommands.Add(new Command(
                        currentCommand.GetDisplayName(),
                        currentCommand.CommandDescription,
                        () =>
                        {
                            var context = CreateCommandContext();
                            commandManager.ExecuteCommand(currentCommand, context);
                        },
                        currentCommand.DynamicEnabledCheck
                    )
                    {
                        RequiresActiveEditor = currentCommand.RequiresActiveEditor
                    });
                }
            }

            // Add dynamic refactoring commands using RefactorManager
            if (refactorManager != null)
            {
                foreach (var refactorInfo in refactorManager.AvailableRefactors)
                {
                    // Capture the info for the lambda
                    RefactorInfo currentRefactorInfo = refactorInfo;
                    AvailableCommands.Add(new Command(
                            $"Refactor: {currentRefactorInfo.Name}{currentRefactorInfo.ShortcutText}",
                            currentRefactorInfo.Description,
                        () =>
                        {
                            if (activeEditor != null)
                            {
                                try
                                {
                                    // Create an instance of the refactor
                                    var refactor = (BaseRefactor?)Activator.CreateInstance(
                                            currentRefactorInfo.RefactorType,
                                            [activeEditor] // Assuming constructor takes ScintillaEditor
                                );

                                    if (refactor != null)
                                    {
                                        // Execute via the manager
                                        refactorManager.ExecuteRefactor(refactor, activeEditor);
                                    }
                                    else
                                    {
                                        Debug.LogError($"Failed to create instance of refactor: {currentRefactorInfo.RefactorType.FullName}");
                                        MessageBox.Show(this, "Error creating refactor instance.", "Refactor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogException(ex, $"Error instantiating or executing refactor: {currentRefactorInfo.RefactorType.FullName}");
                                    MessageBox.Show(this, $"Error running refactor: {ex.Message}", "Refactor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }
                    ));
                }
            }

            // Add dynamic linter commands with "Lint: " prefix
            // Need to get linters from the manager
            if (linterManager != null)
            {
                foreach (var linter in linterManager.LinterRules)
                {
                    // Capture the linter instance for the lambda
                    BaseLintRule currentLinter = linter;
                    AvailableCommands.Add(new Command(
                            $"Lint: {currentLinter.Description}",
                            $"Run {currentLinter.Description} linting rule",
                        () =>
                        {
                            if (activeEditor != null)
                            {
                                // Need to pass current DataManager
                                linterManager?.ProcessSingleLinter(currentLinter, activeEditor, activeEditor.AppDesignerProcess.DataManager);
                            }
                        },
                        () => activeEditor != null && activeEditor.DataManager == null
                        ));
                }
            }
        }

        /// <summary>
        /// Creates a CommandContext with current application state for command execution
        /// </summary>
        private CommandContext CreateCommandContext()
        {
            return new CommandContext
            {
                ActiveEditor = activeEditor,
                LinterManager = linterManager,
                StylerManager = stylerManager,
                AutoCompleteService = autoCompleteService,
                RefactorManager = refactorManager,
                SettingsService = settingsService,
                FunctionCacheManager = functionCacheManager,
                AutoSuggestSettings = autoSuggestSettings,
                MainForm = this,
                MainWindowHandle = activeAppDesigner?.MainWindowHandle ?? IntPtr.Zero,
                ActiveAppDesigner = activeAppDesigner,
                SnapshotManager = snapshotManager
            };
        }

        private void btnPlugins_Click(object sender, EventArgs e)
        {
            // Load plugins from the plugin directory
            string pluginDirectory = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty,
                Properties.Settings.Default.PluginDirectory);

            PluginManagerDialog dialog = new(pluginDirectory);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                // Update the PluginDirectory setting and save it
                Properties.Settings.Default.PluginDirectory = dialog.PluginDirectory;
                Properties.Settings.Default.Save();
            }
        }

        private void RegisterRefactorShortcuts()
        {
            // Use RefactorManager to get shortcut info
            if (refactorManager == null || applicationKeyboardService == null) return;

            foreach (var refactorInfo in refactorManager.AvailableRefactors)
            {
                // Check if this refactor wants a keyboard shortcut
                if (refactorInfo.RegisterShortcut && refactorInfo.Key != Keys.None)
                {
                    // Capture info for the lambda
                    RefactorInfo currentRefactorInfo = refactorInfo;

                    // Register the shortcut using the service
                    bool registered = applicationKeyboardService?.RegisterShortcut(
                        currentRefactorInfo.Name, // Use Name for unique ID
                        currentRefactorInfo.Modifiers,
                        currentRefactorInfo.Key,
                        () => // Action lambda
                        {
                            if (activeEditor == null) return;

                            try
                            {
                                var newRefactor = (BaseRefactor?)Activator.CreateInstance(
                                    currentRefactorInfo.RefactorType,
                                    [activeEditor] // Assuming constructor takes ScintillaEditor
                                );

                                if (newRefactor != null)
                                {
                                    // Execute via manager
                                    refactorManager.ExecuteRefactor(newRefactor, activeEditor);
                                }
                                else
                                {
                                    Debug.LogError($"Failed to create instance for shortcut: {currentRefactorInfo.RefactorType.FullName}");
                                    MessageBox.Show(this, "Error creating refactor instance for shortcut.", "Refactor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogException(ex, $"Error instantiating or executing refactor from shortcut: {currentRefactorInfo.RefactorType.FullName}");
                                MessageBox.Show(this, $"Error running refactor from shortcut: {ex.Message}", "Refactor Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    ) ?? false;

                    if (!registered)
                    {
                        Debug.LogWarning($"Failed to register shortcut for refactor: {currentRefactorInfo.Name}");
                    }
                }
            }
        }

        private void SetActiveEditor(IntPtr hwnd)
        {
            try
            {
                // Check if this is the same editor we already have
                if (activeEditor != null && activeEditor.hWnd == hwnd)
                {
                    // Same editor as before - just return it
                    return;
                }

                // This is a different editor or we didn't have one before
                try
                {
                    WinApi.GetWindowThreadProcessId(hwnd, out uint pid);

                    if (AppDesignerProcesses.TryGetValue(pid, out var process))
                    {
                        activeEditor = process.GetOrInitEditor(hwnd);
                        activeAppDesigner = activeEditor.AppDesignerProcess;
                        stylerManager.StylerRules.Where(s => s is FunctionParameterNames).First().Active = activeEditor.ParameterNamesEnabled;
                        return;
                    }
                    else
                    {
                        var newProcess = new AppDesignerProcess(pid, IntPtr.Zero, GetGeneralSettingsObject(), currentShortcutFlags);
                        string testPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!, "scintilla_mods");
                        if (Settings.Default.useEnhancedEditor)
                        {
                            newProcess.LoadScintillaDll(testPath);
                        }
                        AppDesignerProcesses.Add(pid, newProcess);
                        trackedProcessIds.Add(pid);
                        activeEditor = newProcess.GetOrInitEditor(hwnd);
                        activeEditor.AppDesignerProcess = newProcess;

                        // Apply current theme to newly created process
                        ApplyCurrentThemeToProcess(newProcess);
                        activeAppDesigner = activeEditor.AppDesignerProcess;
                        stylerManager.StylerRules.Where(s => s is FunctionParameterNames).First().Active = activeEditor.ParameterNamesEnabled;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log($"Error getting Scintilla editor: {ex.Message}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Exception in GetActiveEditor: {ex.Message}");
                return;
            }
        }



        // Check if content has changed and process if necessary
        private void CheckForContentChanges(ScintillaEditor editor)
        {
            if (editor == null) return;

            Debug.Log($"CheckForContentChanges: Called for editor {editor.RelativePath ?? "unknown"}");

            /* Update editor caption */
            var caption = WindowHelper.GetGrandparentWindowCaption(editor.hWnd);
            if (caption == "Suppress")
            {
                Thread.Sleep(1000);
                caption = WindowHelper.GetGrandparentWindowCaption(editor.hWnd);
            }

            // Capture previous caption to detect reuse of editor for different program
            var previousCaption = editor.Caption;
            editor.Caption = caption;

            // If caption changed, the CaptionChanged event handler will call CheckForContentChanges
            // recursively and do all the work. Return early to avoid duplicate processing.
            if (previousCaption != null && caption != null && !previousCaption.Equals(caption))
            {
                Debug.Log($"CheckForContentChanges: Caption changed from '{previousCaption}' to '{caption}' - " +
                         "returning early as CaptionChanged event will handle processing");
                return;
            }

            /* if (chkBetterSQL.Checked && editor.Type == EditorType.SQL)
            {
                ScintillaManager.ApplyBetterSQL(editor);
            } */

            /* Try to detect JSON if type is HTML */
            if (editor.Type == EditorType.HTML)
            {
                var contents = ScintillaManager.GetScintillaText(editor);
                if (contents != null)
                {
                    try
                    {
                        var parseResult = JsonDocument.Parse(contents);
                        ScintillaManager.SetJSONLexer(editor);
                    }
                    catch (Exception e)
                    {
                    }
                }
            }

            // Process stylers for PeopleCode with debouncing to prevent double execution
            // IMPORTANT: Clear/reset ONLY happens if we're going to re-process stylers
            // This prevents clearing indicators without re-adding them when debounced
            if (editor.Type == EditorType.PeopleCode)
            {
                var now = DateTime.UtcNow;
                if (!lastStylerProcessingTime.TryGetValue(editor, out var lastProcessed) ||
                    (now - lastProcessed).TotalMilliseconds > STYLER_PROCESSING_DEBOUNCE_MS)
                {
                    Debug.Log($"CheckForContentChanges: Processing stylers for {editor.RelativePath ?? "unknown"}");

                    // Clear annotations and reset styles BEFORE processing stylers
                    ScintillaManager.ClearAnnotations(editor);
                    ScintillaManager.ResetStyles(editor);

                    // Apply dark mode if enabled
                    if (chkAutoDark.Checked)
                    {
                        ScintillaManager.SetDarkMode(editor);
                    }

                    lastStylerProcessingTime[editor] = now;
                    stylerManager?.ProcessStylersForEditor(editor);
                }
                else
                {
                    var timeSinceLastProcess = (now - lastProcessed).TotalMilliseconds;
                    Debug.Log($"CheckForContentChanges: Skipping ALL processing (debounce) for {editor.RelativePath ?? "unknown"} - " +
                             $"only {timeSinceLastProcess}ms since last process (need {STYLER_PROCESSING_DEBOUNCE_MS}ms)");
                }
            }
            else
            {
                // For non-PeopleCode editors, always clear and reset (no stylers to worry about)
                ScintillaManager.ClearAnnotations(editor);
                ScintillaManager.ResetStyles(editor);

                if (chkAutoDark.Checked)
                {
                    ScintillaManager.SetDarkMode(editor);
                }
            }

            FoldingManager.ProcessFolding(editor);

        }

        public void ProcessMessage(Message m)
        {
            WndProc(ref m);
        }

        /* TODO: override WndProc */
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Need activeEditor for most messages, but check null within specific cases
            // if (activeEditor == null) return; 
            // Removed early return, check activeEditor inside cases

            /* if message is a WM_SCN_EVENT (check the mask) */
            if ((m.Msg & WM_SCN_EVENT_MASK) == WM_SCN_EVENT_MASK)
            {
                // Only process if we have an active editor
                if (activeEditor == null || !activeEditor.IsValid()) return;

                /* remove mask */
                var eventCode = m.Msg & ~WM_SCN_EVENT_MASK;

                switch (eventCode)
                {
                    case SCN_DWELLSTART:
                        Debug.Log($"SCN_DWELLSTART: {m.WParam} -- {m.LParam}");

                        /* In case a tooltip got stuck? */
                        TooltipProviders.TooltipManager.HideTooltip(activeEditor);

                        TooltipProviders.TooltipManager.ShowTooltip(activeEditor, m.WParam.ToInt32(), m.LParam.ToInt32());
                        break;
                    case SCN_DWELLEND:
                        TooltipProviders.TooltipManager.HideTooltip(activeEditor);
                        break;
                    case SCN_SAVEPOINTREACHED:
                        Debug.Log("SAVEPOINTREACHED...");
                        // Active editor check already done above
                        lock (savepointLock)
                        {
                            // Cancel any pending savepoint timer
                            savepointDebounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);

                            // Store the editor for later processing
                            pendingSaveEditor = activeEditor;

                            // Record the time of this savepoint
                            lastSavepointTime = DateTime.Now;

                            // Start a new timer to process this savepoint after the debounce period
                            savepointDebounceTimer = new System.Threading.Timer(
                                ProcessSavepoint, null, SAVEPOINT_DEBOUNCE_MS, Timeout.Infinite);
                        }
                        break;
                    case SCN_USERLISTSELECTION:
                        Debug.Log("User list selection received");
                        // Active editor check already done above
                        // wParam is the list type
                        UserListType listType = (UserListType)m.WParam.ToInt32();
                        // lParam is a pointer to a UTF8 string in the editor's process memory
                        if (m.LParam != IntPtr.Zero)
                        {
                            // Read the UTF8 string from the editor's process memory
                            string? selectedText = ScintillaManager.ReadUtf8FromMemory(activeEditor, m.LParam, 256);

                            if (!string.IsNullOrEmpty(selectedText))
                            {
                                Debug.Log($"User selected: {selectedText} (list type: {listType})");
                                // Call the AutoCompleteService
                                var refactor = autoCompleteService?.HandleUserListSelection(activeEditor, selectedText, listType);
                                if (refactor != null)
                                {
                                    Task.Delay(100).ContinueWith(_ =>
                                    {
                                        // Execute via RefactorManager
                                        refactorManager?.ExecuteRefactor(refactor, activeEditor);
                                    }, TaskScheduler.Default); // Use default scheduler
                                }
                            }
                        }

                        if (activeEditor.FunctionCallTipActive && activeEditor.FunctionCallNode != null && activeEditor.FunctionCallTipProgram != null)
                        {
                            var currentCursorLine = ScintillaManager.GetLineFromPosition(activeEditor, ScintillaManager.GetCursorPosition(activeEditor));
                            if (currentCursorLine != activeEditor.FunctionCallNode.SourceSpan.Start.Line)
                            {
                                activeEditor.FunctionCallTipActive = false;
                                activeEditor.FunctionCallNode = null;
                                activeEditor.FunctionCallStartPosition = 0;
                                activeEditor.FunctionCallTipProgram = null;
                            }
                            else
                            {
                                TooltipManager.ShowFunctionCallTooltip(activeEditor, activeEditor.FunctionCallTipProgram, activeEditor.FunctionCallNode);
                            }
                        }
                        break;
                    case SCN_AUTOCSELECTION:
                        Debug.Log("Autocomplete selection received");
                        // Active editor check already done above

                        // Read the selected text from lParam (pointer to UTF8 string)
                        if (m.LParam != IntPtr.Zero)
                        {
                            string? selectedText = ScintillaManager.ReadUtf8FromMemory(activeEditor, m.LParam, 256);

                            if (!string.IsNullOrEmpty(selectedText))
                            {
                                // Cancel autocomplete to prevent Scintilla from auto-inserting the full display text
                                // We'll let our existing handlers insert only the correct portion
                                ScintillaManager.CancelUserList(activeEditor);

                                // Get context from editor
                                var context = activeEditor.ActiveAutoCompleteContext;

                                // Recalculate lengthEntered based on current cursor position
                                // This accounts for additional characters typed while autocomplete was open (for filtering)
                                int currentPos = ScintillaManager.GetCursorPosition(activeEditor);
                                int lengthEntered = ScintillaManager.CalculateLengthEntered(activeEditor, context, currentPos);
                                Debug.Log($"Autocomplete selected: {selectedText} (context: {context}, lengthEntered: {lengthEntered})");

                                // Delete the characters the user already typed (lengthEntered)
                                // This prevents duplication like %%filepath when user typed %file and selected %filepath
                                if (lengthEntered > 0)
                                {
                                    int deleteStart = currentPos - lengthEntered;
                                    if (deleteStart >= 0)
                                    {
                                        ScintillaManager.DeleteRange(activeEditor, deleteStart, lengthEntered);
                                        Debug.Log($"Deleted {lengthEntered} characters from position {deleteStart}");
                                    }
                                }

                                // Convert context to UserListType for routing to existing handlers
                                UserListType convertedListType = context switch
                                {
                                    AutoCompleteContext.AppPackage => UserListType.AppPackage,
                                    AutoCompleteContext.Variable => UserListType.Variable,
                                    AutoCompleteContext.ObjectMembers => UserListType.ObjectMembers,
                                    AutoCompleteContext.SystemVariables => UserListType.SystemVariables,
                                    _ => UserListType.QuickFix  // Fallback (should never happen)
                                };

                                // Route to existing handler (which will insert the correct text)
                                var refactor = autoCompleteService?.HandleUserListSelection(activeEditor, selectedText, convertedListType);
                                if (refactor != null)
                                {
                                    Task.Delay(100).ContinueWith(_ =>
                                    {
                                        // Execute via RefactorManager
                                        refactorManager?.ExecuteRefactor(refactor, activeEditor);
                                    }, TaskScheduler.Default);
                                }

                                // Handle function call tips (same as UserListSelection)
                                if (activeEditor.FunctionCallTipActive && activeEditor.FunctionCallNode != null)
                                {
                                    var currentCursorLine = ScintillaManager.GetLineFromPosition(activeEditor,
                                        ScintillaManager.GetCursorPosition(activeEditor));
                                    if (currentCursorLine != activeEditor.FunctionCallNode.SourceSpan.Start.Line)
                                    {
                                        activeEditor.FunctionCallTipActive = false;
                                        activeEditor.FunctionCallNode = null;
                                        activeEditor.FunctionCallStartPosition = 0;
                                        activeEditor.FunctionCallTipProgram = null;
                                    }
                                    else
                                    {
                                        TooltipManager.ShowFunctionCallTooltip(activeEditor,
                                            activeEditor.FunctionCallTipProgram, activeEditor.FunctionCallNode);
                                    }
                                }
                            }

                            // Clear context and lengthEntered after handling
                            activeEditor.ActiveAutoCompleteContext = AutoCompleteContext.None;
                            activeEditor.AutoCompleteLengthEntered = 0;
                        }
                        break;

                    case SCN_AUTOCCOMPLETED:
                        Debug.Log("Autocomplete completed");
                        // Clear context and lengthEntered when autocomplete finishes
                        if (activeEditor != null)
                        {
                            activeEditor.ActiveAutoCompleteContext = AutoCompleteContext.None;
                            activeEditor.AutoCompleteLengthEntered = 0;
                        }
                        break;
                }
            }
            else if (m.Msg == AR_APP_PACKAGE_SUGGEST)
            {
                // Only process if we have an active editor and service
                if (activeEditor == null || !activeEditor.IsValid() || autoCompleteService == null) return;

                /* Handle app package suggestion request */
                Debug.Log($"Received app package suggest message. WParam: {m.WParam}, LParam: {m.LParam}");

                // WParam contains the current cursor position
                int position = m.WParam.ToInt32();

                // Call the AutoCompleteService
                autoCompleteService.ShowAppPackageSuggestions(activeEditor, position);
            }
            else if (m.Msg == AR_VARIABLE_SUGGEST)
            {
                // Only process if we have an active editor and service
                if (activeEditor == null || !activeEditor.IsValid() || autoCompleteService == null) return;

                // Check if variable suggestions are enabled
                if (!autoSuggestSettings.VariableSuggestions) return;

                /* Handle variable suggestion request */
                Debug.Log($"Received variable suggest message. WParam: {m.WParam}, LParam: {m.LParam}");

                // WParam contains the current cursor position
                int position = m.WParam.ToInt32();

                // Call the AutoCompleteService
                autoCompleteService.ShowVariableSuggestions(activeEditor, position);
            }
            else if (m.Msg == AR_CREATE_SHORTHAND)
            {
                // Only process if we have an active editor and service
                if (activeEditor == null || !activeEditor.IsValid() || autoCompleteService == null) return;

                /* Handle create shorthand detection */
                Debug.Log($"Received create shorthand message. WParam: {m.WParam}, LParam: {m.LParam}");

                // WParam contains auto-pairing status (bool)
                bool autoPairingEnabled = m.WParam.ToInt32() != 0;

                // LParam contains the current cursor position
                int position = m.LParam.ToInt32();

                // Call the AutoCompleteService
                var refactor = autoCompleteService.PrepareCreateAutoCompleteRefactor(activeEditor, position, autoPairingEnabled);
                if (refactor != null)
                {
                    // Execute via RefactorManager
                    refactorManager?.ExecuteRefactor(refactor, activeEditor);

                    /* Move the cursor backwards 1 */
                    if (activeEditor.AppDesignerProcess.Settings.AutoPair)
                    {
                        ScintillaManager.SetCursorPosition(activeEditor, ScintillaManager.GetCursorPosition(activeEditor) - 1);
                    }
                }
            }
            else if (m.Msg == AR_MSGBOX_SHORTHAND)
            {
                // Only process if we have an active editor and service
                if (activeEditor == null || !activeEditor.IsValid() || autoCompleteService == null) return;

                /* Handle create shorthand detection */
                Debug.Log($"Received MsgBox shorthand message. WParam: {m.WParam}, LParam: {m.LParam}");

                // WParam contains auto-pairing status (bool)
                bool autoPairingEnabled = m.WParam.ToInt32() != 0;

                // LParam contains the current cursor position
                int position = m.LParam.ToInt32();

                // Call the AutoCompleteService
                var refactor = autoCompleteService.PrepareMsgBoxAutoCompleteRefactor(activeEditor, position, autoPairingEnabled);
                if (refactor != null)
                {
                    // Execute via RefactorManager
                    refactorManager?.ExecuteRefactor(refactor, activeEditor);
                    ScintillaManager.SetCursorPosition(activeEditor, ScintillaManager.GetCursorPosition(activeEditor) - 3);
                }
            }
            else if (m.Msg == AR_CONCAT_SHORTHAND)
            {
                // Only process if we have an active editor and service
                if (activeEditor == null || !activeEditor.IsValid() || autoCompleteService == null) return;

                /* Handle create shorthand detection */
                Debug.Log($"Received concat shorthand message. WParam: {m.WParam}, LParam: {m.LParam}");

                /* Concat expansion is powered by custom PeopleCode parser rules to detect the concat expression and its type (+=, -=, |=) */
                // Call the AutoCompleteService
                var refactor = autoCompleteService.PrepareConcatAutoCompleteRefactor(activeEditor);
                if (refactor != null)
                {
                    // Execute via RefactorManager
                    Task.Delay(250).ContinueWith(_ =>
                    {
                        // Execute via RefactorManager
                        refactorManager?.ExecuteRefactor(refactor, activeEditor);
                    }, TaskScheduler.Default); // Use default scheduler
                }
            }
            else if (m.Msg == AR_TYPING_PAUSE)
            {
                /* Handle typing pause detection */
                int position = m.WParam.ToInt32();
                int line = m.LParam.ToInt32();

                Debug.Log($"Received typing pause message. Position: {position}, Line: {line}");

                // Only process if we have an active editor
                if (activeEditor != null && activeEditor.IsValid())
                {
                    // Log the typing pause event
                    Debug.Log($"User stopped typing at position {position}, line {line}");

                    activeEditor.ContentString = ScintillaManager.GetScintillaText(activeEditor);

                    var lastKnownKey = $"{activeEditor.AppDesignerProcess.ProcessId}:{activeEditor.Caption}";

                    lastKnownPositions[lastKnownKey] = (ScintillaManager.GetFirstVisibleLine(activeEditor), ScintillaManager.GetCursorPosition(activeEditor));

                    // Process the editor content now that typing has paused
                    // This replaces the periodic scanning from the timer
                    CheckForContentChanges(activeEditor);
                }
            }
            else if (m.Msg == AR_BEFORE_DELETE_ALL)
            {
                Debug.Log("Received before delete all message");
                // Only process if we have an active editor
                if (activeEditor != null && activeEditor.IsValid())
                {
                    UpdateSavedFoldsForEditor(activeEditor);

                }
            }

            else if (m.Msg == AR_FOLD_MARGIN_CLICK)
            {
                UpdateSavedFoldsForEditor(activeEditor);
            }
            else if (m.Msg == AR_TEXT_PASTED)
            {
                // Only process if we have an active editor
                if (activeEditor == null || !activeEditor.IsValid()) return;

                Debug.Log($"Text pasted detected at position: {m.WParam}, length: {m.LParam}");

                // Trigger ResolveImports refactor automatically
                // Disabling this because it really messes up the "undo" where the user expects to just undo the paste
                // TriggerResolveImportsRefactor();
            }
            else if (m.Msg == AR_KEY_COMBINATION)
            {
                // Only process if we have an active editor
                if ((activeEditor == null || !activeEditor.IsValid()) && activeAppDesigner == null) return;

                // Simple throttling to prevent rapid duplicates
                var now = DateTime.UtcNow;
                if ((now - _lastShortcutTime).TotalMilliseconds < SHORTCUT_THROTTLE_MS)
                {
                    return; // Skip very rapid duplicates
                }
                _lastShortcutTime = now;

                Debug.Log($"Key combination detected: {m.WParam:X}, source: {m.LParam}");

                if (applicationKeyboardService != null)
                {
                    applicationKeyboardService.ProcessKeyMessage(m.WParam.ToInt32());
                }
            }
            else if (m.Msg == AR_CURSOR_POSITION_CHANGED)
            {
                // Only process if we have an active editor
                if (activeEditor == null || !activeEditor.IsValid()) return;

                // Extract first visible line from wParam, cursor position from lParam
                int firstVisibleLine = m.WParam.ToInt32();
                int cursorPosition = m.LParam.ToInt32();

                // Update last known positions
                var lastKnownKey = $"{activeEditor.AppDesignerProcess.ProcessId}:{activeEditor.Caption}";
                lastKnownPositions[lastKnownKey] = (firstVisibleLine, cursorPosition);

                Debug.Log($"Cursor position changed: first visible line {firstVisibleLine}, position {cursorPosition}");

                if (activeEditor.Type != EditorType.PeopleCode)
                {
                    return; // Code past here is only for PeopleCode editors
                }

                if (activeEditor.FunctionCallTipActive)
                {
                    var calltipLine = activeEditor.FunctionCallNode.SourceSpan.Start.Line;
                    var currentLine = ScintillaManager.GetCurrentLineNumber(activeEditor);

                    if (currentLine != calltipLine || cursorPosition < activeEditor.FunctionCallStartPosition)
                    {
                        /* Cancel out the call tip, user went elsewhere */
                        activeEditor.FunctionCallTipActive = false;
                        activeEditor.FunctionCallNode = null;
                        activeEditor.FunctionCallTipProgram = null;
                        TooltipManager.HideTooltip(activeEditor);
                    }

                }

                // Check if cursor is inside an interpolated string
                var currentLineText = ScintillaManager.GetCurrentLineText(activeEditor);
                var relativeLinePosition = cursorPosition - ScintillaManager.GetLineStartIndex(activeEditor, ScintillaManager.GetCurrentLineNumber(activeEditor));

                PeopleCodeLexer lexer = new PeopleCodeLexer(currentLineText);
                PeopleCodeParser.SelfHosted.PeopleCodeParser parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(lexer.TokenizeAll());
                var program = parser.ParseProgram();
                bool isInsideInterpolatedString = program.FindDescendants<InterpolatedStringNode>().Any(n => n.SourceSpan.ContainsPosition(relativeLinePosition));

                /* If we used to be in one, but aren't anymore... */
                if (!isInsideInterpolatedString)
                {
                    /* Check if the text has $" anywhere */
                    var fullText = ScintillaManager.GetScintillaText(activeEditor);
                    if (fullText != null && fullText.Contains("$\""))
                    {
                        if (refactorManager != null)
                        {
                            refactorManager.ExecuteRefactor(new ExpandInterpolatedStrings(activeEditor), activeEditor);
                        }
                    }
                    /* Execute the ExpandInterpolatedString  refactor */
                }

            }
            else if (m.Msg == AR_FUNCTION_CALL_TIP)
            {
                if (TooltipManager.IgnoreNextCallTip)
                {
                    TooltipManager.IgnoreNextCallTip = false;
                    return;
                }

                // Only process if we have an active editor
                if (activeEditor == null || !activeEditor.IsValid()) return;

                // Check if function signatures are enabled
                if (!autoSuggestSettings.FunctionSignatures) return;

                // wParam contains the cursor position
                int position = m.WParam.ToInt32();
                // lParam contains the character ('(', ')', or ',')
                char character = (char)m.LParam.ToInt32();

                Debug.Log($"Function call tip: character='{character}' at position={position}");
                if (character == ')')
                {
                    // Check for method extension pattern at current position
                    if (TypeExtensionManager != null)
                    {
                        try
                        {
                            // Use unified helper method
                            bool transformed = TryFindAndTransformExtension(
                                activeEditor,
                                position,
                                LanguageExtensionType.Method
                            );

                            if (transformed)
                            {
                                Debug.Log("Successfully executed method extension transform");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex, "Error checking for method extension on ')'");
                        }
                    }

                    // Always clear function call tip state (existing behavior)
                    activeEditor.FunctionCallTipActive = false;
                    activeEditor.FunctionCallNode = null;
                    activeEditor.FunctionCallTipProgram = null;
                    TooltipManager.HideTooltip(activeEditor);
                }
                else
                {
                    try
                    {
                        // Get the current document text
                        string content = ScintillaManager.GetScintillaText(activeEditor) ?? "";
                        if (string.IsNullOrEmpty(content))
                        {
                            Debug.Log("No content available for variable suggestions.");
                            return;
                        }

                        // Parse the current document to get AST
                        var lexer = new PeopleCodeLexer(content);
                        var tokens = lexer.TokenizeAll();
                        var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                        var program = parser.ParseProgram();

                        RunTypeInference(activeEditor, program, languageExtensionManager);

                        var targetFunction = program.FindNodes(n => n is FunctionCallNode && n.SourceSpan.ContainsPosition(position) && n.SourceSpan.Start.ByteIndex < position).OrderBy(n => n.SourceSpan.Length).FirstOrDefault();
                        var targetCreationNode = program.FindNodes(n => n is ObjectCreationNode && n.SourceSpan.ContainsPosition(position) && n.SourceSpan.Start.ByteIndex < position).OrderBy(n => n.SourceSpan.Length).FirstOrDefault();

                        bool isCreateMoreSpecific = (targetFunction != null && targetCreationNode != null && targetCreationNode.SourceSpan.Length < targetFunction.SourceSpan.Length);

                        if (targetFunction != null && targetFunction is FunctionCallNode fcn && !isCreateMoreSpecific)
                        {
                            activeEditor.FunctionCallTipProgram = program;
                            activeEditor.FunctionCallNode = fcn;
                            activeEditor.FunctionCallTipActive = true;
                            activeEditor.FunctionCallStartPosition = ScintillaManager.GetCursorPosition(activeEditor);
                            TooltipManager.ShowFunctionCallTooltip(activeEditor, program, fcn);
                        }
                        else if (targetCreationNode != null && targetCreationNode is ObjectCreationNode ocn)
                        {
                            FunctionCallNode fakeFCN = new FunctionCallNode(ocn, ocn.Arguments) { SourceSpan = ocn.SourceSpan };
                            foreach (var attr in ocn.Attributes)
                            {
                                fakeFCN.Attributes.Add(attr.Key, attr.Value);
                            }
                            fakeFCN.FirstToken = ocn.FirstToken;
                            fakeFCN.LastToken = ocn.LastToken;
                            activeEditor.FunctionCallTipProgram = program;
                            activeEditor.FunctionCallNode = fakeFCN;
                            activeEditor.FunctionCallTipActive = true;
                            activeEditor.FunctionCallStartPosition = ScintillaManager.GetCursorPosition(activeEditor);

                            TooltipManager.ShowFunctionCallTooltip(activeEditor, program, fakeFCN);
                        }

                    }
                    catch (Exception ex) { }
                }
            }
            else if (m.Msg == AR_OBJECT_MEMBERS)
            {
                // Only process if we have an active editor and service
                if (activeEditor == null || !activeEditor.IsValid() || autoCompleteService == null) return;

                // Check if object member suggestions are enabled
                if (!autoSuggestSettings.ObjectMembers) return;

                /* Handle object member suggestion request */
                Debug.Log($"Received object member suggest message. WParam: {m.WParam}, LParam: {m.LParam}");

                // WParam contains the current cursor position
                int position = m.WParam.ToInt32();

                try
                {
                    // Get the current document text
                    string content = ScintillaManager.GetScintillaText(activeEditor) ?? "";
                    if (string.IsNullOrEmpty(content))
                    {
                        Debug.Log("No content available for object member suggestions.");
                        return;
                    }

                    // Parse the current document to get AST
                    var lexer = new PeopleCodeLexer(content);
                    var tokens = lexer.TokenizeAll();
                    var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                    var program = parser.ParseProgram();

                    RunTypeInference(activeEditor, program, languageExtensionManager);

                    // Find the node just before the '.' character (position - 1)
                    // The position is after the '.', so we need to look at what comes before it
                    var targetPosition = position - 1;
                    var nodeBeforeDot = program.FindNodes(n => n.SourceSpan.ContainsPosition(targetPosition))
                        .LastOrDefault();

                    if (nodeBeforeDot != null)
                    {
                        var typeInfo = AstNodeExtensions.GetInferredType(nodeBeforeDot);

                        /* We need to work out the minimum visibility for methods/properties */
                        var maxVisibility = MemberVisibility.Public;

                        if (typeInfo is AppClassTypeInfo act && program.AppClass != null)
                        {
                            var currentQualifiedName = DetermineQualifiedName(activeEditor);
                            var activeClassTypeInfo = AppClassTypeInfo.CreateWithInheritanceChain(currentQualifiedName, activeAppDesigner.TypeResolver, activeAppDesigner.TypeResolver.Cache);

                            if (activeClassTypeInfo.InheritanceChain.Any(e => e.QualifiedName == act.QualifiedName))
                            {
                                maxVisibility = MemberVisibility.Protected;
                            }

                            if (activeClassTypeInfo.QualifiedName == act.QualifiedName)
                            {
                                maxVisibility = MemberVisibility.Private;
                            }
                        }

                        if (typeInfo != null)
                        {
                            Debug.Log($"Found type {typeInfo.Name} before dot at position {targetPosition}");

                            autoCompleteService.ShowObjectMembers(activeEditor, position, typeInfo, maxVisibility);
                        }
                        else
                        {
                            Debug.Log($"No type information available for node at position {targetPosition}");
                        }
                    }
                    else
                    {
                        Debug.Log($"No AST node found at position {targetPosition}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, "Error processing object member suggestion");
                }
            }
            else if (m.Msg == AR_SYSTEM_VARIABLE_SUGGEST)
            {
                // Only process if we have an active editor and service
                if (activeEditor == null || !activeEditor.IsValid() || autoCompleteService == null) return;

                // Check if system variable suggestions are enabled
                if (!autoSuggestSettings.SystemVariables) return;

                /* Handle system variable suggestion request */
                Debug.Log($"Received system variable suggest message. WParam: {m.WParam}, LParam: {m.LParam}");

                // WParam contains the current cursor position
                int position = m.WParam.ToInt32();

                try
                {
                    // Get the current document text
                    string content = ScintillaManager.GetScintillaText(activeEditor) ?? "";
                    if (string.IsNullOrEmpty(content))
                    {
                        Debug.Log("No content available for system variable suggestions.");
                        return;
                    }

                    // Parse the current document to get AST
                    var lexer = new PeopleCodeLexer(content);
                    var tokens = lexer.TokenizeAll();
                    var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                    var program = parser.ParseProgram();

                    RunTypeInference(activeEditor, program, languageExtensionManager);

                    // Try to determine expected type from context
                    // For now, use AnyTypeInfo to show all system variables
                    // TODO: Enhance to detect expected type from assignment/parameter context
                    var expectedType = PeopleCodeTypeInfo.Types.AnyTypeInfo.Instance;

                    Debug.Log($"Showing system variables with expected type: {expectedType.Name}");
                    autoCompleteService.ShowSystemVariables(activeEditor, program, position, expectedType);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, "Error processing system variable suggestion");
                }
            }
            else if (m.Msg == AR_SCINTILLA_ALREADY_LOADED)
            {
                IntPtr moduleHandle = m.WParam;
                Debug.Log($"Callback: Scintilla.dll is already loaded from the requested location (handle: 0x{moduleHandle:X}) - no replacement needed");
            }
            else if (m.Msg == AR_SCINTILLA_LOAD_SUCCESS)
            {
                IntPtr moduleHandle = m.WParam;
                Debug.Log($"Callback: Scintilla.dll loaded successfully at 0x{moduleHandle:X}");
            }
            else if (m.Msg == AR_SCINTILLA_LOAD_FAILED)
            {
                uint errorCode = (uint)m.WParam.ToInt32();
                Debug.Log($"Callback: Scintilla.dll load failed, error code {errorCode} (0x{errorCode:X})");
            }
            else if (m.Msg == AR_SCINTILLA_IN_USE)
            {
                Debug.Log("Callback: Scintilla.dll is in use (active windows exist, cannot replace)");
            }
            else if (m.Msg == AR_SCINTILLA_NOT_FOUND)
            {
                // Unpack version from wParam/lParam
                int major = (int)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                int minor = (int)(m.WParam.ToInt64() & 0xFFFF);
                int build = (int)((m.LParam.ToInt64() >> 16) & 0xFFFF);
                int revision = (int)(m.LParam.ToInt64() & 0xFFFF);
                string version = $"{major}.{minor}.{build}.{revision}";

                Debug.Log($"Callback: Target Scintilla.dll file not found (version {version})");
            }

            const int WM_AR_COMBO_BUTTON_CLICKED = 2519;
            if (m.Msg == WM_AR_COMBO_BUTTON_CLICKED)
            {
                // Handle button click
                Debug.Log("Button clicked!");
            }
            else if (m.Msg == AR_CONTEXT_MENU_OPTION)
            {
                int optionId = m.WParam.ToInt32();
                int toggleState = m.LParam.ToInt32();

                switch (optionId)
                {
                    case IDM_COMMAND_PALETTE:
                        Debug.Log("Command Palette selected from context menu");
                        BeginInvoke(new Action(() =>
                        {
                            ShowCommandPalette();
                        }));
                        break;

                    case IDM_MINIMAP:
                        Debug.Log($"Minimap toggle: {(toggleState != 0 ? "enabled" : "disabled")}");
                        // Minimap toggle is already handled in C++ hook
                        break;

                    case IDM_PARAM_NAMES:
                        Debug.Log($"Param Names toggle: {(toggleState != 0 ? "enabled" : "disabled")}");
                        HandleParamNamesToggle(toggleState != 0);
                        break;

                    default:
                        Debug.Log($"Unknown context menu option: {optionId}");
                        break;
                }
            }
        }

        private static void RunTypeInference(ScintillaEditor editor, ProgramNode program, LanguageExtensions.TypeExtensionManager? extensionManager = null)
        {
            try
            {
                var qualifiedName = DetermineQualifiedName(editor);

                var metadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);

                // Get type resolver (may be null if no database)
                var typeResolver = editor.AppDesignerProcess?.TypeResolver;

                string? defaultRecord = null;
                string? defaultField = null;
                if (editor.Caption.EndsWith("(Record PeopleCode)"))
                {
                    var parts = qualifiedName.Split('.');
                    defaultRecord = parts[0];
                    defaultField = parts[1];
                }

                // Run type inference (works even with null resolver)
                TypeInferenceVisitor.Run(
                    program,
                    metadata,
                    typeResolver,
                    defaultRecord,
                    defaultField,
                    inferAutoDeclaredTypes: false,
                    onUndefinedVariable: extensionManager != null ? extensionManager.HandleUndefinedVariable : null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error running type inference for tooltips: {ex.Message}");
            }
        }

        public bool TryFindAndTransformExtension(
            ScintillaEditor editor,
            int position,
            LanguageExtensionType extensionType)
        {
            try
            {
                // Get current document text
                string content = ScintillaManager.GetScintillaText(editor) ?? "";
                while (content[position - 1] == ';')
                {
                    position--;
                }

                if (string.IsNullOrEmpty(content))
                {
                    Debug.Log("No content available for extension transform");
                    return false;
                }

                // Parse and run type inference
                var lexer = new PeopleCodeLexer(content);
                var tokens = lexer.TokenizeAll();
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                var program = parser.ParseProgram();

                if (program == null)
                {
                    Debug.Log("Failed to parse for extension transform");
                    return false;
                }

                RunTypeInference(editor, program, languageExtensionManager);

                // Run scope annotation visitor to build variable registry
                // We'll annotate the target node after we find it
                VariableRegistry? variableRegistry = null;
                AstNode? targetNode = null;

                // Find the appropriate node at cursor position
                if (extensionType == LanguageExtensionType.Property)
                {
                    // Find MemberAccessNode containing the position
                    var memberAccessNode = program.FindNodes(n =>
                        n is MemberAccessNode &&
                        n.SourceSpan.ContainsPosition(position)
                    ).OfType<MemberAccessNode>().FirstOrDefault();

                    if (memberAccessNode != null)
                    {
                        var targetExprNode = memberAccessNode.Target;
                        var memberName = memberAccessNode.MemberName;
                        var targetType = targetExprNode.GetInferredType();

                        if (targetType != null && TypeExtensionManager != null)
                        {
                            var extensions = TypeExtensionManager.GetExtensionsForTypeAndName(
                                targetType, memberName, LanguageExtensionType.Property);

                            if (extensions.Count > 0)
                            {
                                var transform = extensions[0];

                                // Get the matched type from the extension
                                if (transform.ParentExtension == null) return false;
                                TypeInfo matchedType = transform.ParentExtension.TargetType;

                                // Run scope annotation visitor for this specific node
                                targetNode = memberAccessNode;
                                var scopeVisitor = new ScopeAnnotationVisitor(targetNode);
                                program.Accept(scopeVisitor);
                                variableRegistry = scopeVisitor.VariableRegistry;

                                Debug.Log($"Executing property extension transform: {transform.GetName()}");
                                transform.TransformAction(editor, memberAccessNode, matchedType, variableRegistry);
                                TooltipManager.IgnoreNextCallTip = true;

                                Task.Delay(500).ContinueWith(_ => TooltipManager.IgnoreNextCallTip = false);

                                TooltipManager.HideTooltip(editor);

                                return true;
                            }
                        }
                    }
                }
                else // Method extension
                {
                    // Find FunctionCallNode with MemberAccess function
                    var functionCallNode = program.FindNodes(n =>
                        n is FunctionCallNode &&
                        n.SourceSpan.ContainsPosition(position)
                    ).OfType<FunctionCallNode>()
                     .FirstOrDefault(fc => fc.Function is MemberAccessNode);

                    if (functionCallNode?.Function is MemberAccessNode memberAccess)
                    {
                        var targetExprNode = memberAccess.Target;
                        var methodName = memberAccess.MemberName;
                        var targetType = targetExprNode.GetInferredType();

                        if (targetType != null && TypeExtensionManager != null)
                        {
                            var extensions = TypeExtensionManager.GetExtensionsForTypeAndName(
                                targetType, methodName, LanguageExtensionType.Method);

                            if (extensions.Count > 0)
                            {
                                var transform = extensions[0];

                                // Get the matched type from the extension
                                if (transform.ParentExtension == null) return false;
                                TypeInfo matchedType = transform.ParentExtension.TargetType;

                                // Run scope annotation visitor for this specific node
                                targetNode = functionCallNode;
                                var scopeVisitor = new ScopeAnnotationVisitor(targetNode);
                                program.Accept(scopeVisitor);
                                variableRegistry = scopeVisitor.VariableRegistry;

                                Debug.Log($"Executing method extension transform: {transform.GetName()}");
                                transform.TransformAction(editor, functionCallNode, matchedType, variableRegistry);
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error in TryFindAndTransformExtension");
                return false;
            }
        }

        private void UpdateSavedFoldsForEditor(ScintillaEditor? editor)
        {
            if (editor == null) return;

            if (editor != null && editor.IsValid())
            {
                var collapsedFoldPaths = FoldingManager.GetCollapsedFoldPathsDirectly(editor);
                if (collapsedFoldPaths.Count > 0)
                {
                    editor.CollapsedFoldPaths = collapsedFoldPaths;
                    FoldingManager.PrintCollapsedFoldPathsDebug(collapsedFoldPaths);
                    if (chkRememberFolds.Checked)
                    {
                        FoldingManager.UpdatePersistedFolds(editor);
                    }
                }
            }
        }

        public void ShowOutlineCommand()
        {
            if (activeEditor == null) return;

            try
            {
                // Parse the content using the parser
                var definitions = CollectOutlineDefinitions();

                if (definitions.Count == 0)
                {
                    Debug.Log("No definitions found in the file");
                    return;
                }

                // Create and show the definition selection dialog
                IntPtr mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;

                ShowOutlineDialog(definitions, mainHandle);

            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in GoToDefinitionCommand: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows the definition selection dialog
        /// </summary>
        private void ShowOutlineDialog(List<OutlineItem> definitions, IntPtr mainHandle)
        {
            var handleWrapper = new WindowWrapper(mainHandle);
            var dialog = new OutlineDialog(definitions, mainHandle);

            DialogResult result = dialog.ShowDialog(handleWrapper);

            if (result == DialogResult.OK)
            {
                OutlineItem? selectedDefinition = dialog.SelectedDefinition;
                if (activeEditor != null && selectedDefinition != null)
                {
                    // Navigate to the selected definition
                    ScintillaManager.SetCursorPosition(activeEditor, selectedDefinition.Position);

                    /* Get line from position and make that line the first visible line */
                    var lineNumber = ScintillaManager.GetLineFromPosition(activeEditor, selectedDefinition.Position);
                    ScintillaManager.SetFirstVisibleLine(activeEditor, lineNumber);
                    var startIndex = ScintillaManager.GetLineStartIndex(activeEditor, lineNumber);
                    var length = ScintillaManager.GetLineLength(activeEditor, lineNumber);
                    ScintillaManager.SetSelection(activeEditor, startIndex, startIndex + length);

                }
            }
        }

        /// <summary>
        /// Collects all definitions from the editor using the ANTLR parser
        /// </summary>
        /// <param name="editor">The editor to collect definitions from</param>
        /// <returns>A list of code definitions</returns>
        private List<OutlineItem> CollectOutlineDefinitions()
        {
            if (activeEditor == null) return [];

            // Use the parse tree from the editor if available, otherwise parse it now
            var program = activeEditor.GetParsedProgram();

            // Create and run the visitor to collect definitions
            var visitor = new OutlineVisitor();
            program?.Accept(visitor);

            return visitor.Definitions ?? [];
        }

        /// <summary>
        /// F12 Go to Definition - Context-sensitive navigation to symbol definition at cursor position
        /// </summary>
        public void GoToDefinitionCommand()
        {
            if (activeEditor == null) return;

            try
            {
                // Get current cursor position
                int cursorPosition = ScintillaManager.GetCursorPosition(activeEditor);
                Debug.Log($"F12 Go to Definition at cursor position: {cursorPosition}");

                // Parse the content to get AST
                var program = activeEditor.GetParsedProgram();
                if (program == null)
                {
                    Debug.Log("Failed to parse program for go to definition");

                    // Show error message
                    IntPtr mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                    var handleWrapper = new WindowWrapper(mainHandle);
                    Task.Delay(100).ContinueWith(_ =>
                    {
                        new MessageBoxDialog(
                            "Unable to parse the current file for go to definition.",
                            "Go to Definition",
                            MessageBoxButtons.OK,
                            mainHandle
                        ).ShowDialog(handleWrapper);
                    });
                    return;
                }

                // Use new scope-aware resolver with optional database support
                var goToVisitor = new GoToDefinitionVisitor(program, cursorPosition, activeEditor.DataManager);

                goToVisitor.VisitProgram(program);

                var result = goToVisitor.Result;

                if (result == null)
                {
                    string errorMsg = "Unable to determine symbol at cursor position";
                    Debug.Log($"Definition resolution failed");

                    // Show error message
                    IntPtr mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                    var handleWrapper = new WindowWrapper(mainHandle);
                    Task.Delay(100).ContinueWith(_ =>
                    {
                        new MessageBoxDialog(
                            errorMsg,
                            "Go to Definition",
                            MessageBoxButtons.OK,
                            mainHandle
                        ).ShowDialog(handleWrapper);
                    });
                    return;
                }

                // BEFORE navigating, capture current location for navigation history
                // Only capture if we found a valid navigation target
                if ((result.TargetProgram != null || result.SourceSpan != null) && activeEditor.AppDesignerProcess != null)
                {
                    // Try to parse current editor caption to OpenTarget
                    var currentOpenTarget = OpenTargetBuilder.CreateFromCaption(activeEditor.Caption);
                    if (currentOpenTarget != null)
                    {
                        // Get current selection or cursor position
                        var (_, selectionStart, selectionEnd) = ScintillaManager.GetSelectedText(activeEditor);

                        // If no selection, use cursor position for both start and end
                        if (selectionStart == selectionEnd)
                        {
                            selectionStart = cursorPosition;
                            selectionEnd = cursorPosition;
                        }

                        var currentSourceSpan = new SourceSpan(
                            new SourcePosition(selectionStart),
                            new SourcePosition(selectionEnd)
                        );

                        // Get current first visible line for scroll restoration
                        int firstVisibleLine = ScintillaManager.GetFirstVisibleLine(activeEditor);

                        // Create navigation history entry
                        var historyEntry = new NavigationHistoryEntry(
                            currentOpenTarget,
                            currentSourceSpan,
                            firstVisibleLine,
                            activeEditor.hWnd
                        );

                        // Push to navigation history (this will prune forward history)
                        activeEditor.AppDesignerProcess.PushNavigationLocation(historyEntry);
                        Debug.Log($"Pushed current location to navigation history: {currentOpenTarget.Name}");
                    }
                    else
                    {
                        Debug.Log("Warning: Could not parse current editor caption to OpenTarget for navigation history");
                    }
                }

                IntPtr mainWindowHandle = activeEditor.AppDesignerProcess.MainWindowHandle;

                // Handle cross-program navigation (base class)
                if (result.TargetProgram != null)
                {
                    activeEditor.AppDesignerProcess.PendingSelection = result.SourceSpan;
                    if (result.SourceSpan != null)
                    {
                        activeEditor.AppDesignerProcess.SetOpenTarget(BuildOpenTargetString(result.TargetProgram, result.SourceSpan));
                    }
                    return;
                }

                // Handle in-file navigation
                if (result.SourceSpan != null)
                {
                    int position = result.SourceSpan.Value.Start.ByteIndex;

                    // Navigate to variable declaration
                    ScintillaManager.SetCursorPosition(activeEditor, position);
                    ScintillaManager.SetSelection(activeEditor, result.SourceSpan.Value.Start.ByteIndex, result.SourceSpan.Value.End.ByteIndex);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in GoToDefinitionCommand: {ex.Message}");
                Debug.LogException(ex, "GoToDefinitionCommand exception");
            }
        }

        /// <summary>
        /// Navigate backward in navigation history (Alt+Left)
        /// </summary>
        public void NavigateBackwardCommand()
        {
            if (activeEditor == null || activeAppDesigner == null) return;

            try
            {
                // If we're at a location beyond the stack, save it first so we can navigate forward later
                if (activeAppDesigner.NavigationHistoryIndex == activeAppDesigner.NavigationHistory.Count)
                {
                    var currentOpenTarget = OpenTargetBuilder.CreateFromCaption(activeEditor.Caption);
                    if (currentOpenTarget != null)
                    {
                        int cursorPosition = ScintillaManager.GetCursorPosition(activeEditor);
                        var (_, selectionStart, selectionEnd) = ScintillaManager.GetSelectedText(activeEditor);
                        if (selectionStart == selectionEnd)
                        {
                            selectionStart = selectionEnd = cursorPosition;
                        }
                        var currentSourceSpan = new SourceSpan(
                            new SourcePosition(selectionStart),
                            new SourcePosition(selectionEnd)
                        );
                        int firstVisibleLine = ScintillaManager.GetFirstVisibleLine(activeEditor);
                        var currentEntry = new NavigationHistoryEntry(
                            currentOpenTarget,
                            currentSourceSpan,
                            firstVisibleLine,
                            activeEditor.hWnd
                        );
                        activeAppDesigner.NavigationHistory.Add(currentEntry);
                        Debug.Log($"Saved current location before navigating back: {currentOpenTarget.Name}");
                    }
                }

                // Check if we can navigate backward
                if (!activeAppDesigner.CanNavigateBackward())
                {
                    Debug.Log("Cannot navigate backward - at beginning of history");
                    return;
                }

                // Get the previous location from history
                var entry = activeAppDesigner.NavigateBackward();
                if (entry == null)
                {
                    Debug.Log("NavigateBackward returned null");
                    return;
                }

                // Validate editor handle is still valid
                if (!WinApi.IsWindow(entry.EditorHandle))
                {
                    Debug.Log($"Navigation history entry has invalid editor handle: {entry.EditorHandle:X}");
                    // Try navigating again (skip invalid entries)
                    NavigateBackwardCommand();
                    return;
                }

                Debug.Log($"Navigating backward to: {entry.OpenTarget.Name}");

                // Check if we need to navigate to a different file or within same file
                bool isSameEditor = entry.EditorHandle == activeEditor.hWnd;

                if (isSameEditor)
                {
                    // Same file - just restore position and scroll
                    ScintillaManager.SetCursorPosition(activeEditor, entry.SourceSpan.Start.ByteIndex);
                    ScintillaManager.SetSelection(activeEditor, entry.SourceSpan.Start.ByteIndex, entry.SourceSpan.End.ByteIndex);
                    ScintillaManager.SetFirstVisibleLine(activeEditor, entry.FirstVisibleLine);
                }
                else
                {
                    // Different file - use SetOpenTarget with PendingSelection
                    activeAppDesigner.PendingSelection = entry.SourceSpan;
                    string openTargetString = BuildOpenTargetString(entry.OpenTarget);
                    activeAppDesigner.SetOpenTarget(openTargetString);

                    // Store first visible line for restoration after window opens
                    // TODO: May need to store this in PendingSelection or a separate field
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in NavigateBackwardCommand: {ex.Message}");
                Debug.LogException(ex, "NavigateBackwardCommand exception");
            }
        }

        /// <summary>
        /// Navigate forward in navigation history (Alt+Right)
        /// </summary>
        public void NavigateForwardCommand()
        {
            if (activeEditor == null || activeAppDesigner == null) return;

            try
            {
                // Check if we can navigate forward
                if (!activeAppDesigner.CanNavigateForward())
                {
                    Debug.Log("Cannot navigate forward - at end of history");
                    return;
                }

                // Get the next location from history
                var entry = activeAppDesigner.NavigateForward();
                if (entry == null)
                {
                    Debug.Log("NavigateForward returned null");
                    return;
                }

                // Validate editor handle is still valid
                if (!WinApi.IsWindow(entry.EditorHandle))
                {
                    Debug.Log($"Navigation history entry has invalid editor handle: {entry.EditorHandle:X}");
                    // Try navigating again (skip invalid entries)
                    NavigateForwardCommand();
                    return;
                }

                Debug.Log($"Navigating forward to: {entry.OpenTarget.Name}");

                // Check if we need to navigate to a different file or within same file
                bool isSameEditor = entry.EditorHandle == activeEditor.hWnd;

                if (isSameEditor)
                {
                    // Same file - just restore position and scroll
                    ScintillaManager.SetCursorPosition(activeEditor, entry.SourceSpan.Start.ByteIndex);
                    ScintillaManager.SetSelection(activeEditor, entry.SourceSpan.Start.ByteIndex, entry.SourceSpan.End.ByteIndex);
                    ScintillaManager.SetFirstVisibleLine(activeEditor, entry.FirstVisibleLine);
                }
                else
                {
                    // Different file - use SetOpenTarget with PendingSelection
                    activeAppDesigner.PendingSelection = entry.SourceSpan;
                    string openTargetString = BuildOpenTargetString(entry.OpenTarget);
                    activeAppDesigner.SetOpenTarget(openTargetString);

                    // Store first visible line for restoration after window opens
                    // TODO: May need to store this in PendingSelection or a separate field
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in NavigateForwardCommand: {ex.Message}");
                Debug.LogException(ex, "NavigateForwardCommand exception");
            }
        }

        /// <summary>
        /// Generates a type error report for GitHub submission
        /// </summary>
        public void GenerateTypeErrorReportCommand()
        {
            if (activeEditor == null) return;

            try
            {
                // Create reporter and generate report
                var reporter = new TypeErrorReporter();
                var report = reporter.GenerateReport(activeEditor, languageExtensionManager);

                if (report == null)
                {
                    // No errors found at cursor position
                    var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                    var handleWrapperNoError = new WindowWrapper(mainHandle);

                    Task.Delay(100).ContinueWith(_ =>
                    {
                        new MessageBoxDialog(
                            "No type errors found at the current cursor position.",
                            "No Errors Found",
                            MessageBoxButtons.OK,
                            mainHandle).ShowDialog(handleWrapperNoError);
                    });
                    return;
                }

                // Show report dialog
                var ownerHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                var handleWrapper = new WindowWrapper(ownerHandle);
                var dialog = new TypeErrorReportDialog(report, ownerHandle);
                dialog.ShowDialog(handleWrapper);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error generating type error report");

                var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
                var handleWrapperError = new WindowWrapper(mainHandle);

                Task.Delay(100).ContinueWith(_ =>
                {
                    new MessageBoxDialog(
                        $"Error generating type error report: {ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        mainHandle).ShowDialog(handleWrapperError);
                });
            }
        }

        internal void ApplyTemplateCommand()
        {
            /* only work if there's an active editor */
            if (activeEditor == null) return;

            // Get all available templates
            var templates = Template.GetAvailableTemplates();

            if (templates.Count == 0)
            {
                MessageBox.Show("No templates found.", "No Templates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var mainHandle = activeEditor.AppDesignerProcess.MainWindowHandle;
            var handleWrapper = new WindowWrapper(mainHandle);

            // Show template selection dialog
            using var templateDialog = new TemplateSelectionDialog(templates, mainHandle);
            if (templateDialog.ShowDialog(handleWrapper) != DialogResult.OK || templateDialog.SelectedTemplate == null)
            {
                return;
            }

            // Check for replacement warning only if it's not insert mode
            if (!templateDialog.SelectedTemplate.IsInsertMode && !string.IsNullOrWhiteSpace(ScintillaManager.GetScintillaText(activeEditor)))
            {
                using var confirmDialog = new TemplateConfirmationDialog(
                    "Applying a template will replace all content in the current editor. Do you want to continue?",
                    mainHandle);

                if (confirmDialog.ShowDialog(handleWrapper) != DialogResult.Yes)
                {
                    return;
                }
            }

            var selectedTemplate = templateDialog.SelectedTemplate;
            templateManager.ActiveTemplate = selectedTemplate;
            templateManager.PromptForInputs(mainHandle, handleWrapper);
            templateManager.ApplyActiveTemplateToEditor(activeEditor);
        }

        private void btnDebugLog_Click(object sender, EventArgs e)
        {


            Debug.Log("Displaying debug dialog...");
            Debug.ShowDebugDialog(Handle);
            //Debug.ShowIndicatorPanel(Handle, this);
        }


        private static string FormatPrefixText(ScintillaEditor activeEditor, List<EventMapItem> overrideItems, List<EventMapItem> preItems, bool showClassText)
        {
            StringBuilder sb = new();
            sb.Append("Event Mapping Information:\n");
            /* Handle override items */
            var groups = overrideItems.GroupBy(i => i.ContentReference);
            foreach (var g in groups)
            {
                var cref = g.Key;
                var item = g.First();
                var pageComponentNote = string.Empty;
                var padding = "";
                sb.Append($"Content Reference: {cref}\n");
                padding = "   ";
                if (activeEditor.EventMapInfo?.Type == EventMapType.Page)
                {
                    pageComponentNote = $"When viewed on Component: {item.Component}.{item.Segment}";
                    padding = padding + "   ";
                }
                sb.Append($"{padding}WARNING:{pageComponentNote} This code is currently being overriden by an event mapped class.\nClass: {item.PackageRoot}:{item.PackagePath}:{item.ClassName}\n");
            }


            /* Handle Pre sequence items */
            groups = preItems.GroupBy(i => i.ContentReference);
            foreach (var g in groups)
            {
                var cref = g.Key;
                var pageComponentNote = string.Empty;
                var padding = "";
                sb.Append($"Content Reference: {cref}\n");
                padding = "   ";
                foreach (var item in g)
                {
                    if (activeEditor.EventMapInfo?.Type == EventMapType.Page)
                    {
                        pageComponentNote = $"When viewed on Component: {item.Component}.{item.Segment}";
                        padding = padding + "   ";
                    }
                    if (showClassText && activeEditor.EventMapInfo?.Type != EventMapType.Page)
                    {
                        sb.Append($"{padding}/****************************************************************************************\n");
                        sb.Append($"{padding}/* Sequence: {item.SeqNumber}) {pageComponentNote} Event Mapped Pre Class: {item.PackageRoot}:{item.PackagePath}:{item.ClassName} */\n");
                        sb.Append($"{padding}****************************************************************************************/\n");
                        sb.Append(GetClassTextWithPadding(activeEditor, item, padding + "   "));
                        sb.Append('\n');
                    }
                    else
                    {
                        sb.Append($"{padding}(Sequence: {item.SeqNumber}){pageComponentNote} Event Mapped Pre Class: {item.PackageRoot}:{item.PackagePath}:{item.ClassName}\n");
                    }
                }
            }

            return sb.ToString();
        }

        private static string FormatPostfixText(ScintillaEditor activeEditor, List<EventMapItem> postItems, bool showClassText)
        {
            StringBuilder sb = new();
            sb.Append("Event Mapping Information:\n");
            /* Handle Pre sequence items */
            var groups = postItems.GroupBy(i => i.ContentReference);
            foreach (var g in groups)
            {
                var cref = g.Key;
                var pageComponentNote = string.Empty;
                var padding = "";
                sb.Append($"Content Reference: {cref}\n");
                padding = "   ";
                foreach (var item in g)
                {
                    if (activeEditor.EventMapInfo?.Type == EventMapType.Page)
                    {
                        pageComponentNote = $"When viewed on Component: {item.Component}.{item.Segment}";
                        padding = padding + "   ";
                    }
                    if (showClassText && activeEditor.EventMapInfo?.Type != EventMapType.Page)
                    {
                        sb.Append($"{padding}/****************************************************************************************\n");
                        sb.Append($"{padding}/* Sequence: {item.SeqNumber}) {pageComponentNote} Event Mapped Post Class: {item.PackageRoot}:{item.PackagePath}:{item.ClassName} */\n");
                        sb.Append($"{padding}****************************************************************************************/\n");
                        sb.Append(GetClassTextWithPadding(activeEditor, item, padding + "   "));
                        sb.Append('\n');
                    }
                    else
                    {
                        sb.Append($"{padding}(Sequence: {item.SeqNumber}){pageComponentNote} Event Mapped Post Class: {item.PackageRoot}:{item.PackagePath}:{item.ClassName}\n");
                    }
                }
            }

            return sb.ToString();
        }

        private static string GetClassTextWithPadding(ScintillaEditor editor, EventMapItem item, string padding)
        {
            var source = editor.DataManager?.GetAppClassSourceByPath($"{item.PackageRoot}:{item.PackagePath}:{item.ClassName}") ?? "/* <Source not found> */";

            var lines = source.Split('\n');
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                sb.Append($"{padding}{line}\n");
            }
            return sb.ToString();
        }


        private void ProcessEventMapping()
        {
            if (activeEditor == null || activeEditor.DataManager == null) return;
            var editorCleanState = ScintillaManager.IsEditorClean(activeEditor);
            Debug.Log($"Editor clean state: {editorCleanState}");
            var checkForEventMapping = chkEventMapping.Checked;
            var checkForEventMapXrefs = chkEventMapXrefs.Checked;
            Debug.Log($"Event map info: {activeEditor.EventMapInfo}");
            if (checkForEventMapping && activeEditor.EventMapInfo != null)
            {
                var showClassText = optClassText.Checked;
                Debug.Log($"Show class text: {showClassText}");

                var items = activeEditor.DataManager.GetEventMapItems(activeEditor.EventMapInfo);

                Debug.Log($"EventMap items: {items.Count}");

                var preItems = items.Where(i => i.Sequence == EventMapSequence.Pre).OrderBy(i => i.SeqNumber).ToList();
                var postItems = items.Where(i => i.Sequence == EventMapSequence.Post).OrderBy(i => i.SeqNumber).ToList();
                var overrideItems = items.Where(i => i.Sequence == EventMapSequence.Replace).OrderBy(i => i.SeqNumber).ToList();

                Debug.Log($"Pre items: {preItems.Count}, Post items: {postItems.Count}, Override items: {overrideItems.Count}");

                Debug.Log($"Inserting event mapping information...");
                if (overrideItems.Count + preItems.Count > 0)
                {
                    ScintillaManager.InsertTextAtLocation(activeEditor, 0, "\n");
                    var preText = FormatPrefixText(activeEditor, overrideItems, preItems, showClassText);
                    ScintillaManager.SetAnnotation(activeEditor, 0, preText, AnnotationStyle.Gray);
                }


                if (postItems.Count > 0)
                {
                    Debug.Log($"Inserting event mapping information:");
                    var lineCount = ScintillaManager.GetLineCount(activeEditor);
                    var postText = FormatPostfixText(activeEditor, postItems, showClassText);
                    ScintillaManager.SetAnnotation(activeEditor, lineCount - 1, postText, AnnotationStyle.Gray);
                }

            }

            if (checkForEventMapXrefs)
            {
                if (activeEditor.ClassPath != string.Empty)
                {
                    var xrefs = activeEditor.DataManager.GetEventMapXrefs(activeEditor.ClassPath);
                    var groups = xrefs.GroupBy(x => x.ContentReference);

                    if (xrefs.Count > 0)
                    {
                        StringBuilder sb = new();
                        sb.Append("Event Mapping Xrefs:\n");

                        foreach (var g in groups)
                        {
                            sb.Append($"Content Reference: {g.Key}\n");
                            foreach (var xref in g)
                            {
                                sb.Append($"  {xref}\n");
                            }
                        }

                        ScintillaManager.InsertTextAtLocation(activeEditor, 0, "\n");
                        ScintillaManager.SetAnnotation(activeEditor, 0, sb.ToString(), AnnotationStyle.Gray);
                    }
                }
            }
            if (editorCleanState)
            {
                Debug.Log("Resetting save point");
                ScintillaManager.SetSavePoint(activeEditor, true);
            }
        }

        // Handle a newly detected editor
        private void ProcessNewEditor(ScintillaEditor editor)
        {
            if (editor == null) return;

            if (!editor.IsValid())
            {
                editor.Cleanup();
                return;
            }

            EnableUIActions();

            int SCMOD_SHIFT = 1;
            int SCK_DOWN = 300;
            int SCK_UP = 301;
            int SCI_LINEUPEXTEND = 2303;
            int SCI_LINEDOWNEXTEND = 2301;
            int SCI_ASSIGNCMDKEY = 2070;
            WinApi.SendMessage(editor.hWnd, SCI_ASSIGNCMDKEY, SCK_UP + (SCMOD_SHIFT << 16), SCI_LINEUPEXTEND);
            WinApi.SendMessage(editor.hWnd, SCI_ASSIGNCMDKEY, SCK_DOWN + (SCMOD_SHIFT << 16), SCI_LINEDOWNEXTEND);

            // If "only PPC" is checked and the editor is not PPC, skip
            if (chkOnlyPPC.Checked && editor.Type != EditorType.PeopleCode)
            {
                return;
            }

            /* If promptForDB is set, lets check if we have a datamanger already? if not, prompt for a db connection */
            if (chkPromptForDB.Checked && editor.DataManager == null && editor.AppDesignerProcess.DoNotPromptForDB != true)
            {
                ConnectToDB();
            }
            Debug.Log($"Event mapping flags: {chkEventMapping.Checked}, {chkEventMapXrefs.Checked}");
            if (editor.DataManager != null && (chkEventMapping.Checked || chkEventMapXrefs.Checked))
            {
                Debug.Log($"Processing event mapping for editor: {editor.RelativePath}");
                ProcessEventMapping();
            }

        }


        private void ConnectToDB()
        {
            if (activeAppDesigner == null) return;

            var mainHandle = activeAppDesigner.MainWindowHandle;
            var handleWrapper = new WindowWrapper(mainHandle);

            // Pass the editor's DBName to the dialog constructor
            DBConnectDialog dialog = new(mainHandle, activeAppDesigner.DBName);
            dialog.StartPosition = FormStartPosition.CenterParent;
            if (dialog.ShowDialog(handleWrapper) == DialogResult.OK)
            {
                IDataManager? manager = dialog.DataManager;

                if (manager != null)
                {
                    activeAppDesigner.DataManager = manager;
                    foreach (var editor in activeAppDesigner.Editors.Values)
                    {
                        editor.DataManager = manager;
                    }

                    // Force refresh all editors to allow DB-dependent stylers to run
                    RefreshAllEditorsAfterDatabaseConnection();
                }
            }
            else
            {
                activeAppDesigner.DoNotPromptForDB = true;
            }
        }

        /// <summary>
        /// Saves the content of the editor to the Snapshot database
        /// </summary>
        /// <param name="editor">The editor to save content from</param>
        private void SaveSnapshot(ScintillaEditor editor)
        {
            if (editor == null || string.IsNullOrEmpty(editor.RelativePath))
            {
                return;
            }

            try
            {
                // Get content from editor
                string? content = ScintillaManager.GetScintillaText(editor);
                if (string.IsNullOrEmpty(content))
                {
                    Debug.Log($"No content to save for editor: {editor.hWnd:X}");
                    return;
                }

                // Save and commit the content
                snapshotManager?.SaveEditorSnapshot(editor, content);
            }
            catch (Exception ex)
            {
                Debug.Log($"Error saving to snapshot database: {ex.Message}");
            }
        }

        // Add this new method to process the savepoint after debouncing
        private void ProcessSavepoint(object? _)
        {
            ScintillaEditor? editorToSave = null;

            lock (savepointLock)
            {
                // If there's no pending editor, just return
                if (pendingSaveEditor == null)
                    return;

                // Get the editor to save
                editorToSave = pendingSaveEditor;

                // Clear the pending editor
                pendingSaveEditor = null;
            }

            try
            {
                // Make sure we're on the UI thread
                this.Invoke(() =>
                {
                    // Check if the editor is still valid
                    if (editorToSave != null && editorToSave.IsValid())
                    {
                        /* Esnure caption (and thus relative path) is accurate. */
                        var caption = WindowHelper.GetGrandparentWindowCaption(editorToSave.hWnd);
                        if (caption == "Suppress")
                        {
                            Thread.Sleep(1000);
                            caption = WindowHelper.GetGrandparentWindowCaption(editorToSave.hWnd);
                        }

                        editorToSave.Caption = caption;



                        Debug.Log($"Processing debounced SAVEPOINTREACHED for {editorToSave.RelativePath}");
                        lock (editorToSave)
                        {
                            if (editorToSave.ExpectingSavePoint)
                            {
                                // Remove the editor from the list of expecting save points
                                editorToSave.ExpectingSavePoint = false;
                                return;
                            }
                        }

                        // Note: We no longer unconditionally clear stylers on save
                        // Stylers remain active since saving doesn't change code structure
                        // Only clear if content actually changed, which will be handled by CheckForContentChanges

                        // Save content to Snapshot database
                        if (!string.IsNullOrEmpty(editorToSave.RelativePath))
                        {
                            // Reset editor state
                            editorToSave.ContentString = ScintillaManager.GetScintillaText(editorToSave);
                            SaveSnapshot(editorToSave);
                        }

                        // Invalidate type metadata cache for this program
                        InvalidateTypeCacheForEditor(editorToSave);

                        Debug.Log("Event mapping flags: " + chkEventMapping.Checked + ", " + chkEventMapXrefs.Checked);
                        if (editorToSave.Type == EditorType.PeopleCode &&
                            editorToSave.DataManager != null &&
                            (chkEventMapping.Checked || chkEventMapXrefs.Checked))
                        {
                            Debug.Log($"Processing event mapping for editor: {editorToSave.RelativePath}");
                            ProcessEventMapping();
                        }

                        // Update function cache asynchronously for PeopleCode editors
                        if (editorToSave.Type == EditorType.PeopleCode &&
                            functionCacheManager != null &&
                            editorToSave.IsParseSuccessful)
                        {
                            // Run cache update on background thread to avoid blocking save
                            Task.Run(() =>
                            {
                                try
                                {
                                    functionCacheManager.UpdateCacheForEditor(editorToSave);
                                }
                                catch (Exception ex)
                                {
                                    Debug.Log($"Error updating function cache on save: {ex.Message}");
                                }
                            });
                        }

                        if (chkBetterSQL.Checked && editorToSave.Type == EditorType.SQL)
                        {
                            ScintillaManager.ApplyBetterSQL(editorToSave);
                        }

                        /* Reapplying code folds */
                        FoldingManager.ApplyCollapsedFoldPaths(editorToSave);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.Log($"Error processing debounced savepoint: {ex.Message}");
                Debug.Log(ex.StackTrace);
            }
        }

        /// <summary>
        /// Determines the qualified name for the current program for type inference.
        /// </summary>
        private static string DetermineQualifiedName(ScintillaEditor editor)
        {
            if (editor?.Caption != null && !string.IsNullOrWhiteSpace(editor.Caption))
            {
                // Parse caption to get program identifier
                var openTarget = OpenTargetBuilder.CreateFromCaption(editor.Caption);
                if (openTarget != null && editor.Caption.Contains("Application Package"))
                {
                    var methodIndex = Array.IndexOf(openTarget.ObjectIDs, PSCLASSID.METHOD);
                    openTarget.ObjectIDs[methodIndex] = PSCLASSID.NONE;
                    openTarget.ObjectValues[methodIndex] = null;
                    return openTarget.Path;
                }
                else
                {
                    /* probably never what you want but we have to return something? */
                    return string.Join(".", openTarget.ObjectValues);
                }
            }
            else
            {
                /* probably never what you want but we have to return something? */
                return "Program";
            }
        }

        /// <summary>
        /// Refreshes all editors after a database connection is established.
        /// This allows stylers and checkers that provide enhanced information when a DB connection
        /// is available to run immediately after the connection is established.
        /// </summary>
        internal void RefreshAllEditorsAfterDatabaseConnection()
        {
            if (activeAppDesigner == null)
            {
                Debug.Log("RefreshAllEditorsAfterDatabaseConnection: No active AppDesigner process");
                return;
            }

            Debug.Log("RefreshAllEditorsAfterDatabaseConnection: Refreshing all editors with new database connection");

            foreach (var editor in activeAppDesigner.Editors.Values)
            {
                try
                {
                    if (editor == null || !editor.IsValid() || editor.Type != EditorType.PeopleCode)
                    {
                        continue; // Only process valid PeopleCode editors
                    }

                    Debug.Log($"RefreshAllEditorsAfterDatabaseConnection: Processing editor {editor.RelativePath ?? "unknown"}");

                    // Clear annotations and reset styles before processing
                    ScintillaManager.ClearAnnotations(editor);
                    ScintillaManager.ResetStyles(editor);

                    // Apply dark mode if enabled
                    if (chkAutoDark.Checked)
                    {
                        ScintillaManager.SetDarkMode(editor);
                    }

                    // Process stylers with the new database connection
                    stylerManager?.ProcessStylersForEditor(editor);

                    Debug.Log($"RefreshAllEditorsAfterDatabaseConnection: Completed processing for {editor.RelativePath ?? "unknown"}");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, $"RefreshAllEditorsAfterDatabaseConnection: Error processing editor {editor?.RelativePath ?? "unknown"}");
                }
            }

            Debug.Log("RefreshAllEditorsAfterDatabaseConnection: Completed refresh of all editors");
        }

        /// <summary>
        /// Invalidates type metadata caches when an editor is saved.
        /// This ensures cached type information is refreshed when source code changes.
        /// </summary>
        /// <param name="editor">The editor that was saved</param>
        private void InvalidateTypeCacheForEditor(ScintillaEditor editor)
        {
            try
            {
                // Only invalidate for PeopleCode editors
                if (editor.Type != EditorType.PeopleCode)
                {
                    return;
                }

                // Get the AppDesigner process
                var appDesignerProcess = editor.AppDesignerProcess;
                if (appDesignerProcess == null)
                {
                    Debug.Log("InvalidateTypeCacheForEditor: No AppDesigner process available");
                    return;
                }

                var qualifiedName = DetermineQualifiedName(editor);

                // Invalidate in TypeCache (shared cache for TypeMetadata)
                if (appDesignerProcess.TypeResolver != null)
                {
                    bool removed = appDesignerProcess.TypeResolver.Cache.Remove(qualifiedName);
                    if (removed)
                    {
                        Debug.Log($"InvalidateTypeCacheForEditor: Removed '{qualifiedName}' from TypeCache");
                    }
                }

                // Invalidate in DatabaseTypeMetadataResolver (resolver's internal cache)
                if (appDesignerProcess.TypeResolver != null)
                {
                    if (appDesignerProcess.TypeResolver is DatabaseTypeMetadataResolver dbResolver)
                    {
                        dbResolver.Clear();
                    }
                    Debug.Log($"InvalidateTypeCacheForEditor: Invalidated DatabaseTypeMetadataResolver cache for '{qualifiedName}'");
                }
                else
                {
                    Debug.Log("InvalidateTypeCacheForEditor: TypeResolver is null (database not connected?)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "InvalidateTypeCacheForEditor: Error invalidating type cache");
            }
        }

        // Renamed from WinEventProc and updated signature for EventHandler
        // Now debounced to prevent multiple rapid focus events
        private void HandleWindowFocusEvent(object? sender, IntPtr hwnd)
        {
            lock (focusEventLock)
            {
                // Cancel any pending focus timer
                focusDebounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);

                // Store the hwnd for later processing
                pendingFocusHwnd = hwnd;

                // Start a new timer to process this focus event after the debounce period
                focusDebounceTimer = new System.Threading.Timer(
                    ProcessFocusEvent, null, FOCUS_DEBOUNCE_MS, Timeout.Infinite);
            }
        }

        private void ProcessFocusEvent(object? _)
        {
            IntPtr hwndToProcess = IntPtr.Zero;

            lock (focusEventLock)
            {
                // If there's no pending focus hwnd, just return
                if (pendingFocusHwnd == IntPtr.Zero)
                    return;

                // Get the hwnd to process
                hwndToProcess = pendingFocusHwnd;

                // Clear the pending hwnd
                pendingFocusHwnd = IntPtr.Zero;
            }

            try
            {
                // Make sure we're on the UI thread
                this.Invoke(() =>
                {
                    ProcessWindowFocus(hwndToProcess);
                });
            }
            catch (Exception ex)
            {
                Debug.Log($"Error processing debounced focus event: {ex.Message}");
                Debug.Log(ex.StackTrace);
            }
        }

        private void ProcessWindowFocus(IntPtr hwnd)
        {
            // Check if the focused window is a Scintilla window

            WinApi.GetWindowThreadProcessId(hwnd, out var processId); // Use WinApi
            /* Handle focusing on App Designer but not an editor */
            StringBuilder windowText = new StringBuilder(256);
            WinApi.GetWindowText(hwnd, windowText, windowText.Capacity);

            if (windowText.ToString().StartsWith("Application Designer"))
            {
                if (!AppDesignerProcesses.ContainsKey(processId))
                {
                    ValidateAndCreateAppDesignerProcess(processId, hwnd);
                }
                return;
            }

            StringBuilder className = new(256);
            WinApi.GetClassName(hwnd, className, className.Capacity); // Use WinApi
            if (className.ToString().Contains("Scintilla"))
            {
                // Ensure hwnd is owned by "pside.exe"

                try
                {
                    if ("pside".Equals(Process.GetProcessById((int)processId).ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"WinEvent detected Scintilla window focus: 0x{hwnd.ToInt64():X}");

                        // The event handler is already invoked on the correct synchronization context
                        // by WinEventService, so no need for BeginInvoke here.

                        // Handle potential focus loss on the previous editor first
                        if (activeEditor != null && hwnd != activeEditor.hWnd && !activeEditor.IsValid())
                        {
                            Debug.Log("Previous active editor lost focus or became invalid.");
                            activeEditor = null;
                            Stylers.InvalidAppClass.ClearValidAppClassPathsCache(); // Example cleanup
                        }

                        // Use SetActiveEditor to properly handle the newly focused window
                        SetActiveEditor(hwnd);


                        /* If editor doesn't have a CaptionChanged handler, set one */
                        if (activeEditor != null && !activeEditor.HasCaptionEventHander)
                        {
                            activeEditor.CaptionChanged += (s, e) =>
                            {
                                Debug.Log($"CaptionChanged event: Editor reused for different program. " +
                                         $"Old: '{e.OldCaption}', New: '{e.NewCaption}'");

                                var lastKnownKey = $"{activeEditor.AppDesignerProcess.ProcessId}:{activeEditor.Caption}";
                                if (lastKnownPositions.TryGetValue(lastKnownKey, out var position))
                                {
                                    ScintillaManager.SetFirstVisibleLine(activeEditor, position.FirstLine);
                                    ScintillaManager.SetCursorPosition(activeEditor, position.CursorPosition);

                                }

                                activeEditor.ContentString = ScintillaManager.GetScintillaText(activeEditor);
                                activeEditor.CollapsedFoldPaths.Clear();
                                if (chkRememberFolds.Checked)
                                {
                                    activeEditor.CollapsedFoldPaths = FoldingManager.RetrievePersistedFolds(activeEditor);
                                }

                                if (activeEditor.AppDesignerProcess.PendingSelection is SourceSpan selection)
                                {
                                    activeEditor.AppDesignerProcess.PendingSelection = null;
                                    WindowHelper.FocusWindow(activeEditor.hWnd);
                                    ScintillaManager.SetSelection(activeEditor, selection.Start.ByteIndex, selection.End.ByteIndex);

                                    // Reposition stack trace dialog if visible to avoid covering selection
                                    if (stackTraceNavigatorDialog != null && !stackTraceNavigatorDialog.IsDisposed && stackTraceNavigatorDialog.Visible)
                                    {
                                        stackTraceNavigatorDialog.AvoidSelectionOverlap(activeEditor, selection);
                                    }
                                }

                                // Clear the styler processing debounce for this editor since it's a new program
                                // This ensures stylers will run immediately even if they ran recently for the previous program
                                lastStylerProcessingTime.Remove(activeEditor);
                                Debug.Log($"CaptionChanged event: Cleared styler debounce timer for reused editor");

                                Debug.Log($"CaptionChanged event: Calling CheckForContentChanges to process new program content");
                                CheckForContentChanges(activeEditor);

                            };
                        }

                        if (activeEditor != null && activeEditor.IsValid())
                        {
                            if (!activeEditor.Initialized) // Check if it's truly a *new* editor needing init
                            {
                                ProcessNewEditor(activeEditor);
                                if (activeEditor.AppDesignerProcess.PendingSelection is SourceSpan selection)
                                {
                                    activeEditor.AppDesignerProcess.PendingSelection = null;
                                    WindowHelper.FocusWindow(activeEditor.hWnd);
                                    ScintillaManager.SetSelection(activeEditor, selection.Start.ByteIndex, selection.End.ByteIndex);

                                    // Reposition stack trace dialog if visible to avoid covering selection
                                    if (stackTraceNavigatorDialog != null && !stackTraceNavigatorDialog.IsDisposed && stackTraceNavigatorDialog.Visible)
                                    {
                                        stackTraceNavigatorDialog.AvoidSelectionOverlap(activeEditor, selection);
                                    }
                                }
                            }
                            else
                            {
                                // Editor already known and initialized, check content on focus
                                CheckForContentChanges(activeEditor);
                                Debug.Log("Focus!");
                                if (activeEditor.AppDesignerProcess.PendingSelection is SourceSpan selection)
                                {
                                    activeEditor.AppDesignerProcess.PendingSelection = null;
                                    WindowHelper.FocusWindow(activeEditor.hWnd);
                                    ScintillaManager.SetSelection(activeEditor, selection.Start.ByteIndex, selection.End.ByteIndex);

                                    // Reposition stack trace dialog if visible to avoid covering selection
                                    if (stackTraceNavigatorDialog != null && !stackTraceNavigatorDialog.IsDisposed && stackTraceNavigatorDialog.Visible)
                                    {
                                        stackTraceNavigatorDialog.AvoidSelectionOverlap(activeEditor, selection);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Focused editor is null or invalid
                            if (activeEditor?.hWnd == hwnd) // If the invalid one was our active one
                            {
                                activeEditor = null; // Clear active editor
                            }
                        }
                    }
                }
                catch (ArgumentException) { /* Process might have exited */ }
            }

            // Check for any window owned by pside.exe to update activeAppDesigner
            NativeMethods.GetWindowThreadProcessId(hwnd, out var focusedProcessId);
            try
            {
                var process = Process.GetProcessById((int)processId);
                if ("pside".Equals(process.ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    // Update activeAppDesigner to the process that owns this focused window
                    if (AppDesignerProcesses.TryGetValue(focusedProcessId, out var appDesignerProcess))
                    {
                        if (activeAppDesigner != appDesignerProcess)
                        {
                            activeAppDesigner = appDesignerProcess;
                            Debug.Log($"Active AppDesigner changed to process ID: {focusedProcessId}");
                        }
                    }
                    else
                    {
                        // Process not yet tracked, this shouldn't happen often as creation events should handle this
                        Debug.Log($"Focus detected on untracked pside.exe process ID: {focusedProcessId}");

                        // Check if it's a pside.exe process
                        ValidateAndCreateAppDesignerProcess(focusedProcessId, process.MainWindowHandle);
                    }
                }
            }
            catch (ArgumentException) { /* Process might have exited */ }
        }

        private void HandleMainWindowShown(IntPtr hwnd)
        {
            try
            {
                // Get the process ID for the created window
                NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);

                // Check if it's a pside.exe process
                var process = Process.GetProcessById((int)processId);
                if (!"pside".Equals(process.ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (process.MainWindowHandle == hwnd)
                {

                    Debug.Log($"WinEvent detected window creation in pside.exe process: PID {processId}, HWND 0x{hwnd.ToInt64():X}");

                    // Early exit if we've already tracked this process ID for AppDesigner process tracking
                    if (trackedProcessIds.Contains(processId))
                    {
                        return;
                    }

                    // Double-check if we already have this process tracked (defensive programming)
                    if (AppDesignerProcesses.ContainsKey(processId))
                    {
                        Debug.Log($"Process {processId} already tracked, adding to tracking set and skipping validation");
                        trackedProcessIds.Add(processId);
                        return;
                    }

                    // Try immediate validation for AppDesigner process tracking
                    if (ValidateAndCreateAppDesignerProcess(processId, hwnd))
                    {
                        Debug.Log($"Process {processId} immediately validated as Application Designer");
                    }
                    else
                    {
                        // Queue for retry validation
                        Debug.Log($"Process {processId} failed immediate validation!");
                    }
                }
            }
            catch (ArgumentException)
            {
                // Process might have exited or be invalid, ignore
            }
            catch (Exception ex)
            {
                Debug.Log($"Error in HandleWindowCreationEvent: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles window shown events to detect and center modal dialogs.
        /// </summary>
        private void HandleWindowShownEvent(object? sender, IntPtr hwnd)
        {
            /* Testing out "Window shown" for pside detect */

            if (settingsService == null) return;

            // Check if auto-centering is enabled
            var generalSettings = settingsService.LoadGeneralSettings();
            if (!generalSettings.AutoCenterDialogs)
            {
                return;
            }
            try
            {
                // Check if it's a standard dialog window class
                var className = new System.Text.StringBuilder(256);
                if (NativeMethods.GetClassName(hwnd, className, className.Capacity) == 0)
                {
                    return;
                }

                string windowClass = className.ToString();
                if (windowClass != "#32770")
                {
                    HandleMainWindowShown(hwnd);
                    return; // Not a standard dialog
                }

                // Get the process ID for the shown window
                NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);

                // Check if it's a pside.exe process
                var process = Process.GetProcessById((int)processId);
                if (!"pside".Equals(process.ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Debug.Log($"WinEvent detected dialog window shown in pside.exe process: PID {processId}, HWND 0x{hwnd.ToInt64():X} (class: {windowClass})");

                // Try to center the dialog
                dialogCenteringService?.TryCenterDialog(hwnd, processId);
            }
            catch (ArgumentException)
            {
                // Process might have exited or be invalid, ignore
            }
            catch (Exception ex)
            {
                Debug.Log($"Error in HandleWindowShownEvent: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates if a pside.exe process is an Application Designer and creates the AppDesignerProcess if successful.
        /// This method includes retry logic for when the Results tab is not immediately available.
        /// </summary>
        /// <param name="processId">The pside.exe process ID</param>
        /// <param name="triggerWindowHandle">The window handle that triggered the validation</param>
        /// <returns>True if validation succeeded and AppDesignerProcess was created</returns>
        private bool ValidateAndCreateAppDesignerProcess(uint processId, IntPtr triggerWindowHandle)
        {
            // Clean up any existing retry for this process first
            CleanupValidationRetry(processId);

            // Try immediate validation
            bool success = ValidateAndCreateAppDesignerProcessInternal(processId, triggerWindowHandle, 0);

            if (!success)
            {
                // If immediate validation failed, it might be due to Results tab not being ready yet
                // Check if we should schedule a retry (only if the process exists and appears to be App Designer)
                try
                {
                    var process = Process.GetProcessById((int)processId);
                    if (process?.MainWindowHandle != IntPtr.Zero)
                    {
                        var caption = WindowHelper.GetWindowText(process.MainWindowHandle);
                        if (caption.StartsWith("Application Designer", StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"Initial validation failed for process {processId}, scheduling retry");
                            ScheduleValidationRetry(processId, triggerWindowHandle, 1);
                            // Return true to indicate we're handling this process (even though async)
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log($"Exception checking process for retry eligibility {processId}: {ex.Message}");
                }
            }

            return success;
        }

        /// <summary>
        /// Internal method that performs the actual Application Designer validation without retry logic.
        /// </summary>
        /// <param name="processId">The pside.exe process ID</param>
        /// <param name="triggerWindowHandle">The window handle that triggered the validation</param>
        /// <param name="attemptNumber">The attempt number (0 for initial, 1+ for retries)</param>
        /// <returns>True if validation succeeded and AppDesignerProcess was created</returns>
        private bool ValidateAndCreateAppDesignerProcessInternal(uint processId, IntPtr triggerWindowHandle, int attemptNumber)
        {
            try
            {
                var process = Process.GetProcessById((int)processId);
                if (process?.MainWindowHandle == IntPtr.Zero)
                {
                    return false;
                }

                // Check if main window caption starts with "Application Designer"
                var caption = WindowHelper.GetWindowText(process.MainWindowHandle);
                if (!caption.StartsWith("Application Designer", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Try to find the Results ListView - this is the key validation
                var resultsListView = ResultsListHelper.FindResultsListView(processId);
                if (resultsListView == IntPtr.Zero)
                {
                    Debug.Log($"Results tab not found for process {processId} (attempt {attemptNumber})");
                    return false;
                }

                // Check if we already have this process tracked (could happen with retries)
                if (AppDesignerProcesses.ContainsKey(processId))
                {
                    Debug.Log($"Process {processId} already tracked, skipping duplicate validation");
                    return true;
                }

                // All validations passed - create and track the AppDesignerProcess
                var newProcess = new AppDesignerProcess(processId, resultsListView, GetGeneralSettingsObject(), currentShortcutFlags);
                if (Settings.Default.useEnhancedEditor)
                {
                    string testPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!, "scintilla_mods");
                    newProcess.LoadScintillaDll(testPath);
                }

                AppDesignerProcesses.Add(processId, newProcess);
                trackedProcessIds.Add(processId);
                activeAppDesigner = newProcess;

                // Apply current theme to newly created process
                ApplyCurrentThemeToProcess(newProcess);

                // Add delayed "AppRefiner Connected!" message to Results ListView
                _ = ResultsListHelper.AddDelayedMessageToResultsList(newProcess, resultsListView, "AppRefiner Connected!", 2000);

                Debug.Log($"Successfully created AppDesignerProcess for process {processId} with Results ListView (attempt {attemptNumber})");

                /* Set the DB name */

                Task.Delay(1000).ContinueWith(_ =>
                {
                    // Split the title by " - " and get the second part (DB name)
                    var caption = WindowHelper.GetWindowText(process.MainWindowHandle);
                    string[] parts = caption.Split(new[] { " - " }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        newProcess.DBName = parts[1].Trim();
                    }
                    else
                    {
                        newProcess.DBName = "";
                    }
                    if (newProcess.DBName != "" && chkPromptForDB.Checked)
                    {
                        this.Invoke(() => ConnectToDB());
                    }
                });
                return true;
            }

            catch (Exception ex)
            {
                Debug.Log($"Exception in ValidateAndCreateAppDesignerProcessInternal for process {processId} (attempt {attemptNumber}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Schedules a retry attempt for Application Designer validation after a delay.
        /// </summary>
        /// <param name="processId">The process ID to retry validation for</param>
        /// <param name="hwnd">The window handle associated with the process</param>
        /// <param name="attemptNumber">Current attempt number (1-based)</param>
        private void ScheduleValidationRetry(uint processId, IntPtr hwnd, int attemptNumber)
        {
            lock (validationRetryLock)
            {
                // Clean up any existing timer for this process
                CleanupValidationRetryLocked(processId);

                // Store the attempt count and window handle
                validationRetryAttempts[processId] = attemptNumber;
                validationRetryHandles[processId] = hwnd;

                // Calculate delay with exponential backoff, capped at max delay
                int delay = Math.Min(VALIDATION_RETRY_BASE_DELAY_MS * (1 << (attemptNumber - 1)), VALIDATION_RETRY_MAX_DELAY_MS);

                Debug.Log($"Scheduling validation retry #{attemptNumber} for process {processId} in {delay}ms");

                // Create and start the retry timer
                var timer = new System.Threading.Timer(RetryValidation, processId, delay, Timeout.Infinite);
                validationRetryTimers[processId] = timer;
            }
        }

        /// <summary>
        /// Timer callback that retries Application Designer validation.
        /// </summary>
        /// <param name="state">The process ID (as uint) to retry validation for</param>
        private void RetryValidation(object? state)
        {
            if (state is not uint processId)
                return;

            IntPtr hwnd;
            int attemptNumber;

            // Get the retry context under lock
            lock (validationRetryLock)
            {
                if (!validationRetryAttempts.TryGetValue(processId, out attemptNumber) ||
                    !validationRetryHandles.TryGetValue(processId, out hwnd))
                {
                    Debug.Log($"Retry validation called for process {processId} but no retry context found");
                    return;
                }
            }

            Debug.Log($"Retrying validation attempt #{attemptNumber} for process {processId}");

            try
            {
                // Try validation again
                if (ValidateAndCreateAppDesignerProcessInternal(processId, hwnd, attemptNumber))
                {
                    Debug.Log($"Validation retry #{attemptNumber} succeeded for process {processId}");
                    CleanupValidationRetry(processId);
                }
                else
                {
                    // Check if we should retry again
                    if (attemptNumber < VALIDATION_RETRY_MAX_ATTEMPTS)
                    {
                        ScheduleValidationRetry(processId, hwnd, attemptNumber + 1);
                    }
                    else
                    {
                        Debug.Log($"Validation failed after {VALIDATION_RETRY_MAX_ATTEMPTS} attempts for process {processId}");
                        CleanupValidationRetry(processId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Exception during validation retry for process {processId}: {ex.Message}");
                CleanupValidationRetry(processId);
            }
        }

        /// <summary>
        /// Cleans up retry tracking for a specific process ID.
        /// </summary>
        /// <param name="processId">The process ID to clean up</param>
        private void CleanupValidationRetry(uint processId)
        {
            lock (validationRetryLock)
            {
                CleanupValidationRetryLocked(processId);
            }
        }

        /// <summary>
        /// Internal cleanup method that must be called under validationRetryLock.
        /// </summary>
        /// <param name="processId">The process ID to clean up</param>
        private void CleanupValidationRetryLocked(uint processId)
        {
            if (validationRetryTimers.TryGetValue(processId, out var timer))
            {
                timer.Dispose();
                validationRetryTimers.Remove(processId);
            }
            validationRetryAttempts.Remove(processId);
            validationRetryHandles.Remove(processId);
        }

        // Add this new CellPainting event handler method
        private void gridRefactors_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            // Check if it's the button column (index 2) and a valid row
            if (e.ColumnIndex == 2 && e.RowIndex >= 0)
            {
                // Check the tag of the cell
                var cell = gridRefactors.Rows[e.RowIndex].Cells[e.ColumnIndex];
                if (cell.Tag?.ToString() == "NoConfig")
                {
                    // Paint the background to match the grid's default background
                    using Brush backColorBrush = new SolidBrush(SystemColors.Control);
                    using Pen gridLinePen = new(gridRefactors.GridColor, 1); // Use the grid color for the border
                                                                             // Erase the cell background
                    e.Graphics.FillRectangle(backColorBrush, e.CellBounds);

                    // Draw the grid lines (border) - Adjust coordinates slightly for standard appearance
                    e.Graphics.DrawRectangle(gridLinePen, e.CellBounds.Left - 1, e.CellBounds.Top - 1, e.CellBounds.Width, e.CellBounds.Height);

                    // Prevent default painting (including hover effects)
                    e.Handled = true;
                }
                // Allow default painting for normal button cells or other columns
            }
        }

        // Add this new CellPainting event handler method
        private void dataGridView1_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            // Check if it's the button column (index 2) and a valid row
            if (e.ColumnIndex == 2 && e.RowIndex >= 0)
            {
                // Check the tag of the cell
                var cell = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex];
                if (cell.Tag?.ToString() == "NoConfig")
                {
                    // Paint the background to match the grid's default background
                    using Brush backColorBrush = new SolidBrush(SystemColors.Control);
                    using Pen gridLinePen = new(dataGridView1.GridColor, 1); // Use the grid color for the border
                                                                             // Erase the cell background
                    e.Graphics.FillRectangle(backColorBrush, e.CellBounds);

                    // Draw the grid lines (border) - Adjust coordinates slightly for standard appearance
                    e.Graphics.DrawRectangle(gridLinePen, e.CellBounds.Left - 1, e.CellBounds.Top - 1, e.CellBounds.Width, e.CellBounds.Height);

                    // Prevent default painting (including hover effects)
                    e.Handled = true;
                }
                // Allow default painting for normal button cells or other columns
            }
        }

        /// <summary>
        /// Checks if a quick fix is available for an indicator at the current cursor position.
        /// </summary>
        /// <returns>True if a quick fix is available, false otherwise.</returns>
        private bool IsQuickFixAvailableAtCursor()
        {
            if (activeEditor == null || !activeEditor.IsValid())
            {
                return false;
            }

            int currentPosition = ScintillaManager.GetCursorPosition(activeEditor);

            /* if any active indicator exists that contains a quick fix that is also in range of the current position return true */

            return activeEditor.ActiveIndicators.Where(i => i.Start <= currentPosition && i.Start + i.Length >= currentPosition)
                .Any(i => i.QuickFixes.Count > 0);
        }

        /// <summary>
        /// Applies the first available quick fix found at the current cursor position.
        /// </summary>
        internal void ApplyQuickFixCommand()
        {
            if (activeEditor == null || !activeEditor.IsValid() || refactorManager == null)
            {
                return;
            }

            int position = ScintillaManager.GetCursorPosition(activeEditor);

            autoCompleteService?.ShowQuickFixSuggestions(activeEditor, position);
        }

        private void btnReportDirectory_Click(object sender, EventArgs e)
        {
            linterManager?.SetLintReportDirectory();
            txtLintReportDir.Text = Properties.Settings.Default.LintReportPath;
        }

        private void btnTNSADMIN_Click(object sender, EventArgs e)
        {
            /* Folder selection dialog and save result to TNS_ADMIN */
            using var folderDialog = new FolderBrowserDialog();
            folderDialog.Description = "Select the TNS_ADMIN directory";
            folderDialog.ShowNewFolderButton = false;
            if (string.IsNullOrEmpty(TNS_ADMIN))
            {
                folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else
            {
                folderDialog.SelectedPath = TNS_ADMIN;
            }
            if (folderDialog.ShowDialog(this) == DialogResult.OK)
            {
                TNS_ADMIN = folderDialog.SelectedPath;
                txtTnsAdminDir.Text = TNS_ADMIN;
                SaveSettings(); // Save all settings
                Debug.Log($"TNS_ADMIN property set to: {folderDialog.SelectedPath}");
            }
        }

        private void linkDocs_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            /* Navigate to URL */
            var si = new ProcessStartInfo("https://github.com/Gideon-Taylor/AppRefiner/blob/main/docs/README.md")
            {
                UseShellExecute = true
            };
            Process.Start(si);
            linkDocs.LinkVisited = true;
        }

        /// <summary>
        /// Opens an arbitrary code definition in the IDE by leveraging the Results list view
        /// </summary>
        /// <param name="targetString">The target string to open (e.g., class path, method signature)</param>
        /// <returns>True if operation was successful</returns>
        public bool OpenTarget(string targetString)
        {
            if (string.IsNullOrEmpty(targetString) || targetString.Length >= 256)
            {
                Debug.Log($"OpenTarget: Invalid target string length ({targetString?.Length ?? 0} chars)");
                return false;
            }

            // Use EventHookInstaller to set the open target and trigger double-click
            if (activeAppDesigner != null)
            {
                bool success = activeAppDesigner.SetOpenTarget(targetString);

                if (success)
                {
                    Debug.Log($"OpenTarget: Successfully set target '{targetString}' for thread {activeAppDesigner.MainThreadId}");
                }
                else
                {
                    Debug.Log($"OpenTarget: Failed to set target '{targetString}' for thread {activeAppDesigner.MainThreadId}");
                }
                return success;
            }
            return false;

        }

        private void btnConfigSmartOpen_Click(object sender, EventArgs e)
        {
            try
            {
                // Load current Smart Open configuration
                var currentConfig = settingsService?.LoadSmartOpenConfig() ?? SmartOpenConfig.GetDefault();

                // Create and show the configuration dialog
                using var dialog = new SmartOpenConfigDialog(currentConfig);
                dialog.StartPosition = FormStartPosition.CenterParent;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    // Save the updated configuration
                    settingsService?.SaveSmartOpenConfig(dialog.Configuration);
                    settingsService?.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "Error showing Smart Open configuration dialog");

                // Show error message to user
                Task.Delay(100).ContinueWith(_ =>
                {
                    var mainHandle = this.Handle;
                    var handleWrapper = new WindowWrapper(mainHandle);
                    new MessageBoxDialog($"Error opening Smart Open configuration: {ex.Message}",
                        "Configuration Error", MessageBoxButtons.OK, mainHandle).ShowDialog(handleWrapper);
                });
            }
        }

        private void lnkWhatsNew_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ShowWhatsNewDialog();
        }

        private void lnkNewVersion_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            /* Navigate to URL */
            var si = new ProcessStartInfo("https://github.com/Gideon-Taylor/AppRefiner/releases/latest")
            {
                UseShellExecute = true
            };
            Process.Start(si);
            lnkNewVersion.LinkVisited = true;
        }
    }
}


