using System.Drawing;
using System.Windows.Forms;

namespace NX2512_HotkeyStudio.UI
{
    public static class NxKeysTheme
    {
        public static bool HighContrast => SystemInformation.HighContrast;
        public static Color Background => HighContrast ? SystemColors.Window : Color.FromArgb(13, 17, 23);
        public static Color Sidebar => HighContrast ? SystemColors.Control : Color.FromArgb(10, 13, 18);
        public static Color Surface => HighContrast ? SystemColors.Control : Color.FromArgb(22, 27, 34);
        public static Color Raised => HighContrast ? SystemColors.ControlLight : Color.FromArgb(33, 38, 45);
        public static Color Border => HighContrast ? SystemColors.WindowText : Color.FromArgb(48, 54, 61);
        public static Color Text => HighContrast ? SystemColors.WindowText : Color.FromArgb(240, 246, 252);
        public static Color Muted => HighContrast ? SystemColors.GrayText : Color.FromArgb(154, 166, 179);
        public static Color Accent => HighContrast ? SystemColors.Highlight : Color.FromArgb(56, 189, 248);
        public static Color Success => HighContrast ? SystemColors.Highlight : Color.FromArgb(16, 185, 129);
        public static Color Warning => HighContrast ? SystemColors.Highlight : Color.FromArgb(245, 158, 11);
        public static Color Danger => HighContrast ? SystemColors.Highlight : Color.FromArgb(239, 68, 68);

        public const int SidebarWidth = 248;
        public const int HeaderHeight = 88;
        public const int FooterHeight = 38;
        public const int ContentPadding = 20;

        public static void ApplyButton(Button button, bool primary = false)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = HighContrast ? 2 : 1;
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.BackColor = primary ? Accent : Raised;
            button.ForeColor = primary && !HighContrast ? Background : Text;
            button.UseVisualStyleBackColor = false;
            button.AccessibleName = button.Text;
        }

        public static void ApplyInput(Control control)
        {
            control.BackColor = Raised;
            control.ForeColor = Text;
        }
    }
}
