using UnityEngine;
using ZenstrokeXR.Lessons;

namespace ZenstrokeXR.Drawing
{
    /// <summary>
    /// Generates AnimationCurve width profiles for calligraphy stroke endings.
    /// Curves return values in [0,1] range — caller scales by desired width.
    /// </summary>
    public static class StrokeEndingCurves
    {
        /// <summary>
        /// Returns a width curve for template/animation strokes based on ending type.
        /// The curve spans [0,1] along the stroke length.
        /// </summary>
        public static AnimationCurve GetTemplateCurve(StrokeEndingType ending)
        {
            switch (ending)
            {
                case StrokeEndingType.Hane:
                    // Entry thickening, steady body, then sharp taper through the hook/flick
                    return new AnimationCurve(
                        new Keyframe(0.0f, 1.25f, 0f, -4f),   // 入り entry thickening
                        new Keyframe(0.06f, 1.0f, 0f, 0f),    // settle to body
                        new Keyframe(0.70f, 1.0f, 0f, 0f),    // consistent body
                        new Keyframe(0.80f, 0.75f, -2f, -2f),  // begin narrowing into hook
                        new Keyframe(0.90f, 0.35f, -3f, -3f),  // rapid taper through flick
                        new Keyframe(1.0f, 0.05f, -2f, 0f)    // fine point at tip
                    );

                case StrokeEndingType.Harai:
                    // Entry thickening, then long gradual taper to zero over last ~50%
                    return new AnimationCurve(
                        new Keyframe(0.0f, 1.25f, 0f, -4f),   // 入り entry thickening
                        new Keyframe(0.06f, 1.0f, 0f, 0f),    // settle to body
                        new Keyframe(0.45f, 1.0f, 0f, -0.3f),  // body holds, begin taper
                        new Keyframe(0.65f, 0.6f, -1.5f, -1.5f), // mid taper
                        new Keyframe(0.80f, 0.25f, -1.5f, -1.5f), // getting thin
                        new Keyframe(0.92f, 0.08f, -0.8f, -0.5f), // very thin
                        new Keyframe(1.0f, 0.0f, -0.3f, 0f)   // fades to zero
                    );

                case StrokeEndingType.Tome:
                default:
                    // Entry thickening, steady body, prominent press-down bulge at end
                    return new AnimationCurve(
                        new Keyframe(0.0f, 1.25f, 0f, -4f),   // 入り entry thickening
                        new Keyframe(0.06f, 1.0f, 0f, 0f),    // settle to body
                        new Keyframe(0.82f, 1.0f, 0f, 1f),    // consistent body
                        new Keyframe(0.92f, 1.35f, 0f, 0f),   // press-down bulge (firm stop)
                        new Keyframe(1.0f, 1.2f, -1f, 0f)     // stays wide — brush pressed down
                    );
            }
        }

        /// <summary>
        /// Applies an ending envelope to a user's ink width curve.
        /// Modulates the last 20% of the curve with the ending shape.
        /// </summary>
        /// <param name="inkCurve">The user's pressure-based width curve (modified in place).</param>
        /// <param name="ending">The expected ending type for this stroke.</param>
        /// <param name="baseWidth">The base stroke width for scaling.</param>
        /// <returns>A new curve with the ending envelope applied.</returns>
        public static AnimationCurve ApplyEndingToInkCurve(AnimationCurve inkCurve, StrokeEndingType ending, float baseWidth)
        {
            if (inkCurve == null || inkCurve.length == 0)
                return inkCurve;

            AnimationCurve endingCurve = GetEndingEnvelope(ending);
            AnimationCurve result = new AnimationCurve();

            // Sample and modulate the last 20% of the curve
            int sampleCount = 32;
            float envelopeStart = 0.8f;

            for (int i = 0; i <= sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float inkValue = inkCurve.Evaluate(t);

                if (t >= envelopeStart)
                {
                    // Map [0.8, 1.0] to [0, 1] for the envelope
                    float envT = (t - envelopeStart) / (1f - envelopeStart);
                    float envelope = endingCurve.Evaluate(envT);
                    inkValue *= envelope;
                }

                result.AddKey(new Keyframe(t, inkValue));
            }

            // Smooth the result
            for (int i = 0; i < result.length; i++)
            {
                result.SmoothTangents(i, 0.5f);
            }

            return result;
        }

        /// <summary>
        /// Returns an envelope curve for the ending portion only.
        /// Maps [0,1] where 0 = start of ending region, 1 = end of stroke.
        /// </summary>
        private static AnimationCurve GetEndingEnvelope(StrokeEndingType ending)
        {
            switch (ending)
            {
                case StrokeEndingType.Hane:
                    // Sharp taper through the hook/flick to fine point
                    return new AnimationCurve(
                        new Keyframe(0.0f, 1.0f, 0f, -1f),
                        new Keyframe(0.3f, 0.6f, -2f, -2f),
                        new Keyframe(0.6f, 0.25f, -1.5f, -1.5f),
                        new Keyframe(1.0f, 0.05f, -0.5f, 0f)
                    );

                case StrokeEndingType.Harai:
                    // Long smooth fade to zero
                    return new AnimationCurve(
                        new Keyframe(0.0f, 1.0f, 0f, -1.5f),
                        new Keyframe(0.4f, 0.4f, -1.5f, -1.5f),
                        new Keyframe(0.7f, 0.12f, -0.8f, -0.5f),
                        new Keyframe(1.0f, 0.0f, -0.3f, 0f)
                    );

                case StrokeEndingType.Tome:
                default:
                    // Press-down bulge (firm stop — brush pressed into paper)
                    return new AnimationCurve(
                        new Keyframe(0.0f, 1.0f, 0f, 0.8f),
                        new Keyframe(0.5f, 1.35f, 0f, 0f),
                        new Keyframe(1.0f, 1.2f, -0.5f, 0f)
                    );
            }
        }

        /// <summary>
        /// Scales all values in an AnimationCurve by a multiplier.
        /// Returns a new curve.
        /// </summary>
        public static AnimationCurve ScaleCurve(AnimationCurve curve, float scale)
        {
            if (curve == null) return null;

            Keyframe[] keys = curve.keys;
            Keyframe[] scaled = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                scaled[i] = new Keyframe(
                    keys[i].time,
                    keys[i].value * scale,
                    keys[i].inTangent * scale,
                    keys[i].outTangent * scale
                );
                scaled[i].weightedMode = keys[i].weightedMode;
                scaled[i].inWeight = keys[i].inWeight;
                scaled[i].outWeight = keys[i].outWeight;
            }
            return new AnimationCurve(scaled);
        }
    }
}
