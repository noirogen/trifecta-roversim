using UnityEngine;

/// <summary>
/// Drives the sun, sky, fog and ambient light from a single time-of-day value.
/// Works with the "Skybox/Mars Gradient" and "Custom/Mars Height Fog" shaders.
/// Add to any GameObject, assign the Sun, Sky Material and Fog Material, then drag Time Of Day.
/// </summary>
[ExecuteAlways]
public class MarsTimeOfDay : MonoBehaviour
{
    [Header("Time")]
    [Range(0f, 24f)] public float timeOfDay = 12f;
    [Tooltip("Advance time automatically in Play mode.")]
    public bool animate = false;
    [Tooltip("Real seconds for one full 24h cycle when animating.")]
    public float dayLengthSeconds = 300f;

    [Header("References")]
    public Light sun;
    public Material skyMaterial;
    public Material fogMaterial;

    [Header("Sun Path")]
    [Tooltip("Compass direction the sun travels along (Y rotation).")]
    public float sunAzimuth = 30f;
    [Tooltip("Tilts the sun's path away from the zenith. Jezero crater sits near 18 degrees north.")]
    [Range(-60f, 60f)] public float latitude = 18f;

    [Header("Fog")]
    [Tooltip("Ground level the height fog is measured from. Must match the terrain's Y position.")]
    public float fogBaseHeight = 0f;

    [Header("Colors over the day (left = midnight, middle = noon)")]
    public Gradient zenithColor;
    public Gradient horizonColor;
    public Gradient sunHaloColor;
    public Gradient sunLightColor;
    public AnimationCurve sunIntensity;
    public Gradient ambientColor;

    static readonly int ZenithID = Shader.PropertyToID("_ZenithColor");
    static readonly int HorizonID = Shader.PropertyToID("_HorizonColor");
    static readonly int HaloID = Shader.PropertyToID("_SunHaloColor");
    static readonly int FogColorID = Shader.PropertyToID("_FogColor");
    static readonly int FogGlowID = Shader.PropertyToID("_SunGlowColor");
    static readonly int FogBaseHeightID = Shader.PropertyToID("_FogBaseHeight");
    static readonly int SunDirID = Shader.PropertyToID("_MarsSunDir");
    static readonly int StarMatrixID = Shader.PropertyToID("_MarsStarMatrix");

    void Reset()
    {
        // Time fractions: 0 = midnight, 0.25 = 6am, 0.5 = noon, 0.75 = 6pm
        Color night = new Color(0.03f, 0.03f, 0.05f);

        zenithColor = MakeGradient(
            (0.00f, night),
            (0.22f, night),
            (0.27f, new Color(0.30f, 0.28f, 0.34f)),
            (0.35f, new Color(0.66f, 0.52f, 0.45f)),
            (0.65f, new Color(0.66f, 0.52f, 0.45f)),
            (0.73f, new Color(0.30f, 0.28f, 0.34f)),
            (0.78f, night),
            (1.00f, night));

        horizonColor = MakeGradient(
            (0.00f, new Color(0.04f, 0.035f, 0.05f)),
            (0.22f, new Color(0.04f, 0.035f, 0.05f)),
            (0.27f, new Color(0.55f, 0.45f, 0.42f)),
            (0.35f, new Color(0.77f, 0.56f, 0.43f)),
            (0.65f, new Color(0.77f, 0.56f, 0.43f)),
            (0.73f, new Color(0.55f, 0.45f, 0.42f)),
            (0.78f, new Color(0.04f, 0.035f, 0.05f)),
            (1.00f, new Color(0.04f, 0.035f, 0.05f)));

        // Dust forward-scatters blue, so the sky near the sun stays cool all day
        // and turns markedly blue at sunrise and sunset: the Martian signature.
        sunHaloColor = MakeGradient(
            (0.00f, new Color(0.25f, 0.35f, 0.60f)),
            (0.27f, new Color(0.55f, 0.72f, 1.00f)),
            (0.36f, new Color(0.70f, 0.72f, 0.75f)),
            (0.64f, new Color(0.70f, 0.72f, 0.75f)),
            (0.73f, new Color(0.55f, 0.72f, 1.00f)),
            (1.00f, new Color(0.25f, 0.35f, 0.60f)));

        sunLightColor = MakeGradient(
            (0.25f, new Color(1.00f, 0.70f, 0.50f)),
            (0.35f, new Color(1.00f, 0.88f, 0.75f)),
            (0.65f, new Color(1.00f, 0.88f, 0.75f)),
            (0.75f, new Color(1.00f, 0.70f, 0.50f)));

        sunIntensity = new AnimationCurve(
            new Keyframe(0.00f, 0f),
            new Keyframe(0.24f, 0f),
            new Keyframe(0.28f, 0.3f),
            new Keyframe(0.40f, 0.8f),
            new Keyframe(0.60f, 0.8f),
            new Keyframe(0.72f, 0.3f),
            new Keyframe(0.76f, 0f),
            new Keyframe(1.00f, 0f));

        ambientColor = MakeGradient(
            (0.00f, new Color(0.03f, 0.03f, 0.05f)),
            (0.22f, new Color(0.03f, 0.03f, 0.05f)),
            (0.28f, new Color(0.45f, 0.36f, 0.32f)),
            (0.36f, new Color(0.79f, 0.60f, 0.45f)),
            (0.64f, new Color(0.79f, 0.60f, 0.45f)),
            (0.72f, new Color(0.45f, 0.36f, 0.32f)),
            (0.78f, new Color(0.03f, 0.03f, 0.05f)),
            (1.00f, new Color(0.03f, 0.03f, 0.05f)));

        if (sun == null) sun = RenderSettings.sun;
        if (skyMaterial == null) skyMaterial = RenderSettings.skybox;
    }

    void Update()
    {
        if (animate && Application.isPlaying && dayLengthSeconds > 0f)
        {
            timeOfDay += 24f * Time.deltaTime / dayLengthSeconds;
            if (timeOfDay >= 24f) timeOfDay -= 24f;
        }
        Apply();
    }

    void OnValidate()
    {
        if (sun != null && sun.type != LightType.Directional)
            Debug.LogWarning($"{sun.name} is not a directional light; the sun path will not work.", this);

        Apply();
    }

    void Apply()
    {
        if (zenithColor == null) return; // not initialised yet
        float t = Mathf.Repeat(timeOfDay / 24f, 1f);

        // 6am = horizon (0 deg), noon = highest (90 deg), 6pm = horizon (180 deg).
        // The latitude tilt swings the path off the zenith without moving where the sun rises and sets.
        if (sun != null)
        {
            float elevation = t * 360f - 90f;
            sun.transform.rotation = Quaternion.Euler(0f, sunAzimuth, 0f)
                                   * Quaternion.AngleAxis(-latitude, Vector3.forward)
                                   * Quaternion.Euler(elevation, 0f, 0f);
            sun.color = sunLightColor.Evaluate(t);
            sun.intensity = Mathf.Max(0f, sunIntensity.Evaluate(t));
            RenderSettings.sun = sun;

            // Give the sky and fog shaders the true sun direction (w = 1 means "valid").
            // Needed because URP reports a fake horizon direction when the light is too dim/off.
            Vector3 toSun = -sun.transform.forward;
            Shader.SetGlobalVector(SunDirID, new Vector4(toSun.x, toSun.y, toSun.z, 1f));

            // Rotate the starfield with the sun so stars move across the sky over the night
            Shader.SetGlobalMatrix(StarMatrixID, Matrix4x4.Rotate(Quaternion.Inverse(sun.transform.rotation)));
        }

        Color zenith = zenithColor.Evaluate(t);
        Color horizon = horizonColor.Evaluate(t);
        Color halo = sunHaloColor.Evaluate(t);

        if (skyMaterial != null)
        {
            skyMaterial.SetColor(ZenithID, zenith);
            skyMaterial.SetColor(HorizonID, horizon);
            skyMaterial.SetColor(HaloID, halo);
        }

        // Keep the fog matched to the horizon so the planar edge stays hidden
        if (fogMaterial != null)
        {
            fogMaterial.SetColor(FogColorID, horizon);
            fogMaterial.SetColor(FogGlowID, halo);
            fogMaterial.SetFloat(FogBaseHeightID, fogBaseHeight);
        }

        Color amb = ambientColor.Evaluate(t);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = amb;
        RenderSettings.ambientEquatorColor = amb * 0.85f;
        RenderSettings.ambientGroundColor = amb * 0.55f;

        // Dim environment reflections with the ambient light so shiny/metal
        // surfaces don't glow with a stale daytime sky reflection at night
        RenderSettings.reflectionIntensity = Mathf.Clamp01(amb.maxColorComponent / 0.79f);
    }

    static Gradient MakeGradient(params (float time, Color color)[] keys)
    {
        var colorKeys = new GradientColorKey[keys.Length];
        for (int i = 0; i < keys.Length; i++)
            colorKeys[i] = new GradientColorKey(keys[i].color, keys[i].time);

        var g = new Gradient();
        g.SetKeys(colorKeys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}