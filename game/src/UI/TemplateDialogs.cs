using System;
using System.Linq;
using ContingencyZero.Game.App;
using ContingencyZero.Game.Match;
using Godot;

namespace ContingencyZero.Game.UI;

/// <summary>Small modal dialogs for build templates: name a new one, rename or delete saved ones.</summary>
public static class TemplateDialogs
{
    private static (CanvasLayer Layer, VBoxContainer Body) Modal(int width)
    {
        var layer = new CanvasLayer { Layer = 32 };
        var root = new Control { Theme = UiTheme.Theme };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.5f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(shade);
        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(centre);
        var body = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0) };
        body.AddThemeConstantOverride("separation", 10);
        centre.AddChild(UiTheme.Panel(body, 22));
        return (layer, body);
    }

    /// <summary>Asks for a template name. Enter saves, Escape cancels.</summary>
    public static CanvasLayer SaveDialog(int structureCount, Action<string> onSave)
    {
        var (layer, body) = Modal(420);
        body.AddChild(UiTheme.Title("SAVE AS TEMPLATE", 24));
        body.AddChild(UiTheme.Label($"{structureCount} structure{(structureCount == 1 ? "" : "s")} with their layout.", 14, UiTheme.Dim, HorizontalAlignment.Center));
        var name = new LineEdit { Text = $"Template {TemplateStore.All.Count + 1}", MaxLength = 32, PlaceholderText = "Name", Name = "TemplateName" };
        body.AddChild(name);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 10);
        void Save()
        {
            string n = name.Text.Trim();
            if (n.Length == 0) return;
            layer.QueueFree();
            onSave(n);
        }
        buttons.AddChild(UiTheme.Button("Save", Save, 130));
        buttons.AddChild(UiTheme.Button("Cancel", layer.QueueFree, 130));
        body.AddChild(buttons);
        name.TextSubmitted += _ => Save();
        name.GuiInput += e => { if (e is InputEventKey { Pressed: true, Keycode: Key.Escape }) { layer.QueueFree(); name.AcceptEvent(); } };
        layer.Ready += () => { name.GrabFocus(); name.SelectAll(); };
        return layer;
    }

    /// <summary>Rename or delete saved templates.</summary>
    public static CanvasLayer ManageDialog()
    {
        var (layer, body) = Modal(520);
        body.AddChild(UiTheme.Title("BUILD TEMPLATES", 24));
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 6);
        body.AddChild(list);

        void Rebuild()
        {
            foreach (Node c in list.GetChildren()) c.QueueFree();
            if (TemplateStore.All.Count == 0)
            {
                list.AddChild(UiTheme.Label("No templates yet. Select some structures and press Ctrl+T.", 14, UiTheme.Dim));
                return;
            }
            foreach (BuildTemplate t in TemplateStore.All.ToList())
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                var name = new LineEdit { Text = t.Name, MaxLength = 32, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, TooltipText = "Edit the name, then press Enter" };
                name.TextSubmitted += n => { TemplateStore.Rename(t, n); Rebuild(); };
                row.AddChild(name);
                row.AddChild(UiTheme.Label($"{t.Items.Count} structures", 13, UiTheme.Dim));
                row.AddChild(UiTheme.Button("Delete", () => { TemplateStore.Delete(t); Rebuild(); }, 90, 14));
                list.AddChild(row);
            }
        }
        Rebuild();
        body.AddChild(UiTheme.Button("Close", layer.QueueFree));
        return layer;
    }
}
