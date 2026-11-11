using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Training
{
    public class TimeWarpPlacement : MonoBehaviour
    {
        public enum PlacementMode
        {
            BelowTarget,
            InsideTopRightOfTarget,
            InsideTopLeftOfTarget,
            MatchTargetRect,
            ScreenTopRightFallback
        }

        private const int ArrowTextureSize = 32;
        private const float Padding = 6f;
        private const float RowGap = 3f;

        [SerializeField] private TrainingOrganizer organizer;

        [Header("Speeds")]
        [Tooltip("One arrow per step. Arrow N sets the speed to N times real time.")]
        [SerializeField, Range(2, 20)] private int arrowCount = 10;
        [SerializeField] private Key speedUpKey = Key.Period;
        [SerializeField] private Key slowDownKey = Key.Comma;

        [Header("Placement & Sizing")]
        [Tooltip("The target RectTransform where this widget should be placed and filled.")]
        [SerializeField] private RectTransform targetRect;
        [SerializeField] private PlacementMode placementMode = PlacementMode.MatchTargetRect;
        [Tooltip("If enabled, arrow size and spacing scale automatically to fill the width of the target rect.")]
        [SerializeField] private bool scaleToFitTarget = true;
        [SerializeField, Min(0f)] private float topOffset = 72f;
        [SerializeField, Min(0f)] private float rightMargin = 12f;
        [SerializeField, Min(0f)] private float gapBelow = 6f;

        [Header("Look")]
        [SerializeField, Min(8f)] private float arrowSize = 18f;
        [SerializeField, Min(0f)] private float arrowGap = 2f;
        [SerializeField] private bool showClock = true;
        [SerializeField] private Color litColor = new Color(0.36f, 0.92f, 0.36f);
        [SerializeField] private Color unlitColor = new Color(0.32f, 0.34f, 0.32f);
        [SerializeField] private Color laggingColor = new Color(1f, 0.72f, 0.25f);
        [SerializeField, Range(0f, 1f)] private float backgroundOpacity = 0.8f;

        private readonly SpeedMeter meter = new SpeedMeter(0.5f);
        private readonly Vector3[] corners = new Vector3[4];
        private Texture2D arrow;
        private Texture2D background;
        private GUIStyle labelStyle;
        private GUIStyle clockStyle;

        private void Awake()
        {
            if (organizer == null)
                organizer = FindAnyObjectByType<TrainingOrganizer>();
        }

        private void OnDestroy()
        {
            if (arrow != null)
                Destroy(arrow);
            if (background != null)
                Destroy(background);
        }

        private void Update()
        {
            if (organizer == null)
                return;

            meter.Sample();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || IsTyping())
                return;

            if (keyboard[speedUpKey].wasPressedThisFrame)
                SetLevel(CurrentLevel() + 1);
            else if (keyboard[slowDownKey].wasPressedThisFrame)
                SetLevel(CurrentLevel() - 1);
        }

        private void OnGUI()
        {
            if (organizer == null)
                return;

            EnsureResources();
            GUI.depth = -900;

            // Get target rect dimensions if available
            float targetW = 0f;
            float targetH = 0f;
            Rect panel = CalculatePanelRect(ref targetW, ref targetH);

            GUI.DrawTexture(panel, background);

            // Calculate dynamic sizing if scaleToFitTarget is active and a target exists
            float currentArrowSize = arrowSize;
            float currentArrowGap = arrowGap;
            float labelWidth = 70f;

            if (scaleToFitTarget && targetRect != null)
            {
                float availableWidth = targetW - (Padding * 2f) - labelWidth - 8f;
                // Distribute available width across arrows and gaps
                // availableWidth = (arrowCount * size) + ((arrowCount - 1) * gap)
                // Let's keep gap proportional or small, e.g., gap = size * 0.15f
                float totalSlotsFactor = arrowCount + (arrowCount - 1) * 0.15f;
                currentArrowSize = Mathf.Max(8f, availableWidth / totalSlotsFactor);
                currentArrowGap = currentArrowSize * 0.15f;
            }

            float arrowsWidth = arrowCount * currentArrowSize + (arrowCount - 1) * currentArrowGap;
            int level = CurrentLevel();
            float y = panel.y + Padding;

            // Draw arrows
            for (int i = 1; i <= arrowCount; i++)
            {
                var rect = new Rect(panel.x + Padding + (i - 1) * (currentArrowSize + currentArrowGap), y, currentArrowSize, currentArrowSize);
                bool hovered = rect.Contains(Event.current.mousePosition);
                Color color = i <= level ? litColor : unlitColor;
                GUI.color = hovered ? Color.Lerp(color, Color.white, 0.35f) : color;
                GUI.DrawTexture(rect, arrow);
                GUI.color = Color.white;

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                    SetLevel(i);
            }

            // Draw Label
            bool lagging = organizer.State == TrainingState.Running && meter.IsBehind(organizer.Speed);
            labelStyle.normal.textColor = lagging ? laggingColor : Color.white;
            string label = lagging ? $"{level}x ({meter.Actual:0.#}x)" : organizer.State == TrainingState.Paused ? $"{level}x ||" : $"{level}x";
            GUI.Label(new Rect(panel.x + Padding + arrowsWidth + 8f, y, labelWidth, currentArrowSize), label, labelStyle);

            // Draw Clock
            if (showClock)
            {
                float clockY = y + currentArrowSize + RowGap;
                GUI.Label(new Rect(panel.x + Padding, clockY, targetW - Padding * 2f, 16f), Clock(organizer.SimulatedSeconds), clockStyle);
            }
        }

        private Rect CalculatePanelRect(ref float outWidth, ref float outHeight)
        {
            float defaultArrowsWidth = arrowCount * arrowSize + (arrowCount - 1) * arrowGap;
            float labelWidth = 70f;
            float defaultWidth = Padding * 2f + defaultArrowsWidth + 8f + labelWidth;
            float defaultHeight = Padding * 2f + arrowSize + (showClock ? RowGap + 16f : 0f);

            if (targetRect == null)
            {
                outWidth = defaultWidth;
                outHeight = defaultHeight;
                return new Rect(Screen.width - rightMargin - defaultWidth, topOffset, defaultWidth, defaultHeight);
            }

            targetRect.GetWorldCorners(corners);
            Canvas canvas = targetRect.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            Vector2 screenBL = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 screenTL = RectTransformUtility.WorldToScreenPoint(cam, corners[1]);
            Vector2 screenTR = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

            float imGuiTop = Screen.height - Mathf.Max(screenTL.y, screenTR.y);
            float imGuiBottom = Screen.height - screenBL.y;
            float imGuiLeft = screenBL.x;
            float imGuiWidth = Mathf.Abs(screenTR.x - screenBL.x);
            float imGuiHeight = Mathf.Abs(screenTL.y - screenBL.y);

            outWidth = imGuiWidth;
            outHeight = imGuiHeight;

            switch (placementMode)
            {
                case PlacementMode.InsideTopRightOfTarget:
                    outWidth = defaultWidth;
                    outHeight = defaultHeight;
                    return new Rect(imGuiLeft + imGuiWidth - defaultWidth - rightMargin, imGuiTop + Padding, defaultWidth, defaultHeight);

                case PlacementMode.InsideTopLeftOfTarget:
                    outWidth = defaultWidth;
                    outHeight = defaultHeight;
                    return new Rect(imGuiLeft + rightMargin, imGuiTop + Padding, defaultWidth, defaultHeight);

                case PlacementMode.MatchTargetRect:
                    return new Rect(imGuiLeft, imGuiTop, imGuiWidth, imGuiHeight);

                case PlacementMode.BelowTarget:
                default:
                    outWidth = defaultWidth;
                    outHeight = defaultHeight;
                    return new Rect(Screen.width - rightMargin - defaultWidth, imGuiBottom + gapBelow, defaultWidth, defaultHeight);
            }
        }

        private int CurrentLevel()
        {
            return Mathf.Clamp(Mathf.RoundToInt(organizer.Speed), 1, arrowCount);
        }

        private void SetLevel(int level)
        {
            organizer.SetTimeScale(Mathf.Clamp(level, 1, arrowCount));
        }

        private static string Clock(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"T+ {total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}";
        }

        private static bool IsTyping()
        {
            if (GUIUtility.keyboardControl != 0)
                return true;

            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            return selected != null && selected.GetComponent<TMP_InputField>() != null;
        }

        private void EnsureResources()
        {
            if (arrow != null)
                return;

            arrow = ArrowTexture.Create(ArrowTextureSize);
            background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0.06f, 0.06f, 0.07f, backgroundOpacity));
            background.Apply();

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            clockStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            clockStyle.normal.textColor = new Color(0.36f, 0.92f, 0.36f);
        }
    }
}