using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pcb
{
    /// <summary>
    /// The player: a glowing sphere riding on the board. Waits on a node, takes a direction, then slides
    /// along the trace at a fixed speed until the next node. On a via, Flip turns the board over and the
    /// spark passes through to the other side. Directions are read on screen, so they stay intuitive
    /// when the board is showing its (mirrored) back. Inputs pressed while moving are used on arrival.
    /// </summary>
    public class Spark : MonoBehaviour
    {
        [Tooltip("World units per second. Fixed for the whole game.")]
        public float speed = 8f;

        public Board Board { get; private set; }
        public PcbNode CurrentNode { get; private set; }
        public PcbLayer Layer { get; private set; }
        public bool IsMoving { get; private set; }
        public bool IsTurning => rig && rig.IsTurning;

        public event Action<PcbNode> Arrived;
        public event Action<PcbLayer> Flipped;
        public event Action Blocked;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        BoardRig rig;
        InputAction moveAction, flipAction;
        int lastSector = -1;
        bool hasQueuedMove, hasQueuedFlip;
        Vector2 queuedMove;

        Board.Exit travelling;
        readonly List<Vector2> path = new List<Vector2>();
        int segment;
        float segmentProgress;
        bool wasTurning;
        float turnFromZ, turnToZ;

        Transform core;
        MeshRenderer coreRenderer;
        MaterialPropertyBlock block;
        Light glow;
        TrailRenderer trail;
        readonly List<SpriteRenderer> arrows = new List<SpriteRenderer>();
        float blockedFlash;

        void Awake()
        {
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");
            moveAction.AddBinding("<Gamepad>/dpad");

            flipAction = new InputAction("Flip", InputActionType.Button);
            flipAction.AddBinding("<Keyboard>/space");
            flipAction.AddBinding("<Gamepad>/buttonSouth");
        }

        void OnEnable() { moveAction.Enable(); flipAction.Enable(); }
        void OnDisable() { moveAction.Disable(); flipAction.Disable(); }
        void OnDestroy() { moveAction.Dispose(); flipAction.Dispose(); }

        public void Init(Board board, PcbNode start, BoardRig boardRig)
        {
            Board = board;
            rig = boardRig;
            CurrentNode = start;
            Layer = start.layer;
            IsMoving = false;
            transform.SetParent(board.transform, false);
            transform.localRotation = Quaternion.identity;
            transform.localPosition = LocalPosition(board.NodePosition(start), Layer);
            BuildVisuals();
            if (rig) rig.SnapTo(Layer);
            else board.SetView(Layer);
            trail.Clear();
            RefreshArrows();
        }

        void Update()
        {
            if (!Board) return;
            ReadInput();

            if (IsTurning)
            {
                // Travel through the via while the board turns over.
                var p = transform.localPosition;
                p.z = Mathf.Lerp(turnFromZ, turnToZ, rig.TurnProgress);
                transform.localPosition = p;
                wasTurning = true;
                AnimateVisuals();
                return;
            }
            if (wasTurning)
            {
                wasTurning = false;
                transform.localPosition = LocalPosition(Board.NodePosition(CurrentNode), Layer);
                trail.Clear();
                trail.emitting = true;
                RefreshArrows();
            }

            if (IsMoving) Move(speed * Time.deltaTime);

            if (!IsMoving && enabled) // enabled: arriving at the goal may have disabled us
            {
                if (hasQueuedFlip) { hasQueuedFlip = false; TryFlip(); }
                else if (hasQueuedMove) { hasQueuedMove = false; TryMove(ScreenToBoard(queuedMove)); }
            }
            AnimateVisuals();
        }

        void ReadInput()
        {
            // Paused: drop anything queued so nothing fires the moment play resumes.
            bool paused = PauseMenu.GamePaused;
            if (paused) hasQueuedMove = hasQueuedFlip = false;
            else if (flipAction.WasPressedThisFrame()) hasQueuedFlip = true;

            Vector2 v = moveAction.ReadValue<Vector2>();
            if (v.sqrMagnitude < 0.25f) { lastSector = -1; return; }
            // Treat each new 8-way direction as a fresh press, so holding keys doesn't auto-repeat.
            int sector = Mathf.RoundToInt(Mathf.Atan2(v.y, v.x) / (Mathf.PI * 0.25f)) & 7;
            if (sector == lastSector) return;
            lastSector = sector; // still tracked while paused, so a key held through Resume doesn't count as a new press
            if (paused) return;
            queuedMove = v.normalized;
            hasQueuedMove = true;
        }

        /// <summary>The back of the board is seen mirrored, so screen-right is board-left there.</summary>
        Vector2 ScreenToBoard(Vector2 v) => Board.View == PcbLayer.Back ? new Vector2(-v.x, v.y) : v;

        Vector3 LocalPosition(Vector2 p, PcbLayer layer)
        {
            var theme = Board.theme;
            float height = theme.traceHeight + theme.sparkSize * 0.5f;
            return new Vector3(p.x, p.y, BoardVisuals.Surface(layer, theme.boardThickness) + BoardVisuals.Out(layer) * height);
        }

        bool TryMove(Vector2 dir)
        {
            if (!Board.TryPickExit(CurrentNode, Layer, dir, out var exit)) { Block(); return false; }
            foreach (var m in exit.trace.GetComponents<TraceMechanic>())
                if (!m.CanEnter(this, exit.reversed)) { Block(); return false; }

            foreach (var m in CurrentNode.GetComponents<NodeMechanic>()) m.OnSparkLeave(this);
            travelling = exit;
            exit.trace.GetPath(Board, exit.reversed, path);
            segment = 0;
            segmentProgress = 0f;
            IsMoving = true;
            HideArrows();
            return true;
        }

        void TryFlip()
        {
            if (CurrentNode.type == NodeType.Switch)
            {
                var switchMech = CurrentNode.GetComponent<SwitchMechanic>();
                if (switchMech) switchMech.Toggle();
                return;
            }

            if (!CurrentNode.IsVia) { Block(); return; }
            turnFromZ = transform.localPosition.z;
            Layer = Layer.Other();
            turnToZ = LocalPosition(Vector2.zero, Layer).z;
            HideArrows();
            if (rig)
            {
                trail.emitting = false;
                rig.TurnTo(Layer);
            }
            else
            {
                Board.SetView(Layer);
                wasTurning = true;
            }
            Flipped?.Invoke(Layer);
        }

        void Move(float distance)
        {
            while (distance > 0f && segment < path.Count - 1)
            {
                float left = Vector2.Distance(path[segment], path[segment + 1]) - segmentProgress;
                if (distance < left) { segmentProgress += distance; distance = 0f; }
                else { distance -= left; segment++; segmentProgress = 0f; }
            }

            if (segment >= path.Count - 1)
            {
                transform.localPosition = LocalPosition(path[path.Count - 1], Layer);
                Arrive();
            }
            else
            {
                Vector2 p = Vector2.MoveTowards(path[segment], path[segment + 1], segmentProgress);
                transform.localPosition = LocalPosition(p, Layer);
            }
        }

        void Arrive()
        {
            IsMoving = false;
            CurrentNode = travelling.target;
            foreach (var m in travelling.trace.GetComponents<TraceMechanic>()) m.OnTraversed(this, travelling.reversed);
            foreach (var m in CurrentNode.GetComponents<NodeMechanic>()) m.OnSparkArrive(this);
            RefreshArrows();
            Arrived?.Invoke(CurrentNode);
        }

        void Block()
        {
            blockedFlash = 1f;
            Blocked?.Invoke();
        }

        // ---------------------------------------------------------------- visuals

        void BuildVisuals()
        {
            if (core) return;
            var theme = Board.theme;
            block = new MaterialPropertyBlock();

            coreRenderer = BoardVisuals.Part(transform, BoardVisuals.Sphere, Vector3.zero, Quaternion.identity,
                Vector3.one * theme.sparkSize, theme.sparkMaterial, null);
            coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            core = coreRenderer.transform;
            core.name = "Core";

            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.type = LightType.Point;
            glow.range = theme.sparkLightRange;
            glow.intensity = theme.sparkLightIntensity;
            glow.color = theme.spark;
            glow.shadows = LightShadows.None;

            trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = theme.spriteMaterial;
            trail.time = 0.2f;
            trail.minVertexDistance = 0.03f;
            trail.numCapVertices = 2;
            trail.widthMultiplier = theme.sparkSize * 0.6f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(theme.spark, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
        }

        void RefreshArrows()
        {
            var theme = Board.theme;
            int count = 0;
            float radius = theme.capacitorSize * 0.5f + 0.18f;
            // Arrows lie on the board surface; the spark floats a little above it.
            float dz = BoardVisuals.Out(Layer) * (theme.traceHeight + 0.006f - (theme.traceHeight + theme.sparkSize * 0.5f));
            foreach (var e in Board.GetExits(CurrentNode))
            {
                if (e.layer != Layer) continue;
                if (count == arrows.Count)
                {
                    var go = new GameObject("Arrow");
                    go.transform.SetParent(transform, false);
                    go.transform.localScale = Vector3.one * 0.16f;
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = theme.triangle;
                    sr.sharedMaterial = theme.spriteMaterial;
                    arrows.Add(sr);
                }
                var arrow = arrows[count++];
                arrow.transform.localPosition = new Vector3(e.direction.x * radius, e.direction.y * radius, dz);
                arrow.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(e.direction.y, e.direction.x) * Mathf.Rad2Deg);
            }
            for (int i = 0; i < arrows.Count; i++) arrows[i].enabled = i < count;
        }

        void HideArrows()
        {
            foreach (var a in arrows) a.enabled = false;
        }

        void AnimateVisuals()
        {
            var theme = Board.theme;
            blockedFlash = Mathf.MoveTowards(blockedFlash, 0f, Time.deltaTime * 4f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);

            Color c = Color.Lerp(theme.spark, theme.sparkBlocked, blockedFlash);
            coreRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(EmissionColorId, c * (theme.sparkGlow * (0.8f + 0.4f * pulse)));
            coreRenderer.SetPropertyBlock(block);
            core.localPosition = UnityEngine.Random.insideUnitSphere * (0.05f * blockedFlash);
            core.localScale = Vector3.one * theme.sparkSize * (1f + 0.08f * pulse);

            glow.color = c;
            glow.intensity = theme.sparkLightIntensity * (0.85f + 0.3f * pulse);

            var arrowColor = theme.spark;
            arrowColor.a = 0.5f + 0.5f * pulse;
            foreach (var a in arrows) a.color = arrowColor;
        }
    }
}
