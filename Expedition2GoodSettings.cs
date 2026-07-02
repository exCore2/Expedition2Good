using System.Collections.Generic;
using System.Drawing;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace Expedition2Good;

public class Expedition2GoodSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public ToggleNode ShowOnMinimap { get; set; } = new ToggleNode(true);

    public ColorNode TextColor { get; set; } = new ColorNode(Color.LightBlue);
    public ColorNode TopPickColor { get; set; } = new ColorNode(Color.LightGreen);
    public RangeNode<float> ValuableColorThreshold { get; set; } = new RangeNode<float>(50, 0, 10000);
    public ColorNode ValuableTextColor { get; set; } = new ColorNode(Color.Pink);

    [Menu(null, "When greater than 0, recipes with a value below this are hidden. Set to 0 to list every item and value.")]
    public RangeNode<float> MinimumValueToShow { get; set; } = new RangeNode<float>(0, 0, 500);

    [Menu(null, "Maximum number of recipes listed per label (highest value first). Set to 0 to show all.")]
    public RangeNode<int> MaxItemsToShow { get; set; } = new RangeNode<int>(0, 0, 20);

    [Menu(null, "Hides activated encounters")]
    public ToggleNode DisplayOnlyNonActivated { get; set; } = new ToggleNode(false);

    [Menu(null, "Horizontal offset (pixels) of the text relative to the on-ground object label. 0 keeps the original position.")]
    public RangeNode<int> RenderOffsetX { get; set; } = new RangeNode<int>(0, -2000, 2000);

    [Menu(null, "Vertical offset (pixels) of the text relative to the on-ground object label. 0 keeps the original position.")]
    public RangeNode<int> RenderOffsetY { get; set; } = new RangeNode<int>(0, -2000, 2000);

    public ToggleNode ShowTransferredRuneSlots { get; set; } = new ToggleNode(true);
    public ToggleNode ShowTransferredRuneOptions { get; set; } = new ToggleNode(true);

    [Menu(null, "A transferred-rune name is drawn in its configured color when it matches one of these entries. Type the rune name exactly as it appears in the 'Transfers rune' line (case-insensitive), then pick a color.")]
    public ContentNode<HighlightedRune> HighlightedTransferredRunes { get; set; } = new ContentNode<HighlightedRune>
        { Content = [], EnableControls = true, EnableItemCollapsing = true, ItemFactory = () => new HighlightedRune() };

    public ContentNode<PriceOverride> PriceOverrides { get; set; } = new ContentNode<PriceOverride>
        { Content = [], EnableControls = true, EnableItemCollapsing = true, ItemFactory = () => new PriceOverride(), };
    public HashSet<string> KnownRecipes = [];
}

[Submenu]
public class PriceOverride
{
    public ListNode Type { get; set; } = new ListNode();
    public RangeNode<float> Value { get; set; } = new RangeNode<float>(0, 0, 10000);
    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Type.Value) ? base.ToString() : $"{Type.Value} -> {Value}";
    }
}

[Submenu]
public class HighlightedRune
{
    // Stable identity appended via "###" so the ImGui collapsing header keeps a constant ID while the
    // visible name changes as you type. Without it, every keystroke is treated as a new widget and the
    // text field loses focus after a single character.
    private readonly string _id = System.Guid.NewGuid().ToString("N");
    public TextNode Name { get; set; } = new TextNode("");
    public ColorNode HighlightColor { get; set; } = new ColorNode(Color.Gold);
    public override string ToString()
    {
        return $"{(string.IsNullOrWhiteSpace(Name.Value) ? "(unnamed rune)" : Name.Value)}###{_id}";
    }
}