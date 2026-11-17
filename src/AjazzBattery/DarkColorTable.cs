using System.Windows.Forms;

namespace AjazzBattery;

internal sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.Card;
    public override Color ImageMarginGradientBegin => Theme.Card;
    public override Color ImageMarginGradientMiddle => Theme.Card;
    public override Color ImageMarginGradientEnd => Theme.Card;
    public override Color MenuBorder => Theme.Stroke;
    public override Color MenuItemBorder => Theme.Stroke;
    public override Color MenuItemSelected => Theme.ControlFillHover;
    public override Color MenuItemSelectedGradientBegin => Theme.ControlFillHover;
    public override Color MenuItemSelectedGradientEnd => Theme.ControlFillHover;
    public override Color MenuItemPressedGradientBegin => Theme.ControlFillPressed;
    public override Color MenuItemPressedGradientMiddle => Theme.ControlFillPressed;
    public override Color MenuItemPressedGradientEnd => Theme.ControlFillPressed;
    public override Color SeparatorDark => Theme.Stroke;
    public override Color SeparatorLight => Theme.Stroke;
    public override Color CheckBackground => Theme.Accent;
    public override Color CheckSelectedBackground => Theme.Accent;
    public override Color CheckPressedBackground => Theme.Accent;
}
