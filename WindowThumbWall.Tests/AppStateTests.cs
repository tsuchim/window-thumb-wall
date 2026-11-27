using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace WindowThumbWall.Tests;

public sealed class AppStateTests : IDisposable
{
    private readonly string? _originalOverridePath;
    private readonly string _tempDirectory;
    private readonly string _statePath;

    public AppStateTests()
    {
        _originalOverridePath = Environment.GetEnvironmentVariable(AppState.StatePathOverrideEnvironmentVariable);
        _tempDirectory = Path.Combine(Path.GetTempPath(), "WindowThumbWall.Tests", Guid.NewGuid().ToString("N"));
        _statePath = Path.Combine(_tempDirectory, "state.json");
        Environment.SetEnvironmentVariable(AppState.StatePathOverrideEnvironmentVariable, _statePath);
    }

    [Fact]
    public void GetStatePath_UsesOverrideEnvironmentVariable()
    {
        string resolvedPath = AppState.GetStatePath();

        Assert.Equal(_statePath, resolvedPath);
    }

    [Fact]
    public void LoadWithStatus_PreservesLegacyUnicodeBom()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_statePath, """{"Slots":[{"ProcessName":"editor","Title":"議事録"}]}""",
            System.Text.Encoding.Unicode);

        AppStateLoadResult load = AppState.LoadWithStatus();

        Assert.True(load.CanSave);
        Assert.True(load.Migrated);
        Assert.Equal("議事録", Assert.Single(load.State.Slots).Title);
    }

    [Fact]
    public void LoadWithStatus_DirectoryAtStatePathIsAnInitialFailure()
    {
        Directory.CreateDirectory(_statePath);

        AppStateLoadResult load = AppState.LoadWithStatus();

        Assert.Equal(AppStateLoadStatus.Failed, load.Status);
        Assert.False(load.CanSave);
        Assert.Equal(AppStateSaveStatus.Failed, load.State.SaveWithStatus());
        Assert.True(Directory.Exists(_statePath));
    }

    [Fact]
    public void SaveAndLoad_UseOverriddenPath()
    {
        AppState state = new()
        {
            AutoAddApps = ["chrome", "outlook"],
            IsLeftPanelCollapsed = true,
            LeftPanelWidth = 320,
            AppListHeight = 240,
            EnableOsNotificationAttention = true,
            Slots =
            [
                new SlotState
                {
                    ProcessName = "notepad",
                    Title = "notes.txt",
                    IsPinned = true,
                    Identity = new WindowIdentity
                    {
                        Handle = 123,
                        ProcessId = 456,
                        ProcessStartTimeUtcTicks = 789,
                        ExecutablePath = @"C:\Windows\notepad.exe",
                        WindowClassName = "Notepad",
                        AppUserModelId = "notepad.app"
                    }
                }
            ]
        };

        state.Save();

        Assert.True(File.Exists(_statePath));

        AppState loaded = AppState.Load();

        Assert.Equal(["chrome", "outlook"], loaded.AutoAddApps);
        Assert.True(loaded.IsLeftPanelCollapsed);
        Assert.Equal(320, loaded.LeftPanelWidth);
        Assert.Equal(240, loaded.AppListHeight);
        Assert.True(loaded.EnableOsNotificationAttention);
        Assert.Single(loaded.Slots);
        Assert.Equal("notepad", loaded.Slots[0].ProcessName);
        Assert.Equal("notes.txt", loaded.Slots[0].Title);
        Assert.True(loaded.Slots[0].IsPinned);
        Assert.Equal(123L, loaded.Slots[0].Identity?.Handle);
        Assert.Equal(456U, loaded.Slots[0].Identity?.ProcessId);
        Assert.Equal(789L, loaded.Slots[0].Identity?.ProcessStartTimeUtcTicks);
        Assert.Equal(@"C:\Windows\notepad.exe", loaded.Slots[0].Identity?.ExecutablePath);
        Assert.Equal("Notepad", loaded.Slots[0].Identity?.WindowClassName);
        Assert.Equal("notepad.app", loaded.Slots[0].Identity?.AppUserModelId);
    }

    [Fact]
    public void Save_ReplacesStateWithoutLeavingTemporaryFiles()
    {
        new AppState { AutoAddApps = ["chrome"] }.Save();
        new AppState { AutoAddApps = ["outlook"] }.Save();

        Assert.Equal(["outlook"], AppState.Load().AutoAddApps);
        Assert.Equal([_statePath], Directory.GetFiles(_tempDirectory));
    }

    [Fact]
    public void Save_KeepsRulesWhenWindowGeometryIsUnavailable()
    {
        new AppState
        {
            Geometry = WindowGeometry.FromBounds(double.PositiveInfinity, double.PositiveInfinity,
                double.NegativeInfinity, double.NegativeInfinity, false),
            AutoAddApps = ["chrome"]
        }.Save();

        AppState loaded = AppState.Load();
        Assert.Null(loaded.Geometry);
        Assert.Equal(["chrome"], loaded.AutoAddApps);
    }

    [Fact]
    public void Load_LegacySlotWithoutIdentityRemainsReadable()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_statePath, """{"Slots":[{"ProcessName":"chrome","Title":"old title"}]}""");

        AppState loaded = AppState.Load();

        SlotState slot = Assert.Single(loaded.Slots);
        Assert.Equal("chrome", slot.ProcessName);
        Assert.Equal("old title", slot.Title);
        Assert.Null(slot.Identity);
    }

    [Fact]
    public void Load_IgnoresOldWindowBoundsWithoutLosingIdentity()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_statePath, """
            {"Slots":[{"ProcessName":"chrome","Title":"WhatsApp","Identity":{
                "WindowClassName":"Chrome_WidgetWin_1","Bounds":{"Left":0,"Top":0,"Right":800,"Bottom":600}
            }}]}
            """);

        SlotState slot = Assert.Single(AppState.Load().Slots);
        Assert.Equal("WhatsApp", slot.Title);
        Assert.Equal("Chrome_WidgetWin_1", slot.Identity?.WindowClassName);
    }

    [Fact]
    public void Load_DefaultsNotificationAttentionToDisabled()
    {
        AppState loaded = AppState.Load();

        Assert.False(loaded.EnableOsNotificationAttention);
    }

    [Fact]
    public void RestoreAndSave_PreservesLegacySlotOrderAndAppRulesWithoutCreatingPins()
    {
        WriteState("""
            {
              "AutoAddApps": ["chrome", "outlook"],
              "Slots": [
                { "ProcessName": "chrome", "Title": "Saved first" },
                { "ProcessName": "notepad", "Title": "Saved second" },
                { "ProcessName": "outlook", "Title": "Saved third" }
              ]
            }
            """);

        for (int i = 0; i < 2; i++)
        {
            RestoreAndClose(Visibility.Collapsed);
            Assert.Contains("\"IsPinned\"", File.ReadAllText(_statePath));
            AppState saved = AppState.Load();
            Assert.Equal(["chrome", "outlook"], saved.AutoAddApps);
            Assert.Equal(["Saved first", "Saved second", "Saved third"], saved.Slots.Select(slot => slot.Title));
            Assert.All(saved.Slots, slot => Assert.False(slot.IsPinned));
        }
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"Slots\":[null]}")]
    public void RestoreAndSave_LeavesUnreadableSettingsUntouched(string original)
    {
        WriteState(original);

        RestoreAndClose(Visibility.Visible);

        Assert.Equal(original, File.ReadAllText(_statePath));
    }

    [Fact]
    public void RestoreAndSave_DoesNotOverwriteSettingsAfterAnInitialReadLockIsReleased()
    {
        const string original = """{"AutoAddApps":["chrome"]}""";
        WriteState(original);
        using var stateLock = File.Open(_statePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        RestoreAndClose(Visibility.Visible, stateLock.Dispose);

        Assert.Equal(original, File.ReadAllText(_statePath));
        RestoreAndClose(Visibility.Collapsed);
        Assert.Equal(["chrome"], AppState.Load().AutoAddApps);
    }

    [Fact]
    public void LoadWithStatus_MigratesLegacySlotsAndKeepsOrderedManualAnchors()
    {
        const string original = """
            {"Slots":[
              {"ProcessName":"chrome","Title":"manual one"},
              {"ProcessName":"notepad","Title":"pinned two","IsPinned":true},
              {"ProcessName":"outlook","Title":"manual three"}
            ]}
            """;
        WriteState(original);

        AppStateLoadResult result = AppState.LoadWithStatus();

        Assert.Equal(AppStateLoadStatus.Loaded, result.Status);
        Assert.True(result.CanSave);
        Assert.True(result.Migrated);
        Assert.Equal(["manual one", "pinned two", "manual three"], result.State.Slots.Select(slot => slot.Title));
        Assert.All(result.State.Slots, slot => Assert.NotEqual(Guid.Empty, slot.SlotId));
        Assert.All(result.State.Slots.Where(slot => !slot.IsPinned), slot =>
        {
            Assert.Null(slot.RuleId);
            Assert.False(slot.IsAutomatic);
        });
        SlotState pinned = result.State.Slots[1];
        WindowRuleState rule = Assert.Single(result.State.SpecificWindowRules);
        Assert.Equal(pinned.RuleId, rule.RuleId);
        Assert.Equal("notepad", rule.ProcessName);
        Assert.Equal("pinned two", rule.TitleAnchor);

        Assert.Equal(AppStateSaveStatus.Saved, result.State.SaveWithStatus());
        Assert.Equal(original, File.ReadAllText(_statePath + ".pre-v2.bak"));
        AppStateLoadResult saved = AppState.LoadWithStatus();
        Assert.False(saved.Migrated);
        Assert.Equal(2, saved.State.SchemaVersion);
        Assert.Equal(["manual one", "pinned two", "manual three"], saved.State.Slots.Select(slot => slot.Title));
    }

    [Fact]
    public async Task SaveWithStatus_SerializesSimultaneousWritersToTheSameFile()
    {
        AppState first = LoadMissingState();
        AppState second = LoadMissingState();
        first.LeftPanelWidth = 240;
        second.LeftPanelWidth = 360;
        using var ready = new Barrier(2);

        AppStateSaveStatus SaveTogether(AppState state)
        {
            Assert.True(ready.SignalAndWait(TimeSpan.FromSeconds(8)), "Both writers must reach the write gate.");
            return state.SaveWithStatus();
        }

        AppStateSaveStatus[] results = await Task.WhenAll(
            Task.Run(() => SaveTogether(first)),
            Task.Run(() => SaveTogether(second)));

        Assert.Single(results, status => status == AppStateSaveStatus.Saved);
        Assert.Single(results, status => status == AppStateSaveStatus.SourceChanged);
        double winningWidth = results[0] == AppStateSaveStatus.Saved ? first.LeftPanelWidth : second.LeftPanelWidth;
        Assert.Equal(winningWidth, AppState.Load().LeftPanelWidth);
        Assert.Equal([_statePath], Directory.GetFiles(_tempDirectory));
    }

    [Fact]
    public void SaveWithStatus_DetectsConcurrentSourceChangeAndAllowsReloadRetry()
    {
        AppState first = LoadMissingState();
        Assert.Equal(AppStateSaveStatus.Saved, first.SaveWithStatus());
        AppStateLoadResult left = AppState.LoadWithStatus();
        AppStateLoadResult right = AppState.LoadWithStatus();
        left.State.LeftPanelWidth = 321;
        right.State.LeftPanelWidth = 654;

        Assert.Equal(AppStateSaveStatus.Saved, left.State.SaveWithStatus());
        Assert.Equal(AppStateSaveStatus.SourceChanged, right.State.SaveWithStatus());
        Assert.Equal(321, AppState.LoadWithStatus().State.LeftPanelWidth);

        AppStateLoadResult retry = AppState.LoadWithStatus();
        retry.State.LeftPanelWidth = 654;
        Assert.Equal(AppStateSaveStatus.Saved, retry.State.SaveWithStatus());
        Assert.Equal(654, AppState.LoadWithStatus().State.LeftPanelWidth);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"SchemaVersion\":99,\"Slots\":[],\"AutoAddApps\":[],\"SpecificWindowRules\":[]}")]
    public void LoadWithStatus_FailedInputCannotBeOverwritten(string original)
    {
        WriteState(original);

        AppStateLoadResult result = AppState.LoadWithStatus();
        result.State.AutoAddApps.Add("should-not-write");

        Assert.Equal(AppStateLoadStatus.Failed, result.Status);
        Assert.False(result.CanSave);
        Assert.Equal(AppStateSaveStatus.Failed, result.State.SaveWithStatus());
        Assert.Equal(original, File.ReadAllText(_statePath));
    }

    [Fact]
    public void LoadWithStatus_RejectsVersionTwoBrokenReferencesAndDuplicateIds()
    {
        Guid slotId = Guid.NewGuid();
        Guid missingRuleId = Guid.NewGuid();
        WriteState($"{{\"SchemaVersion\":2,\"Slots\":[{{\"SlotId\":\"{slotId}\",\"RuleId\":\"{missingRuleId}\",\"IsPinned\":true}}],\"SpecificWindowRules\":[],\"AutoAddApps\":[]}}");
        Assert.Equal(AppStateLoadStatus.Failed, AppState.LoadWithStatus().Status);

        WriteState($"{{\"SchemaVersion\":2,\"Slots\":[{{\"SlotId\":\"{slotId}\"}},{{\"SlotId\":\"{slotId}\"}}],\"SpecificWindowRules\":[],\"AutoAddApps\":[]}}");
        Assert.Equal(AppStateLoadStatus.Failed, AppState.LoadWithStatus().Status);
    }

    [Fact]
    public void SaveWithStatus_DoesNotReplaceDifferentExistingMigrationBackup()
    {
        const string original = """{"Slots":[{"ProcessName":"chrome","Title":"manual"}]}""";
        WriteState(original);
        File.WriteAllText(_statePath + ".pre-v2.bak", "owner backup");
        AppStateLoadResult loaded = AppState.LoadWithStatus();

        Assert.Equal(AppStateSaveStatus.Failed, loaded.State.SaveWithStatus());
        Assert.Equal("owner backup", File.ReadAllText(_statePath + ".pre-v2.bak"));
        Assert.Equal(original, File.ReadAllText(_statePath));
    }

    [Fact]
    public void SaveWithStatus_RetriesWhenAnIdenticalMigrationBackupAlreadyExists()
    {
        const string original = """{"Slots":[{"ProcessName":"chrome","Title":"manual"}]}""";
        WriteState(original);
        File.WriteAllText(_statePath + ".pre-v2.bak", original);
        AppStateLoadResult loaded = AppState.LoadWithStatus();

        Assert.Equal(AppStateSaveStatus.Saved, loaded.State.SaveWithStatus());
        Assert.Equal(original, File.ReadAllText(_statePath + ".pre-v2.bak"));
        Assert.Equal(2, AppState.LoadWithStatus().State.SchemaVersion);
    }

    [Fact]
    public void TrySaveRecoveryCopy_UsesCreateNewAndNeverOverwrites()
    {
        AppState state = LoadMissingState();
        state.AutoAddApps.Add("chrome");
        string copyPath = Path.Combine(_tempDirectory, "recovery.json");
        Directory.CreateDirectory(_tempDirectory);

        Assert.True(state.TrySaveRecoveryCopy(copyPath));
        string original = File.ReadAllText(copyPath);
        state.AutoAddApps.Add("outlook");
        Assert.False(state.TrySaveRecoveryCopy(copyPath));
        Assert.Equal(original, File.ReadAllText(copyPath));
    }

    private static AppState LoadMissingState()
    {
        AppStateLoadResult result = AppState.LoadWithStatus();
        Assert.Equal(AppStateLoadStatus.NotFound, result.Status);
        Assert.True(result.CanSave);
        return result.State;
    }

    [Fact]
    public void PendingChoiceFollowsItsSlotAfterAnEarlierSlotDisappears()
    {
        AppState state = LoadMissingState();
        Guid first = Guid.NewGuid();
        Guid chosen = Guid.NewGuid();
        Guid last = Guid.NewGuid();
        state.Slots =
        [
            new SlotState { SlotId = first, ProcessName = "editor", Title = "First" },
            new SlotState { SlotId = chosen, ProcessName = "editor", Title = "Chosen" },
            new SlotState { SlotId = last, ProcessName = "editor", Title = "Last" }
        ];
        Assert.Equal(AppStateSaveStatus.Saved, state.SaveWithStatus());

        RestoreAndClose(Visibility.Collapsed, inspect: window =>
        {
            Invoke(window, "BeginWindowChoice", chosen);
            var choice = typeof(MainWindow).GetField("_windowChoice", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object? originalChoice = choice.GetValue(window);
            Assert.NotNull(originalChoice);

            Invoke(window, "RemoveSlot", 0);

            Assert.Same(originalChoice, choice.GetValue(window));
            Assert.Equal(0, Assert.IsType<int>(Invoke(window, "FindSlotIndex", chosen)));
            Assert.Equal(1, Assert.IsType<int>(Invoke(window, "FindSlotIndex", last)));
            Invoke(window, "RemoveSlot", 0);
            Assert.Null(choice.GetValue(window));
        });
    }

    [Fact]
    public void RemovingWallSlotKeepsSpecificRegistration()
    {
        AppState state = LoadMissingState();
        var rules = new WindowSlotRules();
        var slot = new SlotState { SlotId = Guid.NewGuid(), ProcessName = "editor", Title = "Registered" };
        rules.SetRule(slot);
        state.Slots.Add(slot);
        state.SpecificWindowRules = rules.All.ToList();
        Assert.Equal(AppStateSaveStatus.Saved, state.SaveWithStatus());

        RestoreAndClose(Visibility.Collapsed, inspect: window => Invoke(window, "RemoveSlot", 0));

        var saved = AppState.Load();
        Assert.Empty(saved.Slots);
        Assert.Equal(slot.RuleId, Assert.Single(saved.SpecificWindowRules).RuleId);
    }

    [Fact]
    public void CollapsedSidebarWidensWallAndRestoresPreviousWidthAfterRestart()
    {
        WriteState("""{"LeftPanelWidth":320}""");

        RestoreAndClose(Visibility.Collapsed, inspect: window =>
        {
            var root = (Grid)window.Content;
            var wall = (Grid)window.FindName("ThumbGrid");
            var expand = (Button)window.FindName("ExpandLeftPanelButton");
            var fullscreen = (Button)window.FindName("FullScreenButton");
            Arrange(root);
            double expandedWallWidth = wall.ActualWidth;
            WindowStyle windowStyle = window.WindowStyle;

            ((Button)window.FindName("CollapseLeftPanelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Arrange(root);
            Assert.Equal(40, root.ColumnDefinitions[0].ActualWidth);
            Assert.True(wall.ActualWidth > expandedWallWidth);
            Assert.Equal(windowStyle, window.WindowStyle);

            fullscreen.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(Visibility.Collapsed, expand.Visibility);
            fullscreen.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(Visibility.Visible, expand.Visibility);
        });

        AppState saved = AppState.Load();
        Assert.True(saved.IsLeftPanelCollapsed);
        Assert.Equal(320, saved.LeftPanelWidth);
        RestoreAndClose(Visibility.Collapsed, inspect: window =>
        {
            var sidebar = (Grid)window.FindName("LeftPanel");
            var expand = (Button)window.FindName("ExpandLeftPanelButton");
            Assert.Equal(Visibility.Collapsed, sidebar.Visibility);
            Assert.Equal(Visibility.Visible, expand.Visibility);
            expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(Visibility.Visible, sidebar.Visibility);
            Assert.Equal(Visibility.Collapsed, expand.Visibility);
            Assert.Equal(new GridLength(320), ((Grid)window.Content).ColumnDefinitions[0].Width);
        });
    }

    [Fact]
    public void SaveFailureAfterSidebarCollapse_LeavesWarningAndRetryReachableAndRecovers()
    {
        WriteState("""{"LeftPanelWidth":320}""");

        RestoreAndClose(Visibility.Collapsed, inspect: window =>
        {
            var root = (Grid)window.Content;
            var leftPanel = (Grid)window.FindName("LeftPanel");
            var stateStatusPanel = (StackPanel)window.FindName("StateStatusPanel");
            var notice = (TextBlock)window.FindName("StateNotice");
            var retry = (Button)window.FindName("StateRetryButton");
            ((Button)window.FindName("CollapseLeftPanelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Arrange(root);

            using (var stateLock = File.Open(_statePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Invoke(window, "SaveState");
                Arrange(root);

                Assert.Equal(Visibility.Visible, stateStatusPanel.Visibility);
                Assert.Equal(Visibility.Visible, notice.Visibility);
                Assert.Equal(Visibility.Visible, retry.Visibility);
                Assert.False(IsDescendantOf(stateStatusPanel, leftPanel),
                    "The save warning must be hosted outside the collapsible sidebar.");
                Assert.True(HasVisibleAncestors(notice), "The save warning must remain visible after the sidebar collapses.");
                Assert.True(HasVisibleAncestors(retry), "The save retry button must remain reachable after the sidebar collapses.");
                Assert.True(retry.IsEnabled);
                Assert.True(retry.ActualWidth > 0 && retry.ActualHeight > 0,
                    "The save retry button must have an arranged hit target.");
            }

            typeof(MainWindow).GetField("_stateTrackingEnabled", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(window, true);
            retry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(Visibility.Collapsed, stateStatusPanel.Visibility);
            Assert.True(File.Exists(_statePath));
            Assert.True(AppState.Load().IsLeftPanelCollapsed);
            Assert.Equal(320, AppState.Load().LeftPanelWidth);
        });
    }

    [Fact]
    public void EnterOnSidebarAndRetryButtonsDoesNotConsumeFullscreenShortcut()
    {
        RestoreAndClose(Visibility.Collapsed, inspect: window =>
        {
            IntPtr ownerHandle = new WindowInteropHelper(window).EnsureHandle();
            using var inputSource = new HwndSource(new HwndSourceParameters("WindowThumbWall.Tests.Input")
            {
                ParentWindow = ownerHandle,
                WindowStyle = 0x40000000,
                Width = 1,
                Height = 1
            });
            var collapse = (Button)window.FindName("CollapseLeftPanelButton");
            var expand = (Button)window.FindName("ExpandLeftPanelButton");
            var retry = (Button)window.FindName("StateRetryButton");
            var wall = (Grid)window.FindName("ThumbGrid");
            Invoke(window, "SetStateNotice", "state.saveFailed");

            foreach (Button button in new[] { collapse, expand, retry })
            {
                KeyEventArgs buttonEnter = RaiseEnter(button, inputSource);
                Assert.Same(button, buttonEnter.OriginalSource);
                Assert.False(buttonEnter.Handled, $"Enter on {button.Name} must remain available to the button.");
                Assert.False(GetPrivateField<bool>(window, "_isFullScreen"));
            }

            KeyEventArgs wallEnter = RaiseEnter(wall, inputSource);
            Assert.Same(wall, wallEnter.OriginalSource);
            Assert.True(wallEnter.Handled, "Enter from the wall must keep the existing fullscreen shortcut.");
            Assert.True(GetPrivateField<bool>(window, "_isFullScreen"));

            KeyEventArgs retryEnterInFullscreen = RaiseEnter(retry, inputSource);
            Assert.Same(retry, retryEnterInFullscreen.OriginalSource);
            Assert.False(retryEnterInFullscreen.Handled,
                "Enter on Retry must remain available to the button even while fullscreen.");
            Assert.True(GetPrivateField<bool>(window, "_isFullScreen"));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BeginWindowChoiceFromCollapsedSidebar_ExpandsAndCollapseCancels(bool startFullscreen)
    {
        AppState state = LoadMissingState();
        Guid slotId = Guid.NewGuid();
        state.IsLeftPanelCollapsed = true;
        state.LeftPanelWidth = 320;
        state.Slots = [new SlotState { SlotId = slotId, ProcessName = "editor", Title = "Saved intent" }];
        Assert.Equal(AppStateSaveStatus.Saved, state.SaveWithStatus());

        RestoreAndClose(Visibility.Collapsed, inspect: window =>
        {
            var leftPanel = (Grid)window.FindName("LeftPanel");
            var collapse = (Button)window.FindName("CollapseLeftPanelButton");
            var filterBox = (TextBox)window.FindName("FilterBox");
            Assert.Equal(Visibility.Collapsed, leftPanel.Visibility);
            filterBox.Focusable = false;

            if (startFullscreen)
                Invoke(window, "ToggleFullScreen");
            Assert.Equal(startFullscreen, GetPrivateField<bool>(window, "_isFullScreen"));

            Invoke(window, "BeginWindowChoice", slotId);

            Assert.False(GetPrivateField<bool>(window, "_isFullScreen"));
            Assert.False(GetPrivateField<bool>(window, "_isLeftPanelCollapsed"));
            Assert.Equal(Visibility.Visible, leftPanel.Visibility);
            object? choice = GetPrivateField<object?>(window, "_windowChoice");
            Assert.NotNull(choice);
            Assert.Equal(slotId, choice!.GetType().GetProperty("SlotId")!.GetValue(choice));

            collapse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(GetPrivateField<bool>(window, "_isLeftPanelCollapsed"));
            Assert.Equal(Visibility.Collapsed, leftPanel.Visibility);
            Assert.Null(GetPrivateField<object?>(window, "_windowChoice"));
        });
    }

    private static KeyEventArgs RaiseEnter(UIElement sourceElement, HwndSource presentationSource)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, presentationSource, Environment.TickCount, Key.Enter)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        sourceElement.RaiseEvent(args);
        return args;
    }

    private static T GetPrivateField<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static bool IsDescendantOf(DependencyObject element, DependencyObject ancestor)
    {
        DependencyObject? current = element;
        while (current != null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static bool HasVisibleAncestors(DependencyObject element)
    {
        DependencyObject? current = element;
        while (current != null)
        {
            if (current is UIElement uiElement && uiElement.Visibility != Visibility.Visible)
                return false;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return true;
    }

    private static void Arrange(Grid root)
    {
        root.Measure(new Size(1200, 780));
        root.Arrange(new Rect(0, 0, 1200, 780));
        root.UpdateLayout();
    }

    private void WriteState(string contents)
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_statePath, contents);
    }

    private static void RestoreAndClose(Visibility noticeVisibility, Action? beforeClose = null, Action<MainWindow>? inspect = null)
    {
        Exception? failure = null;
        string stage = "start STA thread";
        var thread = new Thread(() =>
        {
            try
            {
                Volatile.Write(ref stage, "construct main window");
                var window = new MainWindow();
                Volatile.Write(ref stage, "restore panel layout and application rules");
                Invoke(window, "RestorePanelLayout");
                Invoke(window, "RestoreAutoAddApps");
                Volatile.Write(ref stage, "restore slots");
                Invoke(window, "RestoreSlots");
                Volatile.Write(ref stage, "inspect restored window");
                Assert.Equal(noticeVisibility, ((UIElement)window.FindName("StateNotice")).Visibility);
                inspect?.Invoke(window);
                beforeClose?.Invoke();
                Volatile.Write(ref stage, "close and save state");
                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(12)),
            $"The WPF restore and close operation did not finish while attempting to {Volatile.Read(ref stage)}.");
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static object? Invoke(MainWindow window, string name, params object[] arguments) =>
        typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, arguments);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(AppState.StatePathOverrideEnvironmentVariable, _originalOverridePath);
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }
}
