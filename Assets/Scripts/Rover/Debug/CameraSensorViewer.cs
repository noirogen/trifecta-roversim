using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rover
{

    // Debug overlay that shows every CameraSensorComponent under a root as a grid on screen,
    // at the sensor's own resolution and grayscale setting. 
    // Each feed is rendered a second time for display, so disable this component for training runs.

    public class CameraSensorViewer : MonoBehaviour
    {
        [Tooltip("Where to look for camera sensors. Defaults to this object.")]
        [SerializeField] private Transform searchRoot;
        [SerializeField] private bool visible = true;
        [SerializeField] private Key toggleKey = Key.F2;

        [Header("Layout")]
        [Tooltip("Size the feeds to fill the screen. Off = use Cell Size.")]
        [SerializeField] private bool fitToScreen = true;
        [Tooltip("Fraction of the screen the grid may cover when fitting.")]
        [SerializeField, Range(0.1f, 1f)] private float screenCoverage = 1f;
        [SerializeField, Min(16)] private int cellSize = 320;
        [SerializeField, Min(0)] private int padding = 6;

        [Tooltip("Seconds between refreshes. 0 = every frame.")]
        [SerializeField, Min(0f)] private float refreshInterval = 0.1f;

        private class Feed
        {
            public CameraSensorComponent sensor;
            public Texture2D texture;
            public Color32[] pixels;
        }

        private const int LabelHeight = 18;

        private readonly List<Feed> feeds = new List<Feed>();
        private float nextRefresh;

        private void Start()
        {
            Transform root = searchRoot != null ? searchRoot : transform;
            foreach (var sensor in root.GetComponentsInChildren<CameraSensorComponent>(true))
            {
                if (sensor.Camera == null)
                    continue;

                feeds.Add(new Feed
                {
                    sensor = sensor,
                    texture = new Texture2D(sensor.Width, sensor.Height, TextureFormat.RGB24, false)
                    {
                        filterMode = FilterMode.Point,   // show real pixels when scaled up
                        wrapMode = TextureWrapMode.Clamp
                    }
                });
            }

            if (feeds.Count == 0)
                Debug.LogWarning($"No camera sensors found under {root.name}.", this);
        }

        private void LateUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
                visible = !visible;

            if (!visible || Time.unscaledTime < nextRefresh)
                return;

            nextRefresh = Time.unscaledTime + refreshInterval;
            foreach (var feed in feeds)
                Capture(feed);
        }

        private static void Capture(Feed feed)
        {
            var sensor = feed.sensor;
            Camera camera = sensor.Camera;
            int width = sensor.Width;
            int height = sensor.Height;

            if (feed.texture.width != width || feed.texture.height != height)
                feed.texture.Reinitialize(width, height);

            var renderTexture = RenderTexture.GetTemporary(width, height, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;

            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            feed.texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);

            if (sensor.Grayscale)
                ToGrayscale(feed);

            feed.texture.Apply();
        }

        private static void ToGrayscale(Feed feed)
        {
            feed.pixels = feed.texture.GetPixels32();
            for (int i = 0; i < feed.pixels.Length; i++)
            {
                var c = feed.pixels[i];
                byte g = (byte)((c.r + c.g + c.b) / 3);   // ML-Agents averages channels
                feed.pixels[i] = new Color32(g, g, g, 255);
            }
            feed.texture.SetPixels32(feed.pixels);
        }

        private void OnGUI()
        {
            if (!visible || feeds.Count == 0)
                return;

            int size;
            int columns;
            if (fitToScreen)
                FitGrid(out size, out columns);
            else
            {
                size = cellSize;
                columns = Mathf.Max(1, (Screen.width - padding) / (size + padding));
            }

            int cellHeight = size + LabelHeight;

            for (int i = 0; i < feeds.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                float x = padding + column * (size + padding);
                float y = padding + row * (cellHeight + padding);

                var feed = feeds[i];
                var imageRect = new Rect(x, y + LabelHeight, size, size);

                GUI.Box(new Rect(x, y, size, cellHeight), GUIContent.none);
                GUI.Label(new Rect(x + 4, y, size - 8, LabelHeight),
                          $"{feed.sensor.SensorName} ({feed.sensor.Width}x{feed.sensor.Height})");
                GUI.DrawTexture(imageRect, feed.texture, ScaleMode.ScaleToFit, false);
            }
        }

        /// Picks the column count that gives the largest square cells that still fit on screen.
        private void FitGrid(out int size, out int columns)
        {
            float width = Screen.width * screenCoverage;
            float height = Screen.height * screenCoverage;
            int count = feeds.Count;

            size = 16;
            columns = 1;
            for (int c = 1; c <= count; c++)
            {
                int rows = Mathf.CeilToInt(count / (float)c);
                float byWidth = (width - padding * (c + 1)) / c;
                float byHeight = (height - padding * (rows + 1)) / rows - LabelHeight;
                int candidate = Mathf.FloorToInt(Mathf.Min(byWidth, byHeight));

                if (candidate > size)
                {
                    size = candidate;
                    columns = c;
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var feed in feeds)
                Destroy(feed.texture);
        }
    }
}