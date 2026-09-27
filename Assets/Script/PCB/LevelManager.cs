using UnityEngine;
using UnityEngine.InputSystem;

namespace Pcb
{
    /// <summary>
    /// One per scene. Plays the levels from the LevelList: spawns the board and the spark,
    /// detects the Goal, and handles restart / previous / next level plus a simple HUD.
    /// If a Board is already in the scene (the one you are editing), play starts on it.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public LevelList levels;
        [Tooltip("Level to start on when the scene has no Board in it.")]
        public int startLevel;
        [Tooltip("Optional. If empty, a default spark is created.")]
        public Spark sparkPrefab;
        [Tooltip("Show the Prev / Restart / Next buttons (turn off for the final build if you want).")]
        public bool showLevelButtons = true;

        GameObject template; // what Restart re-creates: a level prefab, or the disabled scene board
        int index = -1;
        Board current;
        BoardRig rig;
        Spark spark;
        bool won;
        float wonAt;
        InputAction restartAction, confirmAction, prevAction, nextAction, quitAction;
        GUIStyle hudStyle, bigStyle;

        public Board CurrentBoard => current;

        void Awake()
        {
            restartAction = Button("<Keyboard>/r", "<Gamepad>/select");
            confirmAction = Button("<Keyboard>/space", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            prevAction = Button("<Keyboard>/leftBracket", "<Keyboard>/pageUp", "<Gamepad>/leftShoulder");
            nextAction = Button("<Keyboard>/rightBracket", "<Keyboard>/pageDown", "<Gamepad>/rightShoulder");
            quitAction = Button("<Keyboard>/escape");
        }

        static InputAction Button(params string[] bindings)
        {
            var action = new InputAction(type: InputActionType.Button);
            foreach (var b in bindings) action.AddBinding(b);
            return action;
        }

        void OnEnable() { restartAction.Enable(); confirmAction.Enable(); prevAction.Enable(); nextAction.Enable(); quitAction.Enable(); }
        void OnDisable() { restartAction.Disable(); confirmAction.Disable(); prevAction.Disable(); nextAction.Disable(); quitAction.Disable(); }
        void OnDestroy() { restartAction.Dispose(); confirmAction.Dispose(); prevAction.Dispose(); nextAction.Dispose(); quitAction.Dispose(); }

        void Start()
        {
            var sceneBoard = FindAnyObjectByType<Board>();
            bool levelsAvailable = levels && levels.Count > 0;
            if (sceneBoard && (Application.isEditor || !levelsAvailable))
            {
                // Editor: play the board being edited, keeping an untouched copy for restarts.
                sceneBoard.gameObject.SetActive(false);
                template = sceneBoard.gameObject;
                index = levels ? levels.IndexOf(sceneBoard.levelName) : -1;
                Spawn();
            }
            else if (levelsAvailable)
            {
                if (sceneBoard) sceneBoard.gameObject.SetActive(false); // builds always start from the Level List
                GoTo(startLevel);
            }
            else Debug.LogError("[PCB] No Board in the scene and no levels in the Level List.", this);
        }

        public void GoTo(int levelIndex)
        {
            if (!levels || levels.Count == 0) return;
            index = (levelIndex % levels.Count + levels.Count) % levels.Count;
            template = levels[index] ? levels[index].gameObject : null;
            Spawn();
        }

        public void Next() => GoTo(index + 1);
        public void Previous() => GoTo(index < 0 ? -1 : index - 1);
        public void Restart() => Spawn();

        void Spawn()
        {
            if (!template) { Debug.LogError($"[PCB] Level {index + 1} is missing from the Level List.", this); return; }
            if (rig) Destroy(rig.gameObject); // takes the board and the spark with it
            won = false;

            var go = Instantiate(template);
            go.name = template.name;
            go.SetActive(true); // Board.Awake builds the graph and the 3D look
            current = go.GetComponent<Board>();
            rig = BoardRig.Create(current, Camera.main);

            PcbNode start = null;
            foreach (var n in current.Nodes)
                if (n.type == NodeType.Start) { start = n; break; }
            if (!start)
            {
                Debug.LogError($"[PCB] Level '{current.levelName}' has no Start node.", this);
                return;
            }

            spark = sparkPrefab ? Instantiate(sparkPrefab) : new GameObject("Spark").AddComponent<Spark>();
            spark.Init(current, start, rig);
            spark.Arrived += OnArrived;
        }

        void OnArrived(PcbNode node)
        {
            if (node.type != NodeType.Goal) return;
            won = true;
            wonAt = Time.time;
            spark.enabled = false;
        }

        void Update()
        {
            if (quitAction.WasPressedThisFrame() && !Application.isEditor) Application.Quit(); // no effect in WebGL
            if (restartAction.WasPressedThisFrame()) Restart();
            else if (prevAction.WasPressedThisFrame()) Previous();
            else if (nextAction.WasPressedThisFrame()) Next();
            else if (won && Time.time - wonAt > 0.4f && confirmAction.WasPressedThisFrame()) Next();
        }

        void OnGUI()
        {
            if (!current) return;
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label) { richText = true };
                bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
            }
            int font = Mathf.Max(12, Screen.height / 40);
            hudStyle.fontSize = font;
            bigStyle.fontSize = Mathf.Max(24, Screen.height / 12);

            string number = index >= 0 && levels ? $"{index + 1}/{levels.Count}" : "unsaved";
            string side = current.View == PcbLayer.Front ? "FRONT" : "<color=#7fd4ff>BACK</color>";
            string hud = $"<b>{current.levelName}</b>  ({number})\nSide: <b>{side}</b>\n" +
                         $"<size={font * 3 / 4}>Move: WASD / Arrows   Flip (on via): Space   Inspect: drag mouse   Restart: R   Level: [ ]</size>";
            if (spark && !won && !spark.IsMoving && !spark.IsTurning && spark.CurrentNode && spark.CurrentNode.IsVia)
                hud += "\n<color=#ffe08a>On a via: press Space to flip side</color>";
            GUI.Label(new Rect(16, 12, Screen.width - 32, Screen.height * 0.3f), hud, hudStyle);

            if (showLevelButtons)
            {
                float w = font * 5f, h = font * 2f, y = 12f, x = Screen.width - 16f - w * 3f - 8f;
                var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = font };
                if (GUI.Button(new Rect(x, y, w, h), "◀ Prev", buttonStyle)) Previous();
                if (GUI.Button(new Rect(x + w + 4f, y, w, h), "Restart", buttonStyle)) Restart();
                if (GUI.Button(new Rect(x + (w + 4f) * 2f, y, w, h), "Next ▶", buttonStyle)) Next();
            }

            if (won)
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height),
                    $"<b>CIRCUIT COMPLETE</b>\n<size={bigStyle.fontSize / 3}>Space / Enter: next board    R: replay</size>", bigStyle);
        }
    }
}
