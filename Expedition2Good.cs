using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.FilesInMemory;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.PoEMemory.Models;
using ExileCore2.Shared.Cache;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;
using RectangleF = ExileCore2.Shared.RectangleF;
using Vector2 = System.Numerics.Vector2;

namespace Expedition2Good;

public class Expedition2Good : BaseSettingsPlugin<Expedition2GoodSettings>
{
    private readonly TimeCache<List<(LabelOnGround, Expedition2EncounterLabel)>> _labels;
    private readonly TimeCache<Dictionary<Expedition2Recipe, (double, bool)>> _price;
    private static readonly (double, bool) NoPrice = (0, false);

    public Expedition2Good()
    {
        _labels = new TimeCache<List<(LabelOnGround, Expedition2EncounterLabel)>>(() =>
                GameController.EntityListWrapper.Entities
                    .Any(x => x.Metadata.StartsWith("Metadata/MiscellaneousObjects/Expedition2/Expedition2Encounter", StringComparison.Ordinal))
                    ? GameController.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Where(x =>
                            x?.ItemOnGround?.Metadata?.StartsWith("Metadata/MiscellaneousObjects/Expedition2/Expedition2Encounter", StringComparison.Ordinal) == true)
                        .Select(x => (x, x.Label.AsObject<Expedition2EncounterLabel>())).ToList()
                    : []
            , 1000);
        _price = new TimeCache<Dictionary<Expedition2Recipe, (double, bool)>>(() =>
        {
            var getCurrencyValue = GameController.PluginBridge.GetMethod<Func<BaseItemType, double>>("NinjaPrice.GetBaseItemTypeValue") ?? (_ => 0);
            return GameController.Files.Expedition2Recipes.EntriesList.ToDictionary(x => x, x =>
            {
                if (Settings.PriceOverrides.Content.FirstOrDefault(priceOverride => priceOverride.Type.Value == x.Id) is { } over)
                {
                    return (over.Value, true);
                }

                return ((x.Reward == null ? 0 : getCurrencyValue(x.Reward)) * x.RewardCount, false);
            });
        }, 1000);
    }

    public override bool Initialise()
    {
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
    }

    public override void DrawSettings()
    {
        var knownRecipes = Settings.KnownRecipes.OrderBy(x => x).ToList();
        foreach (var priceOverride in Settings.PriceOverrides.Content)
        {
            priceOverride.Type.SetListValues(knownRecipes);
        }

        base.DrawSettings();
    }

    public override void Tick()
    {
        Settings.KnownRecipes.UnionWith(_price.Value.Keys.Select(x => x.Id));
    }

    public override void Render()
    {
        var entities = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.IngameIcon]
            .Where(x => x.Metadata.StartsWith("Metadata/MiscellaneousObjects/Expedition2/Expedition2Encounter", StringComparison.Ordinal)).ToList();

        var areaLevel = GameController.IngameState.Data.CurrentAreaLevel;
        var showOnMinimap = Settings.ShowOnMinimap && GameController.IngameState.IngameUi.Map.LargeMap.IsVisible;
        if (_labels.Value is { Count: > 0 } labels)
        {
            var allRecipes = GameController.Files.Expedition2Recipes.EntriesList.ToLookup(x => x.RuneCountRequired);
            if (allRecipes.Count > 0)
            {
                var expedition2RunesWeights = GameController.Files.Expedition2RunesWeights.EntriesList;
                foreach (var (log, label) in labels)
                {
                    var entity = log.ItemOnGround;
                    if (IsActivated(entity)) continue;

                    entities.Remove(entity);
                    var recipes = GetRecipes(expedition2RunesWeights, areaLevel, allRecipes, label?.Data);
                    var allValidRecipes = recipes.Select(x => x.x).ToList();
                    if (Settings.MinimumValueToShow > 0)
                    {
                        recipes = recipes.Where(x => x.value.Item1 >= Settings.MinimumValueToShow).ToList();
                    }

                    if (Settings.MaxItemsToShow > 0)
                    {
                        recipes = recipes.Take(Settings.MaxItemsToShow).ToList();
                    }

                    var bottomLeft = label.GetClientRect().BottomLeft;
                    bottomLeft += new Vector2(Settings.RenderOffsetX, Settings.RenderOffsetY);
                    var y = bottomLeft.Y;

                    var first = true;
                    foreach (var (recipe, (value, overridden)) in recipes)
                    {
                        if (first && showOnMinimap)
                        {
                            var color = value >= Settings.ValuableColorThreshold ? Settings.ValuableTextColor : Settings.TextColor;
                            var lines = GetMapText(overridden, value, allValidRecipes, label.Data, color);
                            DrawMapText(lines, Graphics.GridToMap(entity.GridPos, entity.GridPos));
                        }

                        var textColor = first ? Settings.TopPickColor : value >= Settings.ValuableColorThreshold ? Settings.ValuableTextColor : Settings.TextColor;
                        var size = Graphics.DrawTextWithBackground(
                            $"{(overridden ? "~" : "")}{value,7:F2} {(string.IsNullOrWhiteSpace(recipe.Description) ? recipe.Reward?.BaseName : recipe.Description)} x{recipe.RewardCount}",
                            bottomLeft with { Y = y },
                            textColor, Color.Black);
                        y += size.Y;
                        first = false;
                    }

                    //GameController.InspectObject(recipes, "Recipes");
                }
            }

            //GameController.InspectObject(labels, "Labels");
        }

        if (showOnMinimap)
        {
            foreach (var entity in entities)
            {
                if (IsActivated(entity)) continue;

                var found = false;
                if (entity?.HashComponents.GetValueOrDefault((ushort)0x87B2) is not 0 and { } dataAddr)
                {
                    var data = RemoteMemoryObject.GetObjectStatic<Expedition2EncounterData>(dataAddr);
                    var expedition2RunesWeights = GameController.Files.Expedition2RunesWeights.EntriesList;
                    var allRecipes = GameController.Files.Expedition2Recipes.EntriesList.ToLookup(x => x.RuneCountRequired);
                    var allValidRecipes = GetRecipes(expedition2RunesWeights, areaLevel, allRecipes, data);
                    var recipes = allValidRecipes.FirstOrDefault();
                    if (recipes != default)
                    {
                        var value = recipes.value.Item1;
                        var color = value >= Settings.ValuableColorThreshold ? Settings.ValuableTextColor : Settings.TextColor;
                        var lines = GetMapText(recipes.value.Item2, recipes.value.Item1, allValidRecipes.Select(x => x.x).ToList(), data, color);
                        DrawMapText(lines, Graphics.GridToMap(entity.GridPos, entity.GridPos));
                        found = true;
                    }
                }

                if (!found)
                {
                    var states = entity?.GetComponent<StateMachine>()?.States;
                    var runeCount = states?.FirstOrDefault(x => x.Name == "sockets")?.Value;
                    if (runeCount != null)
                    {
                        Graphics.DrawTextWithBackground($"Unknown rune {runeCount} sockets", Graphics.GridToMap(entity.GridPos, entity.GridPos), Color.Black);
                    }
                }
            }
        }

        if (GameController.IngameState.IngameUi.Expedition2Window is { IsVisible: true } expedition2Window)
        {
            var windowRect = expedition2Window.GetClientRectCache;
            if (!IsDrawableRect(windowRect))
            {
                return;
            }

            var options = expedition2Window.Options
                .Where(x => x is { IsValid: true, IsVisible: true, IsVisibleLocal: true, Recipe: not null })
                .Select(x => (x, GetPriceOrDefault(x.Recipe)))
                .OrderByDescending(x => x.Item2.Item1)
                .ToList();
            var first = true;
            foreach (var (option, (value, overridden)) in options)
            {
                var optionRect = option.GetClientRectCache;
                var bounds = windowRect;
                if (!IsDrawableRect(optionRect) ||
                    !bounds.Intersects(optionRect) ||
                    !bounds.Contains(optionRect.TopLeft))
                {
                    continue;
                }

                var text = $"{(overridden ? "~" : "")}{value,5:F2}";
                var textSize = Graphics.MeasureText(text);
                var position = ClampTextPosition(optionRect.TopRight, textSize, bounds);
                var textColor = first ? Settings.TopPickColor : value >= Settings.ValuableColorThreshold ? Settings.ValuableTextColor : Settings.TextColor;
                Graphics.DrawTextWithBackground(text, position, textColor, Color.Black);
                Graphics.DrawLine(optionRect.TopRight.Translate(-3, 0), optionRect.BottomRight.Translate(-3, 0), 5, textColor);
                first = false;
            }
        }
    }

    private bool IsActivated(Entity entity)
    {
        var states = entity?.GetComponent<StateMachine>()?.States;
        return entity == null ||
               Settings.DisplayOnlyNonActivated &&
               states != null &&
               states.Any(s => s.Name == "activated" && (int)s.Value is 6 or 7 or 8);
    }

    private List<List<(string text, Color color)>> GetMapText(bool overridden, double value, IReadOnlyCollection<Expedition2Recipe> recipes, Expedition2EncounterData data, Color baseColor)
    {
        var lines = new List<List<(string text, Color color)>>
        {
            new() { ($"Rune {(overridden ? "~" : "")}{value:F1} ({data.RuneCount} sockets)", baseColor) },
        };
        if (Settings.ShowTransferredRuneSlots && data.PassedOnRunePositions is { Count: > 0 } positions)
        {
            if (Settings.ShowTransferredRuneOptions)
            {
                foreach (var position in positions)
                {
                    var runeIds = recipes.Select(x => x.Runes.ElementAtOrDefault(position)).Where(x => x != null)
                        .Select(r => r.Id).Distinct().OrderBy(x => x).ToList();
                    var segments = new List<(string text, Color color)> { ($"Transfers rune {position}: ", baseColor) };
                    for (var i = 0; i < runeIds.Count; i++)
                    {
                        segments.Add((runeIds[i], GetHighlightColor(runeIds[i]) ?? baseColor));
                        if (i < runeIds.Count - 1)
                        {
                            segments.Add((",", baseColor));
                        }
                    }

                    lines.Add(segments);
                }
            }
            else
            {
                lines.Add([($"Transfers rune {string.Join(",", positions)}", baseColor)]);
            }
        }

        return lines;
    }

    private Color? GetHighlightColor(string runeId)
    {
        if (string.IsNullOrEmpty(runeId))
        {
            return null;
        }

        foreach (var rune in Settings.HighlightedTransferredRunes.Content)
        {
            if (string.Equals(rune.Name.Value?.Trim(), runeId, StringComparison.OrdinalIgnoreCase))
            {
                return rune.HighlightColor.Value;
            }
        }

        return null;
    }

    private void DrawMapText(List<List<(string text, Color color)>> lines, Vector2 anchor)
    {
        var y = anchor.Y;
        foreach (var segments in lines)
        {
            var x = anchor.X;
            var lineHeight = 0f;
            foreach (var (text, color) in segments)
            {
                Graphics.DrawTextWithBackground(text, new Vector2(x, y), color, Color.Black);
                var size = Graphics.MeasureText(text);
                x += size.X;
                if (size.Y > lineHeight)
                {
                    lineHeight = size.Y;
                }
            }

            y += lineHeight;
        }
    }

    private List<(Expedition2Recipe x, (double, bool) value)> GetRecipes(List<Expedition2RunesWeight> expedition2RunesWeights, int areaLevel, ILookup<int, Expedition2Recipe> allRecipes, Expedition2EncounterData data)
    {
        var allowedRuneCounts = expedition2RunesWeights.Where(x => x.RuneSlot - 1 == data?.FixedRunePosition)
            .Where(x => x.Rune?.Equals(data?.FixedRune) == true)
            .Where(x => x.Level <= areaLevel)
            .Select(x => x.SlotCount)
            .ToHashSet();
        var recipes = allRecipes.Where(x => x.Key <= data?.RuneCount)
            .SelectMany(x => x)
            .Where(x => allowedRuneCounts.Contains(x.RuneCountRequired))
            .Where(x => x.MinLevelReq <= areaLevel && x.MaxLevelReq >= areaLevel)
            .Where(x => x.Runes.ElementAtOrDefault(data?.FixedRunePosition ?? 0)?.Equals(data?.FixedRune) == true)
            .Select(x => (x, value: GetPriceOrDefault(x)))
            .OrderByDescending(x => x.value.Item1)
            .ToList();
        return recipes;
    }

    private (double, bool) GetPriceOrDefault(Expedition2Recipe recipe)
    {
        return recipe != null && _price.Value.TryGetValue(recipe, out var price) ? price : NoPrice;
    }

    private static bool IsDrawableRect(RectangleF rect)
    {
        return rect.Width > 1 && rect.Height > 1;
    }

    private static Vector2 ClampTextPosition(Vector2 position, Vector2 textSize, RectangleF bounds)
    {
        var maxX = Math.Max(bounds.Left, bounds.Right - textSize.X);
        var maxY = Math.Max(bounds.Top, bounds.Bottom - textSize.Y);
        return new Vector2(Math.Clamp(position.X, bounds.Left, maxX), Math.Clamp(position.Y, bounds.Top, maxY));
    }
}