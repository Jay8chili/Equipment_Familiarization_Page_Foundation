using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.UIElements;


public static class ExtensionMethods
{
    private static Vector3 currentvelocity = Vector3.zero;
    private static float speed = 0.3f;

    public static async Task MoveToPositionAsync(this Transform originalTransform, TransformContainer destinationTransform, CancellationToken token)
    {
        if (originalTransform == null)
        {
            return;
        }

        var destination = destinationTransform;
        var lerpAmount = 0f;

        while (Vector3.Distance(originalTransform.position, destination.Position) > 0.001f)
        {
            lerpAmount = Mathf.Clamp01(lerpAmount + Time.deltaTime * 0.1f);

            var newPosition = Vector3.Lerp(originalTransform.position, destination.Position, lerpAmount);
            var newRotation = Quaternion.Lerp(originalTransform.rotation, destination.Rotation, lerpAmount);
            var newScale = Vector3.Lerp(originalTransform.localScale, destination.localScale, lerpAmount);

            await Task.Yield();

            if (token.IsCancellationRequested)
            {
                originalTransform.SetPositionAndRotation(destinationTransform.Position, destinationTransform.Rotation);
                originalTransform.localScale = destinationTransform.localScale;
                return;
            }

            originalTransform.SetPositionAndRotation(newPosition, newRotation);
            originalTransform.localScale = newScale;
        }
    }

    // =========================================================================
    //  TWEEN ENGINE CORE
    // =========================================================================

    /// <summary>
    /// Core tween loop. Drives a normalized 0-1 value through an easing
    /// function and calls your apply action every frame. Zero allocations
    /// during the loop itself. All other DoXxx methods are built on this.
    /// </summary>
    private static async Task TweenAsync(float duration, Ease ease, Action<float> onUpdate,
        CancellationToken token, Action onComplete = null, float delay = 0f,
        int loopCount = 1, LoopType loopType = LoopType.Restart)
    {
        if (delay > 0f)
        {
            float waited = 0f;
            while (waited < delay)
            {
                if (token.IsCancellationRequested) return;
                await Task.Yield();
                waited += Time.deltaTime;
            }
        }

        int loops = loopCount <= 0 ? int.MaxValue : loopCount; // 0 or negative = infinite

        for (int loop = 0; loop < loops; loop++)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (token.IsCancellationRequested) return;
                await Task.Yield();
                if (token.IsCancellationRequested) return;

                elapsed += Time.deltaTime;
                float rawT = Mathf.Clamp01(elapsed / duration);

                // Reverse on odd loops for Yoyo
                float t = (loopType == LoopType.Yoyo && loop % 2 == 1)
                    ? 1f - rawT : rawT;

                onUpdate(EaseEvaluate(t, ease));
            }

            // Snap to final value at end of each loop
            float finalT = (loopType == LoopType.Yoyo && loop % 2 == 1) ? 0f : 1f;
            onUpdate(EaseEvaluate(finalT, ease));
        }

        onComplete?.Invoke();
    }

    // =========================================================================
    //  MOVE
    // =========================================================================

    /// <summary>Move world position to target over duration.</summary>
    public static async Task DoMove(this Transform t, Vector3 target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        int loopCount = 1, LoopType loopType = LoopType.Restart, Action onComplete = null)
    {
        Vector3 start = t.position;
        await TweenAsync(duration, ease, v => {
            if (t != null) t.position = Vector3.LerpUnclamped(start, target, v);
        }, token, onComplete, delay, loopCount, loopType);
    }

    /// <summary>Move world position by an offset over duration.</summary>
    public static async Task DoMoveBy(this Transform t, Vector3 offset, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Vector3 start = t.position;
        Vector3 target = start + offset;
        await TweenAsync(duration, ease, v => {
            if (t != null) t.position = Vector3.LerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    /// <summary>Move local position to target over duration.</summary>
    public static async Task DoLocalMove(this Transform t, Vector3 target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        int loopCount = 1, LoopType loopType = LoopType.Restart, Action onComplete = null)
    {
        Vector3 start = t.localPosition;
        await TweenAsync(duration, ease, v => {
            if (t != null) t.localPosition = Vector3.LerpUnclamped(start, target, v);
        }, token, onComplete, delay, loopCount, loopType);
    }

    /// <summary>Move only the X axis (world).</summary>
    public static async Task DoMoveX(this Transform t, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        float start = t.position.x;
        await TweenAsync(duration, ease, v => {
            if (t != null) { var p = t.position; p.x = Mathf.LerpUnclamped(start, target, v); t.position = p; }
        }, token, onComplete, delay);
    }

    /// <summary>Move only the Y axis (world).</summary>
    public static async Task DoMoveY(this Transform t, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        float start = t.position.y;
        await TweenAsync(duration, ease, v => {
            if (t != null) { var p = t.position; p.y = Mathf.LerpUnclamped(start, target, v); t.position = p; }
        }, token, onComplete, delay);
    }

    /// <summary>Move only the Z axis (world).</summary>
    public static async Task DoMoveZ(this Transform t, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        float start = t.position.z;
        await TweenAsync(duration, ease, v => {
            if (t != null) { var p = t.position; p.z = Mathf.LerpUnclamped(start, target, v); t.position = p; }
        }, token, onComplete, delay);
    }

    // =========================================================================
    //  ROTATE
    // =========================================================================

    /// <summary>Rotate to target euler angles over duration.</summary>
    public static async Task DoRotate(this Transform t, Vector3 targetEuler, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        int loopCount = 1, LoopType loopType = LoopType.Restart, Action onComplete = null)
    {
        Quaternion start = t.rotation;
        Quaternion target = Quaternion.Euler(targetEuler);
        await TweenAsync(duration, ease, v => {
            if (t != null) t.rotation = Quaternion.SlerpUnclamped(start, target, v);
        }, token, onComplete, delay, loopCount, loopType);
    }

    /// <summary>Rotate to target quaternion over duration.</summary>
    public static async Task DoRotateQuaternion(this Transform t, Quaternion target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Quaternion start = t.rotation;
        await TweenAsync(duration, ease, v => {
            if (t != null) t.rotation = Quaternion.SlerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    /// <summary>Rotate local to target euler angles over duration.</summary>
    public static async Task DoLocalRotate(this Transform t, Vector3 targetEuler, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Quaternion start = t.localRotation;
        Quaternion target = Quaternion.Euler(targetEuler);
        await TweenAsync(duration, ease, v => {
            if (t != null) t.localRotation = Quaternion.SlerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    /// <summary>Rotate around an axis by a total angle over duration.</summary>
    public static async Task DoRotateAround(this Transform t, Vector3 axis, float angle, float duration,
        Ease ease = Ease.InOutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Quaternion start = t.rotation;
        Quaternion target = Quaternion.AngleAxis(angle, axis) * start;
        await TweenAsync(duration, ease, v => {
            if (t != null) t.rotation = Quaternion.SlerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    /// <summary>Continuously spin around a local axis. Loops infinitely by default.</summary>
    public static async Task DoSpin(this Transform t, Vector3 axis, float degreesPerSecond,
        CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Yield();
            if (t == null || token.IsCancellationRequested) return;
            t.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }

    // =========================================================================
    //  SCALE
    // =========================================================================

    /// <summary>Scale to target over duration.</summary>
    public static async Task DoScale(this Transform t, Vector3 target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        int loopCount = 1, LoopType loopType = LoopType.Restart, Action onComplete = null)
    {
        Vector3 start = t.localScale;
        await TweenAsync(duration, ease, v => {
            if (t != null) t.localScale = Vector3.LerpUnclamped(start, target, v);
        }, token, onComplete, delay, loopCount, loopType);
    }

    /// <summary>Uniform scale to a single value.</summary>
    public static async Task DoScale(this Transform t, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        await t.DoScale(Vector3.one * target, duration, ease, token, delay, 1, LoopType.Restart, onComplete);
    }

    /// <summary>Scale X axis only.</summary>
    public static async Task DoScaleX(this Transform t, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        float start = t.localScale.x;
        await TweenAsync(duration, ease, v => {
            if (t != null) { var s = t.localScale; s.x = Mathf.LerpUnclamped(start, target, v); t.localScale = s; }
        }, token, onComplete, delay);
    }

    /// <summary>Scale Y axis only.</summary>
    public static async Task DoScaleY(this Transform t, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        float start = t.localScale.y;
        await TweenAsync(duration, ease, v => {
            if (t != null) { var s = t.localScale; s.y = Mathf.LerpUnclamped(start, target, v); t.localScale = s; }
        }, token, onComplete, delay);
    }

    /// <summary>Scale Z axis only.</summary>
    public static async Task DoScaleZ(this Transform t, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        float start = t.localScale.z;
        await TweenAsync(duration, ease, v => {
            if (t != null) { var s = t.localScale; s.z = Mathf.LerpUnclamped(start, target, v); t.localScale = s; }
        }, token, onComplete, delay);
    }

    // =========================================================================
    //  POP / PUNCH / BOUNCE / SHAKE  (the fun ones)
    // =========================================================================

    /// <summary>
    /// Pop: quick scale up then back to original. Great for UI feedback.
    /// </summary>
    public static async Task DoPop(this Transform t, float popScale = 1.2f, float duration = 0.3f,
        Ease ease = Ease.OutBack, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Vector3 original = t.localScale;
        Vector3 peak = original * popScale;

        // Scale up
        await TweenAsync(duration * 0.4f, ease, v => {
            if (t != null) t.localScale = Vector3.LerpUnclamped(original, peak, v);
        }, token, delay: delay);

        if (token.IsCancellationRequested) return;

        // Scale back down
        await TweenAsync(duration * 0.6f, Ease.OutBounce, v => {
            if (t != null) t.localScale = Vector3.LerpUnclamped(peak, original, v);
        }, token, onComplete);
    }

    /// <summary>
    /// Punch position: quick displacement that snaps back with overshoot.
    /// </summary>
    public static async Task DoPunchPosition(this Transform t, Vector3 punch, float duration = 0.4f,
        int vibrato = 6, float elasticity = 0.5f, CancellationToken token = default,
        float delay = 0f, Action onComplete = null)
    {
        Vector3 origin = t.localPosition;
        await TweenAsync(duration, Ease.Linear, v => {
            if (t == null) return;
            float decay = 1f - v;
            float wave = Mathf.Sin(v * vibrato * Mathf.PI) * decay;
            t.localPosition = origin + punch * wave * elasticity;
        }, token, () => { if (t != null) t.localPosition = origin; onComplete?.Invoke(); }, delay);
    }

    /// <summary>
    /// Punch rotation: quick rotational jolt that settles back.
    /// </summary>
    public static async Task DoPunchRotation(this Transform t, Vector3 punch, float duration = 0.4f,
        int vibrato = 6, float elasticity = 0.5f, CancellationToken token = default,
        float delay = 0f, Action onComplete = null)
    {
        Quaternion origin = t.localRotation;
        await TweenAsync(duration, Ease.Linear, v => {
            if (t == null) return;
            float decay = 1f - v;
            float wave = Mathf.Sin(v * vibrato * Mathf.PI) * decay;
            t.localRotation = origin * Quaternion.Euler(punch * wave * elasticity);
        }, token, () => { if (t != null) t.localRotation = origin; onComplete?.Invoke(); }, delay);
    }

    /// <summary>
    /// Punch scale: quick scale burst that settles back.
    /// </summary>
    public static async Task DoPunchScale(this Transform t, Vector3 punch, float duration = 0.4f,
        int vibrato = 6, float elasticity = 0.5f, CancellationToken token = default,
        float delay = 0f, Action onComplete = null)
    {
        Vector3 origin = t.localScale;
        await TweenAsync(duration, Ease.Linear, v => {
            if (t == null) return;
            float decay = 1f - v;
            float wave = Mathf.Sin(v * vibrato * Mathf.PI) * decay;
            t.localScale = origin + punch * wave * elasticity;
        }, token, () => { if (t != null) t.localScale = origin; onComplete?.Invoke(); }, delay);
    }

    /// <summary>
    /// Bounce: drops to a surface then bounces with decreasing height.
    /// </summary>
    public static async Task DoBounce(this Transform t, float bounceHeight = 0.5f, float duration = 0.6f,
        int bounces = 3, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Vector3 origin = t.localPosition;
        await TweenAsync(duration, Ease.Linear, v => {
            if (t == null) return;
            float decay = 1f - v;
            float bounce = Mathf.Abs(Mathf.Sin(v * bounces * Mathf.PI)) * decay * bounceHeight;
            var pos = origin;
            pos.y += bounce;
            t.localPosition = pos;
        }, token, () => { if (t != null) t.localPosition = origin; onComplete?.Invoke(); }, delay);
    }

    /// <summary>
    /// Shake position: random displacement that decays over time.
    /// Great for hit feedback, camera shake, etc.
    /// </summary>
    public static async Task DoShakePosition(this Transform t, float strength = 0.1f,
        float duration = 0.5f, int vibrato = 10, CancellationToken token = default,
        float delay = 0f, Action onComplete = null)
    {
        Vector3 origin = t.localPosition;
        // Pre-generate random directions to avoid per-frame randomness issues.
        Vector3[] randoms = new Vector3[vibrato];
        for (int i = 0; i < vibrato; i++)
            randoms[i] = UnityEngine.Random.insideUnitSphere;

        await TweenAsync(duration, Ease.Linear, v => {
            if (t == null) return;
            float decay = 1f - v;
            int idx = Mathf.Clamp(Mathf.FloorToInt(v * vibrato), 0, vibrato - 1);
            t.localPosition = origin + randoms[idx] * strength * decay;
        }, token, () => { if (t != null) t.localPosition = origin; onComplete?.Invoke(); }, delay);
    }

    /// <summary>
    /// Shake rotation: random rotational jitter that decays.
    /// </summary>
    public static async Task DoShakeRotation(this Transform t, float strength = 15f,
        float duration = 0.5f, int vibrato = 10, CancellationToken token = default,
        float delay = 0f, Action onComplete = null)
    {
        Quaternion origin = t.localRotation;
        Vector3[] randoms = new Vector3[vibrato];
        for (int i = 0; i < vibrato; i++)
            randoms[i] = UnityEngine.Random.insideUnitSphere;

        await TweenAsync(duration, Ease.Linear, v => {
            if (t == null) return;
            float decay = 1f - v;
            int idx = Mathf.Clamp(Mathf.FloorToInt(v * vibrato), 0, vibrato - 1);
            t.localRotation = origin * Quaternion.Euler(randoms[idx] * strength * decay);
        }, token, () => { if (t != null) t.localRotation = origin; onComplete?.Invoke(); }, delay);
    }

    /// <summary>
    /// Shake scale: random scale jitter that decays.
    /// </summary>
    public static async Task DoShakeScale(this Transform t, float strength = 0.2f,
        float duration = 0.5f, int vibrato = 10, CancellationToken token = default,
        float delay = 0f, Action onComplete = null)
    {
        Vector3 origin = t.localScale;
        float[] randoms = new float[vibrato];
        for (int i = 0; i < vibrato; i++)
            randoms[i] = UnityEngine.Random.Range(-1f, 1f);

        await TweenAsync(duration, Ease.Linear, v => {
            if (t == null) return;
            float decay = 1f - v;
            int idx = Mathf.Clamp(Mathf.FloorToInt(v * vibrato), 0, vibrato - 1);
            t.localScale = origin + Vector3.one * randoms[idx] * strength * decay;
        }, token, () => { if (t != null) t.localScale = origin; onComplete?.Invoke(); }, delay);
    }

    /// <summary>
    /// Wobble: continuous oscillating rotation. Good for idle animations.
    /// </summary>
    public static async Task DoWobble(this Transform t, Vector3 axis, float angle = 10f,
        float duration = 0.5f, int loopCount = 1, CancellationToken token = default,
        float delay = 0f, Action onComplete = null)
    {
        Quaternion origin = t.localRotation;
        await TweenAsync(duration, Ease.Linear, v => {
            if (t == null) return;
            float wave = Mathf.Sin(v * Mathf.PI * 2f) * angle;
            t.localRotation = origin * Quaternion.AngleAxis(wave, axis);
        }, token, () => { if (t != null) t.localRotation = origin; onComplete?.Invoke(); },
        delay, loopCount, LoopType.Restart);
    }

    /// <summary>
    /// Squash and stretch: classic cartoon scaling on Y and XZ.
    /// </summary>
    public static async Task DoSquashStretch(this Transform t, float intensity = 0.3f,
        float duration = 0.4f, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Vector3 origin = t.localScale;
        await TweenAsync(duration, Ease.Linear, v => {
            if (t == null) return;
            float wave = Mathf.Sin(v * Mathf.PI * 2f) * (1f - v) * intensity;
            t.localScale = new Vector3(
                origin.x * (1f - wave),
                origin.y * (1f + wave),
                origin.z * (1f - wave));
        }, token, () => { if (t != null) t.localScale = origin; onComplete?.Invoke(); }, delay);
    }

    // =========================================================================
    //  FADE (CanvasGroup / Material / SpriteRenderer)
    // =========================================================================

    /// <summary>Fade a CanvasGroup alpha. Best for UI panels.</summary>
    public static async Task DoFade(this CanvasGroup cg, float targetAlpha, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        float start = cg.alpha;
        await TweenAsync(duration, ease, v => {
            if (cg != null) cg.alpha = Mathf.LerpUnclamped(start, targetAlpha, v);
        }, token, onComplete, delay);
    }

    /// <summary>Fade a Material _Color alpha (works with Standard, URP Lit, etc.).</summary>
    public static async Task DoFade(this Material mat, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Color c = mat.color;
        float start = c.a;
        await TweenAsync(duration, ease, v => {
            if (mat != null)
            {
                c.a = Mathf.LerpUnclamped(start, target, v);
                mat.color = c;
            }
        }, token, onComplete, delay);
    }

    /// <summary>Fade a SpriteRenderer alpha.</summary>
    public static async Task DoFade(this SpriteRenderer sr, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        float start = sr.color.a;
        await TweenAsync(duration, ease, v => {
            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.LerpUnclamped(start, target, v);
                sr.color = c;
            }
        }, token, onComplete, delay);
    }

    /// <summary>Fade a Renderer's material instance alpha (safe, creates instance).</summary>
    public static async Task DoFade(this Renderer rend, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Material mat = rend.material; // creates instance
        Color c = mat.color;
        float start = c.a;
        await TweenAsync(duration, ease, v => {
            if (rend != null)
            {
                c.a = Mathf.LerpUnclamped(start, target, v);
                mat.color = c;
            }
        }, token, onComplete, delay);
    }

    // =========================================================================
    //  COLOR
    // =========================================================================

    /// <summary>Tween a Material color.</summary>
    public static async Task DoColor(this Material mat, Color target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Color start = mat.color;
        await TweenAsync(duration, ease, v => {
            if (mat != null) mat.color = Color.LerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    /// <summary>Tween a SpriteRenderer color.</summary>
    public static async Task DoColor(this SpriteRenderer sr, Color target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Color start = sr.color;
        await TweenAsync(duration, ease, v => {
            if (sr != null) sr.color = Color.LerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    // =========================================================================
    //  FLOAT / VALUE TWEENS
    // =========================================================================

    /// <summary>Tween an arbitrary float value. Use the callback to apply it.</summary>
    public static async Task DoFloat(float from, float to, float duration, Action<float> onUpdate,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        await TweenAsync(duration, ease, v => {
            onUpdate?.Invoke(Mathf.LerpUnclamped(from, to, v));
        }, token, onComplete, delay);
    }

    /// <summary>Tween an arbitrary Vector3 value.</summary>
    public static async Task DoVector3(Vector3 from, Vector3 to, float duration, Action<Vector3> onUpdate,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        await TweenAsync(duration, ease, v => {
            onUpdate?.Invoke(Vector3.LerpUnclamped(from, to, v));
        }, token, onComplete, delay);
    }

    /// <summary>Tween an arbitrary Color value.</summary>
    public static async Task DoColorValue(Color from, Color to, float duration, Action<Color> onUpdate,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        await TweenAsync(duration, ease, v => {
            onUpdate?.Invoke(Color.LerpUnclamped(from, to, v));
        }, token, onComplete, delay);
    }

    // =========================================================================
    //  PATH / BEZIER
    // =========================================================================

    /// <summary>
    /// Move along a set of waypoints using Catmull-Rom interpolation.
    /// </summary>
    public static async Task DoPath(this Transform t, Vector3[] waypoints, float duration,
        Ease ease = Ease.InOutQuad, CancellationToken token = default, float delay = 0f,
        bool closedLoop = false, Action onComplete = null)
    {
        if (waypoints == null || waypoints.Length < 2) return;

        // Prepend current position as first point.
        var pts = new List<Vector3> { t.position };
        pts.AddRange(waypoints);
        if (closedLoop) pts.Add(pts[0]);

        int segments = pts.Count - 1;

        await TweenAsync(duration, ease, v => {
            if (t == null) return;
            float scaled = v * segments;
            int seg = Mathf.Min(Mathf.FloorToInt(scaled), segments - 1);
            float segT = scaled - seg;

            // Catmull-Rom requires 4 points: p0, p1, p2, p3
            Vector3 p0 = pts[Mathf.Max(seg - 1, 0)];
            Vector3 p1 = pts[seg];
            Vector3 p2 = pts[Mathf.Min(seg + 1, pts.Count - 1)];
            Vector3 p3 = pts[Mathf.Min(seg + 2, pts.Count - 1)];

            t.position = CatmullRom(p0, p1, p2, p3, segT);
        }, token, onComplete, delay);
    }

    /// <summary>
    /// Move along a quadratic bezier curve (start -> control -> end).
    /// </summary>
    public static async Task DoBezier(this Transform t, Vector3 controlPoint, Vector3 endPoint,
        float duration, Ease ease = Ease.InOutQuad, CancellationToken token = default,
        float delay = 0f, Action onComplete = null)
    {
        Vector3 start = t.position;
        await TweenAsync(duration, ease, v => {
            if (t == null) return;
            // Quadratic bezier: B(t) = (1-t)^2*P0 + 2*(1-t)*t*P1 + t^2*P2
            float u = 1f - v;
            t.position = u * u * start + 2f * u * v * controlPoint + v * v * endPoint;
        }, token, onComplete, delay);
    }

    /// <summary>
    /// Move along a cubic bezier curve.
    /// </summary>
    public static async Task DoCubicBezier(this Transform t, Vector3 cp1, Vector3 cp2,
        Vector3 endPoint, float duration, Ease ease = Ease.InOutQuad,
        CancellationToken token = default, float delay = 0f, Action onComplete = null)
    {
        Vector3 start = t.position;
        await TweenAsync(duration, ease, v => {
            if (t == null) return;
            float u = 1f - v;
            t.position = u * u * u * start + 3f * u * u * v * cp1 + 3f * u * v * v * cp2 + v * v * v * endPoint;
        }, token, onComplete, delay);
    }

    // =========================================================================
    //  LOOK AT / AIM
    // =========================================================================

    /// <summary>Smoothly rotate to look at a world-space target point.</summary>
    public static async Task DoLookAt(this Transform t, Vector3 target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Vector3? upAxis = null, Action onComplete = null)
    {
        Quaternion start = t.rotation;
        Vector3 up = upAxis ?? Vector3.up;
        Vector3 dir = (target - t.position).normalized;
        if (dir == Vector3.zero) return;
        Quaternion end = Quaternion.LookRotation(dir, up);

        await TweenAsync(duration, ease, v => {
            if (t != null) t.rotation = Quaternion.SlerpUnclamped(start, end, v);
        }, token, onComplete, delay);
    }

    // =========================================================================
    //  TRANSFORM CONTAINER (your existing type)
    // =========================================================================

    /// <summary>
    /// Tween position + rotation + scale to a TransformContainer simultaneously.
    /// Upgraded version of your existing MoveToPositionAsync with easing support.
    /// </summary>
    public static async Task DoTransform(this Transform t, TransformContainer target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Vector3 startPos = t.position;
        Quaternion startRot = t.rotation;
        Vector3 startScale = t.localScale;

        await TweenAsync(duration, ease, v => {
            if (t == null) return;
            t.SetPositionAndRotation(
                Vector3.LerpUnclamped(startPos, target.Position, v),
                Quaternion.SlerpUnclamped(startRot, target.Rotation, v));
            t.localScale = Vector3.LerpUnclamped(startScale, target.localScale, v);
        }, token, onComplete, delay);
    }

    // =========================================================================
    //  AUDIO
    // =========================================================================

    /// <summary>Fade AudioSource volume.</summary>
    public static async Task DoFade(this AudioSource src, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        float start = src.volume;
        await TweenAsync(duration, ease, v => {
            if (src != null) src.volume = Mathf.LerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    /// <summary>Fade AudioSource pitch.</summary>
    public static async Task DoPitch(this AudioSource src, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        float start = src.pitch;
        await TweenAsync(duration, ease, v => {
            if (src != null) src.pitch = Mathf.LerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    // =========================================================================
    //  CAMERA
    // =========================================================================

    /// <summary>Tween camera field of view.</summary>
    public static async Task DoFOV(this Camera cam, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        float start = cam.fieldOfView;
        await TweenAsync(duration, ease, v => {
            if (cam != null) cam.fieldOfView = Mathf.LerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    /// <summary>Tween camera orthographic size.</summary>
    public static async Task DoOrthoSize(this Camera cam, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        float start = cam.orthographicSize;
        await TweenAsync(duration, ease, v => {
            if (cam != null) cam.orthographicSize = Mathf.LerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    // =========================================================================
    //  LIGHT
    // =========================================================================

    /// <summary>Tween light intensity.</summary>
    public static async Task DoIntensity(this Light light, float target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        float start = light.intensity;
        await TweenAsync(duration, ease, v => {
            if (light != null) light.intensity = Mathf.LerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    /// <summary>Tween light color.</summary>
    public static async Task DoColor(this Light light, Color target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Color start = light.color;
        await TweenAsync(duration, ease, v => {
            if (light != null) light.color = Color.LerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    // =========================================================================
    //  RECTRANSFORM (UI)
    // =========================================================================

    /// <summary>Tween RectTransform anchoredPosition.</summary>
    public static async Task DoAnchorPos(this RectTransform rt, Vector2 target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Vector2 start = rt.anchoredPosition;
        await TweenAsync(duration, ease, v => {
            if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    /// <summary>Tween RectTransform sizeDelta.</summary>
    public static async Task DoSizeDelta(this RectTransform rt, Vector2 target, float duration,
        Ease ease = Ease.OutQuad, CancellationToken token = default, float delay = 0f,
        Action onComplete = null)
    {
        Vector2 start = rt.sizeDelta;
        await TweenAsync(duration, ease, v => {
            if (rt != null) rt.sizeDelta = Vector2.LerpUnclamped(start, target, v);
        }, token, onComplete, delay);
    }

    // =========================================================================
    //  SEQUENCE BUILDER
    // =========================================================================

    /// <summary>
    /// Run multiple tweens one after another.
    /// Usage: await TweenSequence.Create()
    ///            .Append(() => transform.DoMove(...))
    ///            .AppendInterval(0.5f)
    ///            .Append(() => transform.DoScale(...))
    ///            .Play(token);
    /// </summary>
    public class TweenSequence
    {
        private readonly List<Func<CancellationToken, Task>> _steps = new();

        public static TweenSequence Create() => new TweenSequence();

        public TweenSequence Append(Func<Task> tween)
        {
            _steps.Add(_ => tween());
            return this;
        }

        public TweenSequence Append(Func<CancellationToken, Task> tween)
        {
            _steps.Add(tween);
            return this;
        }

        public TweenSequence AppendInterval(float seconds)
        {
            _steps.Add(async token => {
                float waited = 0f;
                while (waited < seconds)
                {
                    if (token.IsCancellationRequested) return;
                    await Task.Yield();
                    waited += Time.deltaTime;
                }
            });
            return this;
        }

        public TweenSequence AppendCallback(Action callback)
        {
            _steps.Add(_ => { callback(); return Task.CompletedTask; });
            return this;
        }

        public async Task Play(CancellationToken token = default)
        {
            foreach (var step in _steps)
            {
                if (token.IsCancellationRequested) return;
                await step(token);
            }
        }
    }

    /// <summary>
    /// Run multiple tweens simultaneously and wait for all to finish.
    /// Usage: await TweenParallel.All(
    ///            transform.DoMove(...),
    ///            transform.DoScale(...),
    ///            canvasGroup.DoFade(...)
    ///        );
    /// </summary>
    public static class TweenParallel
    {
        public static Task All(params Task[] tweens) => Task.WhenAll(tweens);
    }

    // =========================================================================
    //  CATMULL-ROM HELPER
    // =========================================================================

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // =========================================================================
    //  EASING FUNCTIONS  (30 curves)
    // =========================================================================

    public static float EaseEvaluate(float t, Ease ease)
    {
        switch (ease)
        {
            // --- Linear ---
            case Ease.Linear: return t;

            // --- Quad ---
            case Ease.InQuad: return t * t;
            case Ease.OutQuad: return t * (2f - t);
            case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

            // --- Cubic ---
            case Ease.InCubic: return t * t * t;
            case Ease.OutCubic: { float u = t - 1f; return u * u * u + 1f; }
            case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : (t - 1f) * (2f * t - 2f) * (2f * t - 2f) + 1f;

            // --- Quart ---
            case Ease.InQuart: return t * t * t * t;
            case Ease.OutQuart: { float u = t - 1f; return 1f - u * u * u * u; }
            case Ease.InOutQuart: { float u = t - 1f; return t < 0.5f ? 8f * t * t * t * t : 1f - 8f * u * u * u * u; }

            // --- Quint ---
            case Ease.InQuint: return t * t * t * t * t;
            case Ease.OutQuint: { float u = t - 1f; return 1f + u * u * u * u * u; }
            case Ease.InOutQuint: { float u = t - 1f; return t < 0.5f ? 16f * t * t * t * t * t : 1f + 16f * u * u * u * u * u; }

            // --- Sine ---
            case Ease.InSine: return 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
            case Ease.OutSine: return Mathf.Sin(t * Mathf.PI * 0.5f);
            case Ease.InOutSine: return 0.5f * (1f - Mathf.Cos(Mathf.PI * t));

            // --- Expo ---
            case Ease.InExpo: return t == 0f ? 0f : Mathf.Pow(2f, 10f * (t - 1f));
            case Ease.OutExpo: return t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
            case Ease.InOutExpo:
                if (t == 0f) return 0f;
                if (t == 1f) return 1f;
                return t < 0.5f ? 0.5f * Mathf.Pow(2f, 20f * t - 10f) : 1f - 0.5f * Mathf.Pow(2f, -20f * t + 10f);

            // --- Circ ---
            case Ease.InCirc: return 1f - Mathf.Sqrt(1f - t * t);
            case Ease.OutCirc: { float u = t - 1f; return Mathf.Sqrt(1f - u * u); }
            case Ease.InOutCirc: return t < 0.5f ? 0.5f * (1f - Mathf.Sqrt(1f - 4f * t * t)) : 0.5f * (Mathf.Sqrt(1f - (2f * t - 2f) * (2f * t - 2f)) + 1f);

            // --- Back ---
            case Ease.InBack: { const float s = 1.70158f; return t * t * ((s + 1f) * t - s); }
            case Ease.OutBack: { const float s = 1.70158f; float u = t - 1f; return u * u * ((s + 1f) * u + s) + 1f; }
            case Ease.InOutBack:
                {
                    const float s = 1.70158f * 1.525f;
                    float u = t * 2f;
                    if (u < 1f) return 0.5f * (u * u * ((s + 1f) * u - s));
                    u -= 2f;
                    return 0.5f * (u * u * ((s + 1f) * u + s) + 2f);
                }

            // --- Elastic ---
            case Ease.InElastic:
                if (t == 0f || t == 1f) return t;
                return -Mathf.Pow(2f, 10f * (t - 1f)) * Mathf.Sin((t - 1.1f) * 5f * Mathf.PI);
            case Ease.OutElastic:
                if (t == 0f || t == 1f) return t;
                return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - 0.1f) * 5f * Mathf.PI) + 1f;
            case Ease.InOutElastic:
                if (t == 0f || t == 1f) return t;
                return t < 0.5f
                    ? -0.5f * Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * Mathf.PI / 4.5f)
                    : 0.5f * Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * Mathf.PI / 4.5f) + 1f;

            // --- Bounce ---
            case Ease.InBounce: return 1f - BounceOut(1f - t);
            case Ease.OutBounce: return BounceOut(t);
            case Ease.InOutBounce: return t < 0.5f ? 0.5f * (1f - BounceOut(1f - 2f * t)) : 0.5f * BounceOut(2f * t - 1f) + 0.5f;

            default: return t;
        }
    }

    private static float BounceOut(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
        if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
        t -= 2.625f / d1; return n1 * t * t + 0.984375f;
    }
}

// =============================================================================
//  EASE ENUM
// =============================================================================

public enum Ease
{
    Linear,
    InQuad, OutQuad, InOutQuad,
    InCubic, OutCubic, InOutCubic,
    InQuart, OutQuart, InOutQuart,
    InQuint, OutQuint, InOutQuint,
    InSine, OutSine, InOutSine,
    InExpo, OutExpo, InOutExpo,
    InCirc, OutCirc, InOutCirc,
    InBack, OutBack, InOutBack,
    InElastic, OutElastic, InOutElastic,
    InBounce, OutBounce, InOutBounce
}

public enum LoopType
{
    Restart,  // Replay from the beginning each loop
    Yoyo      // Reverse direction on odd loops
}

// =============================================================================
//  YOUR EXISTING CLASSES (unchanged)
// =============================================================================

public class MyTransform
{
    private TransformContainer ThisTransform = new TransformContainer();

    public MyTransform(Transform tf)
    {
        ThisTransform.Position = tf.position;
        ThisTransform.Rotation = tf.rotation;
        ThisTransform.localScale = tf.localScale;
    }

    public TransformContainer GetThisTransform()
    {
        return ThisTransform;
    }
}

public class TransformContainer
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 localScale;
}