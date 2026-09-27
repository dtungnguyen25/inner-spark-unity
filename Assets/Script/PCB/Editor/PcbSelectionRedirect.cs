using Pcb;
using UnityEditor;
using UnityEngine;

/// <summary>Clicking a generated 3D part in the Scene view selects the node or trace it belongs to.</summary>
[InitializeOnLoad]
static class PcbSelectionRedirect
{
    static PcbSelectionRedirect() => Selection.selectionChanged += Redirect;

    static void Redirect()
    {
        var go = Selection.activeGameObject;
        if (!go) return;
        var visual = go.GetComponentInParent<PcbVisualOwner>();
        if (visual && visual.owner) Selection.activeObject = visual.owner.gameObject;
    }
}
