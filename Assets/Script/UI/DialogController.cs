using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pcb
{
    /// <summary>
    /// Modal name/portrait/text box shown once when a stage starts, if its Board has a DialogSequence assigned.
    /// Advances on Continue click, or keyboard/gamepad Submit once Continue is the selected UI element.
    /// </summary>
    public class DialogController : MonoBehaviour
    {
        [Header("Wiring")]
        public GameObject panel;
        public TMP_Text speakerLabel;
        public Image portraitImage;
        public TMP_Text bodyLabel;
        public Button continueButton;

        DialogSequence.Line[] lines;
        int index;
        Action onComplete;

        public bool IsShowing => panel && panel.activeSelf;

        void Awake()
        {
            // Don't force-hide 'panel' here: if this script lives on the panel itself (the natural
            // place to put it) and the panel starts disabled in the scene - as it should - Awake only
            // fires the first time something reactivates it (i.e. the first Show()), and disabling it
            // again from inside that same Awake would immediately undo the activation that triggered it.
            // The panel's saved inactive state already covers "starts hidden".
            if (continueButton) continueButton.onClick.AddListener(Advance);
        }

        /// <summary>Starts showing 'sequence'; calls onDone once every line has been dismissed.</summary>
        public void Show(DialogSequence sequence, Action onDone)
        {
            lines = sequence ? sequence.lines : null;
            onComplete = onDone;
            index = -1;
            if (panel) panel.SetActive(true);
            Advance();
        }

        void Advance()
        {
            index++;
            if (lines == null || index >= lines.Length) { Close(); return; }

            var line = lines[index];
            if (speakerLabel) speakerLabel.text = line.speakerName;
            if (bodyLabel) bodyLabel.text = line.text;
            if (portraitImage)
            {
                portraitImage.sprite = line.portrait;
                portraitImage.enabled = line.portrait;
            }
            if (continueButton && EventSystem.current) EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        }

        void Close()
        {
            if (panel) panel.SetActive(false);
            var callback = onComplete;
            onComplete = null;
            callback?.Invoke();
        }
    }
}
