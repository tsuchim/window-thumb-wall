using System.Windows;
using System.Windows.Controls;

namespace WindowThumbWall.Tests;

public sealed class SettingsWindowTests
{
    [Fact]
    public void Constructor_ReflectsInitialNotificationSetting()
    {
        RunInSta(() =>
        {
            SettingsWindow window = new(
                osNotificationAttentionEnabled: true,
                _ => { });

            CheckBox checkBox = FindLogicalDescendant<CheckBox>(window.Content);

            Assert.Equal(LocalizedText.Get("label.settings"), window.Title);
            Assert.Equal(LocalizedText.Get("setting.osNotifications"), checkBox.Content);
            Assert.True(checkBox.IsChecked);
        });
    }

    [Fact]
    public void CheckboxChange_NotifiesCallbackWithUpdatedValue()
    {
        RunInSta(() =>
        {
            List<bool> observed = [];
            SettingsWindow window = new(
                osNotificationAttentionEnabled: false,
                enabled => observed.Add(enabled));

            CheckBox checkBox = FindLogicalDescendant<CheckBox>(window.Content);

            checkBox.IsChecked = true;
            checkBox.IsChecked = false;

            Assert.Equal([true, false], observed);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? captured = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured != null)
            throw new Xunit.Sdk.XunitException($"STA test failed: {captured}");
    }

    private static T FindLogicalDescendant<T>(object? root) where T : DependencyObject
    {
        if (root is T typed)
            return typed;

        if (root is not DependencyObject dependencyObject)
            throw new InvalidOperationException($"Could not find child of type {typeof(T).Name}.");

        foreach (object? child in LogicalTreeHelper.GetChildren(dependencyObject))
        {
            if (child is T directMatch)
                return directMatch;

            try
            {
                return FindLogicalDescendant<T>(child);
            }
            catch (InvalidOperationException)
            {
                // Continue searching siblings.
            }
        }

        throw new InvalidOperationException($"Could not find child of type {typeof(T).Name}.");
    }
}
