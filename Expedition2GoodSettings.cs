using System.Collections.Generic;
using System.Drawing;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace Expedition2Good;

public class Expedition2GoodSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    public ColorNode TextColor { get; set; } = new ColorNode(Color.LightBlue);
    public ColorNode TopPickColor { get; set; } = new ColorNode(Color.LightGreen);
    public RangeNode<float> ValuableColorThreshold { get; set; } = new RangeNode<float>(50, 0, 10000);
    public ColorNode ValuableTextColor { get; set; } = new ColorNode(Color.Pink);

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