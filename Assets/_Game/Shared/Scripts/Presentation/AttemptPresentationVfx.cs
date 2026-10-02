using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// One instance on the attempt root. Gameplay supplies accepted events; this only draws.
[DisallowMultipleComponent]
public sealed class AttemptPresentationVfx : MonoBehaviour
{
    [SerializeField] private Material lineMaterial;
    [Tooltip("Boss body/weapon sprites only. Exclude warning, prediction, platform and UI renderers.")]
    [SerializeField] private SpriteRenderer[] bossParts;
    [SerializeField] private string sortingLayer = "Default";
    [SerializeField] private int sortingOrder = 5;

    private sealed class Flash
    {
        public GameObject root;
        public LineRenderer[] lines;
        public SpriteRenderer[] sprites;
        public float born, life, alpha;
        public bool echo;
        public Vector3 velocity;
    }

    private readonly List<Flash> flashes = new List<Flash>();
    private Transform owned;
    private bool running, ghosting;
    private float nextEcho;
    private GameObject slowRoot;
    private LineRenderer[] slowLines;
    private Transform slowAnchor;

    public void BeginAttempt()
    {
        EndAttempt();
        running = isActiveAndEnabled;
    }

    public void EndAttempt()
    {
        running = ghosting = false;
        nextEcho = 0f;
        slowAnchor = null;
        slowRoot = null;
        slowLines = null;
        flashes.Clear();
        if (owned != null)
        {
            owned.gameObject.SetActive(false); // Hide now; Unity destruction is deferred.
            Destroy(owned.gameObject);
            owned = null;
        }
    }

    private bool CanDraw => running && isActiveAndEnabled;

    private GameObject Child(string name)
    {
        if (owned == null)
        {
            owned = new GameObject("AttemptVfx_Transient").transform;
            owned.gameObject.layer = gameObject.layer;
            owned.SetParent(transform, true);
        }
        var go = new GameObject(name);
        go.layer = gameObject.layer;
        go.transform.SetParent(owned, true);
        return go;
    }

    private LineRenderer Line(GameObject parent, Vector3[] points, float width, Color color)
    {
        var go = new GameObject("Line");
        go.layer = parent.layer;
        go.transform.SetParent(parent.transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.sortingLayerID = SortingLayer.NameToID(sortingLayer);
        line.sortingOrder = sortingOrder;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return line;
    }

    private void Rays(Vector3 point, Vector2 direction, int count, float length, float life)
    {
        if (!CanDraw || lineMaterial == null || direction.sqrMagnitude < .0001f) return;
        var go = Child("ImpactLines");
        var lines = new LineRenderer[count];
        float facing = Mathf.Atan2(direction.y, direction.x);
        for (int i = 0; i < count; i++)
        {
            float angle = facing + Mathf.Lerp(-.85f, .85f, i / (float)(count - 1));
            var ray = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            lines[i] = Line(go, new[] { point + ray * .035f, point + ray * length }, .025f, Color.white);
        }
        flashes.Add(new Flash { root = go, lines = lines, born = Time.unscaledTime, life = life, alpha = 1f });
    }

    // A/B: after successful Launch, never from the warning/input state.
    public void Muzzle(Vector3 origin, Vector2 direction) => Rays(origin, direction, 3, .20f, .06f);

    // One accepted discharge, never one call per pellet. Only the visual length changes.
    public void Muzzle(Vector3 origin, Vector2 direction, float visualScale)
        => Rays(origin, direction, 3, .20f * Mathf.Clamp(visualScale, 1f, 1.25f), .06f);
    // A: actual contact only; direction points away from the impacted surface.
    public void BulletHit(Vector3 contact, Vector2 outward) => Rays(contact, outward, 3, .17f, .10f);
    // E: accepted counter contact only.
    public void CounterSuccess(Vector3 contact, Vector2 outward) => Rays(contact, outward, 4, .23f, .12f);

    // D/G: world-space visual blade pivot and reach; no root/weapon transform is modified.
    public void BladeSlash(Vector3 pivot, Vector2 direction, float visualReach)
    {
        if (!CanDraw || lineMaterial == null || visualReach <= 0f || direction.sqrMagnitude < .0001f) return;
        var go = Child("BladeArc");
        var points = new Vector3[9];
        float facing = Mathf.Atan2(direction.y, direction.x);
        for (int i = 0; i < points.Length; i++)
        {
            float angle = facing + Mathf.Lerp(-.9f, .9f, i / 8f);
            points[i] = pivot + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * visualReach;
        }
        var line = Line(go, points, .025f, Color.white);
        flashes.Add(new Flash { root = go, lines = new[] { line }, born = Time.unscaledTime, life = .10f, alpha = 1f });
    }

    // Arm-Blade swing only; hit and deflection remain separate accepted gameplay events.
    public void ArmBladeSwing(Vector3 pivot, Vector2 direction, float visualReach)
    {
        if (!CanDraw || lineMaterial == null || visualReach <= 0f || direction.sqrMagnitude < .0001f) return;
        BladeSlash(pivot, direction, visualReach);
        var go = Child("ArmBladeLine");
        Vector3 forward = direction.normalized;
        var line = Line(go, new[] { pivot + forward * (visualReach * .15f),
            pivot + forward * (visualReach * .5f) }, .025f, Color.white);
        flashes.Add(new Flash { root = go, lines = new[] { line }, born = Time.unscaledTime, life = .10f, alpha = 1f });
    }

    // C/G: call on state entry/exit. Repeated true calls do not restart the sampler.
    public void SetGhosting(bool active)
    {
        active &= CanDraw;
        if (ghosting == active) return;
        ghosting = active;
        if (active) nextEcho = Time.unscaledTime;
    }

    private void CaptureEcho()
    {
        if (bossParts == null || bossParts.Length == 0) return;
        int count = 0;
        foreach (var f in flashes) if (f.echo) count++;
        if (count >= 2)
            for (int i = 0; i < flashes.Count; i++)
                if (flashes[i].echo) { RemoveFlash(i); break; }
        var copies = new List<SpriteRenderer>();
        GameObject group = null;
        foreach (var source in bossParts)
        {
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy || source.sprite == null) continue;
            if (group == null) group = Child("BossEcho");
            var go = new GameObject("Part");
            go.layer = group.layer;
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            go.transform.localScale = source.transform.lossyScale;
            go.transform.SetParent(group.transform, true);
            var copy = go.AddComponent<SpriteRenderer>();
            copy.sprite = source.sprite;
            copy.sharedMaterial = source.sharedMaterial;
            copy.flipX = source.flipX;
            copy.flipY = source.flipY;
            copy.drawMode = source.drawMode;
            copy.size = source.size;
            copy.sortingLayerID = source.sortingLayerID;
            copy.sortingOrder = source.sortingOrder - 1;
            copy.color = new Color(.75f, .75f, .75f, .15f);
            copies.Add(copy);
        }
        if (group != null)
            flashes.Add(new Flash { root = group, sprites = copies.ToArray(), born = Time.unscaledTime, life = .15f, alpha = .15f, echo = true });
    }

    // F: safe to synchronize each frame; allocates only on the first true call.
    public void SetTimeSlow(bool active, Transform torso)
    {
        active &= CanDraw && torso != null && torso.gameObject.activeInHierarchy;
        slowAnchor = active ? torso : null;
        if (!active)
        {
            if (slowRoot != null) slowRoot.SetActive(false);
            return;
        }
        if (lineMaterial == null) return;
        if (slowRoot == null)
        {
            slowRoot = Child("TimeSlowBrackets");
            slowLines = new[] {
                Line(slowRoot, new Vector3[4], .02f, new Color(.55f,.55f,.55f,.55f)),
                Line(slowRoot, new Vector3[4], .02f, new Color(.55f,.55f,.55f,.55f)) };
        }
        slowRoot.SetActive(true);
        PositionBrackets();
    }

    private void PositionBrackets()
    {
        if (slowRoot == null || !slowRoot.activeSelf) return;
        if (slowAnchor == null || !slowAnchor.gameObject.activeInHierarchy) { slowRoot.SetActive(false); return; }
        Vector3 p = slowAnchor.position;
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1 : 1;
            slowLines[i].SetPosition(0, p + new Vector3(s * .13f, .24f, 0));
            slowLines[i].SetPosition(1, p + new Vector3(s * .19f, .24f, 0));
            slowLines[i].SetPosition(2, p + new Vector3(s * .19f, -.24f, 0));
            slowLines[i].SetPosition(3, p + new Vector3(s * .13f, -.24f, 0));
        }
    }

    // H: pass saved bounds only when that intact platform really becomes inactive.
    public void HighCBroken(Bounds previousBounds)
    {
        if (!CanDraw || lineMaterial == null || previousBounds.size.x <= 0f) return;
        for (int i = 0; i < 4; i++)
        {
            var go = Child("HighCFragment");
            float x = Mathf.Lerp(previousBounds.min.x, previousBounds.max.x, (i + .5f) / 4f);
            Vector3 p = new Vector3(x, previousBounds.max.y, previousBounds.center.z);
            var line = Line(go, new[] { p, p + new Vector3(.12f, -.035f, 0) }, .025f, new Color(.6f,.6f,.6f,.7f));
            flashes.Add(new Flash { root = go, lines = new[] { line }, born = Time.unscaledTime, life = .35f, alpha = .7f,
                velocity = new Vector3((i - 1.5f) * .15f, -.6f, 0) });
        }
    }

    private void LateUpdate()
    {
        if (!CanDraw) return;
        float now = Time.unscaledTime;
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            var f = flashes[i];
            float age = now - f.born;
            if (f.root == null || age >= f.life) { RemoveFlash(i); continue; }
            float alpha = f.alpha * (1f - age / f.life);
            if (f.lines != null) foreach (var line in f.lines)
            {
                if (line == null) continue;
                Color color = line.startColor; color.a = alpha;
                line.startColor = line.endColor = color;
                if (f.velocity != Vector3.zero)
                    for (int j = 0; j < line.positionCount; j++)
                        line.SetPosition(j, line.GetPosition(j) + f.velocity * Time.unscaledDeltaTime);
            }
            if (f.sprites != null) foreach (var sprite in f.sprites)
                if (sprite != null) sprite.color = new Color(.75f, .75f, .75f, alpha);
        }
        if (ghosting && now >= nextEcho)
        {
            CaptureEcho();
            nextEcho = now + .075f; // No catch-up burst after a frame hitch.
        }
        PositionBrackets();
    }

    private void RemoveFlash(int index)
    {
        var go = flashes[index].root;
        if (go != null) { go.SetActive(false); Destroy(go); }
        flashes.RemoveAt(index);
    }

    private void OnEnable() => EndAttempt();
    private void OnDisable() => EndAttempt();
}
