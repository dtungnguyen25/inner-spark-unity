using System.Collections.Generic;
using Pcb;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Look part of the PCB Level Editor: this level's default models (Look section) and per-object models (Paint tool).
/// Which model wins: the object's own model, then the level's Look, then the theme.
/// </summary>
public partial class PcbLevelEditorWindow
{
    static readonly NodeType[] NodeTypes = (NodeType[])System.Enum.GetValues(typeof(NodeType));

    bool showLook = true;
    int copyLookFrom;

    // Paint tool
    bool paintDecorations;
    NodeType paintNodeType = NodeType.Capacitor;
    DecorType paintDecorType = DecorType.Resistor;
    GameObject brush; // null = reset to the default
    readonly List<GameObject> paintOptions = new List<GameObject>();
    GUIStyle paletteStyle;

    // ---------------------------------------------------------------- Look section

    void LookGUI()
    {
        EditorGUILayout.Space();
        showLook = EditorGUILayout.Foldout(showLook, "Look (this level)", true, EditorStyles.foldoutHeader);
        if (!showLook || !board.theme) return;
        var theme = board.theme;

        var look = board.look;
        EditorGUI.BeginChangeCheck();
        look.boardTile = LookPopup("Board Tile", look.boardTile, theme.boardTilePrefab, theme.boardTileVariants, "plain slab");
        look.trace = LookPopup("Trace", look.trace, theme.tracePrefab, theme.traceVariants, "simple shapes");
        look.traceBend = LookPopup("Trace Bend", look.traceBend, theme.traceBendPrefab, theme.traceBendVariants, "overlap");
        foreach (var type in NodeTypes)
            look.Set(type, LookPopup(type.ToString(), look.For(type), theme.NodePrefab(type), theme.NodeVariants(type), "simple shape"));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(board, "Change Level Look");
            board.look = look;
            board.Rebuild();
            dirtyCheckDue = true;
        }

        var list = PcbAssetSetup.GetOrCreateLevelList();
        if (list.Count == 0) return;
        var names = new string[list.Count];
        for (int i = 0; i < list.Count; i++) names[i] = $"{i + 1}. {(list.levels[i] ? list.levels[i].levelName : "(missing)")}";
        using (new EditorGUILayout.HorizontalScope())
        {
            copyLookFrom = EditorGUILayout.Popup("Copy Look From", Mathf.Clamp(copyLookFrom, 0, list.Count - 1), names);
            var source = list.levels[copyLookFrom];
            GUI.enabled = source && board.savedPath != AssetDatabase.GetAssetPath(source);
            if (GUILayout.Button("Copy", GUILayout.Width(48)))
            {
                Undo.RecordObject(board, "Copy Level Look");
                board.look = source.look;
                board.Rebuild();
                dirtyCheckDue = true;
            }
            GUI.enabled = true;
        }
    }

    /// <summary>Popup of "Theme default" + the theme's catalog (+ the current pick, if it came from elsewhere).</summary>
    static GameObject LookPopup(string label, GameObject current, GameObject themeDefault, GameObject[] variants, string fallback)
    {
        var options = new List<GameObject> { null };
        AddUnique(options, themeDefault);
        if (variants != null) foreach (var v in variants) AddUnique(options, v);
        AddUnique(options, current);

        var names = new string[options.Count];
        names[0] = $"Theme default ({(themeDefault ? themeDefault.name : fallback)})";
        for (int i = 1; i < options.Count; i++) names[i] = options[i].name;
        int index = EditorGUILayout.Popup(label, Mathf.Max(0, options.IndexOf(current)), names);
        return options[index];
    }

    static void AddUnique(List<GameObject> list, GameObject model)
    {
        if (model && !list.Contains(model)) list.Add(model);
    }

    // ---------------------------------------------------------------- Paint tool

    void PaintOptionsGUI()
    {
        if (!board.theme) return;
        paintDecorations = GUILayout.Toolbar(paintDecorations ? 1 : 0, new[] { "Nodes", "Decorations" }) == 1;
        if (paintDecorations) paintDecorType = (DecorType)EditorGUILayout.EnumPopup("Decor Type", paintDecorType);
        else paintNodeType = (NodeType)EditorGUILayout.EnumPopup("Node Type", paintNodeType);

        GetPaintOptions(paintOptions);
        if (!paintOptions.Contains(brush)) brush = null;

        var contents = new GUIContent[paintOptions.Count];
        for (int i = 0; i < paintOptions.Count; i++)
        {
            var model = paintOptions[i];
            if (!model)
            {
                contents[i] = new GUIContent($"Default\n({DefaultPaintName()})", "Reset to the level's / theme's default");
                continue;
            }
            var preview = AssetPreview.GetAssetPreview(model);
            if (!preview) preview = AssetPreview.GetMiniThumbnail(model);
            contents[i] = new GUIContent(model.name, preview, model.name);
        }
        if (AssetPreview.IsLoadingAssetPreviews()) Repaint(); // thumbnails arrive over a few frames

        paletteStyle ??= new GUIStyle(GUI.skin.button)
        {
            imagePosition = ImagePosition.ImageAbove,
            fontSize = 9,
            wordWrap = true,
            fixedHeight = 80,
            padding = new RectOffset(2, 2, 2, 2)
        };
        int perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 24f) / 80f));
        brush = paintOptions[GUILayout.SelectionGrid(paintOptions.IndexOf(brush), contents, perRow, paletteStyle)];

        string label = paintDecorations ? paintDecorType.ToString() : paintNodeType.ToString();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button($"Paint All {label}s")) PaintAll(brush);
            if (GUILayout.Button($"Reset All {label}s")) PaintAll(null);
        }
        if (paintOptions.Count == 1)
            EditorGUILayout.HelpBox(paintDecorations
                ? "No models for this decoration type yet. Add entries to the theme's Decoration Prefabs (several of the same type = variants)."
                : "No variants for this node type yet. Add models to the theme's Catalog.", MessageType.Info);
    }

    /// <summary>"Default" first, then every model offered for the chosen type.</summary>
    void GetPaintOptions(List<GameObject> result)
    {
        var theme = board.theme;
        if (paintDecorations) theme.GetDecorationVariants(paintDecorType, result);
        else
        {
            result.Clear();
            AddUnique(result, theme.NodePrefab(paintNodeType));
            AddUnique(result, board.look.For(paintNodeType));
            var variants = theme.NodeVariants(paintNodeType);
            if (variants != null) foreach (var v in variants) AddUnique(result, v);
        }
        result.Insert(0, null);
    }

    string DefaultPaintName()
    {
        var model = paintDecorations ? board.theme.GetDecorationPrefab(paintDecorType) : board.DefaultNodeModel(paintNodeType);
        return model ? model.name : paintDecorations ? "placeholder" : "simple shape";
    }

    void PaintTool(Event e, Vector2 mouse)
    {
        Component target = null;
        if (paintDecorations)
        {
            var d = PickDecoration(mouse);
            if (d && d.type == paintDecorType) target = d;
        }
        else
        {
            var n = PickNode(mouse);
            if (n && n.type == paintNodeType) target = n;
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            if (target)
            {
                Undo.RecordObject(target, e.shift ? "Reset Look" : "Paint Look");
                SetModel(target, e.shift ? null : brush);
                board.Rebuild();
                dirtyCheckDue = true;
            }
            e.Use();
        }

        if (e.type != EventType.Repaint) return;
        if (target is PcbNode node) HighlightNode(node, Color.green);
        else if (target is PcbDecoration decor) HighlightDecoration(decor, Color.green);
    }

    /// <summary>Both sides of the board, not just the one being edited.</summary>
    void PaintAll(GameObject model)
    {
        Undo.SetCurrentGroupName(model ? "Paint All" : "Reset All");
        int group = Undo.GetCurrentGroup();
        if (paintDecorations)
        {
            foreach (var d in board.Decorations)
                if (d && d.type == paintDecorType) { Undo.RecordObject(d, "Paint All"); SetModel(d, model); }
        }
        else
        {
            foreach (var n in board.Nodes)
                if (n && n.type == paintNodeType) { Undo.RecordObject(n, "Paint All"); SetModel(n, model); }
        }
        Undo.CollapseUndoOperations(group);
        board.Rebuild();
        dirtyCheckDue = true;
    }

    static void SetModel(Component target, GameObject model)
    {
        if (target is PcbNode n) n.model = model;
        else if (target is PcbDecoration d) d.model = model;
    }
}
