using Avalonia.Controls;
using Avalonia.Input;

namespace GtaHeistPlanner.App.Controls;

public sealed class NoWheelListBox : ListBox
{
    // Custom controls use their own style key by default. Reuse ListBox's theme
    // template so item presenters retain normal pointer, focus, and keyboard input.
    protected override Type StyleKeyOverride => typeof(ListBox);

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        // Segmented selectors change only through explicit click or keyboard input.
        e.Handled = true;
    }
}
