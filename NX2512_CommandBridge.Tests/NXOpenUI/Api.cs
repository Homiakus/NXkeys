using NXOpen.MenuBar;

namespace NXOpen
{
    public sealed class Selection
    {
        public int GetNumSelectedObjects() => 0;
        public TaggedObject GetSelectedTaggedObject(int index) => null;
    }

    public sealed class DialogTester
    {
        public bool InvokeMenuButtonAction(MenuButton button) => true;
    }

    public sealed class UI
    {
        public MenuBarManager MenuBarManager { get; } = new MenuBarManager();
        public Selection SelectionManager { get; } = new Selection();
        public DialogTester DialogTester { get; } = new DialogTester();
        public static UI GetUI() => new UI();
    }
}

namespace NXOpen.MenuBar
{
    public sealed class MenuButtonEvent { }

    public sealed class MenuButton
    {
        public enum AvailabilityStatus
        {
            Available,
            Unavailable
        }

        public enum SensitivityStatus
        {
            Sensitive,
            Insensitive
        }

        public AvailabilityStatus ButtonAvailability { get; set; } = AvailabilityStatus.Available;
        public SensitivityStatus ButtonSensitivity { get; set; } = SensitivityStatus.Sensitive;
    }

    public sealed class MenuBarManager
    {
        public delegate int InitializeMenuApplication();
        public delegate int EnterMenuApplication();
        public delegate int ExitMenuApplication();
        public delegate CallbackStatus ActionCallback(MenuButtonEvent buttonEvent);

        public enum CallbackStatus
        {
            Continue
        }

        public void RegisterApplication(
            string name,
            InitializeMenuApplication initialize,
            EnterMenuApplication enter,
            ExitMenuApplication exit,
            bool parameter1,
            bool parameter2,
            bool parameter3)
        {
        }

        public void AddMenuAction(string actionName, ActionCallback callback) { }
        public MenuButton GetButtonFromName(string commandId) => new MenuButton();
        public void ApplicationSwitchRequest(string applicationId) { }
    }
}
