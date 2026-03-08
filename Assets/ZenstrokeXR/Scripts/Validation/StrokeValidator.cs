using System.Collections.Generic;
using UnityEngine;
using ZenstrokeXR.Lessons;

namespace ZenstrokeXR.Validation
{
    public class StrokeValidator : MonoBehaviour
    {
        [Header("Validation Parameters")]
        [SerializeField] private int resamplePointCount = 32;
        [SerializeField] private float passingThreshold = 0.15f;
        [SerializeField] private float directionWeight = 0.3f;
        [SerializeField] private float minStrokeLength = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        // Cached lists to avoid allocation
        private readonly List<Vector2> resampledA = new List<Vector2>(64);
        private readonly List<Vector2> resampledB = new List<Vector2>(64);
        private readonly List<Vector2> normalizedA = new List<Vector2>(64);
        private readonly List<Vector2> normalizedB = new List<Vector2>(64);

        /// <summary>
        /// Validates a drawn stroke against a template stroke.
        /// Both inputs are in normalized [0,1] paper coordinates.
        /// </summary>
        public bool ValidateStroke(List<Vector2> drawnPoints, List<Vector2> templatePoints)
        {
            return ValidateStroke(drawnPoints, templatePoints, StrokeEndingType.Tome, null);
        }

        /// <summary>
        /// Validates a drawn stroke with optional ending evaluation.
        /// </summary>
        public bool ValidateStroke(List<Vector2> drawnPoints, List<Vector2> templatePoints,
            StrokeEndingType expectedEnding, List<float> pressures)
        {
            if (drawnPoints == null || drawnPoints.Count < 2)
            {
                Log("Rejected: too few drawn points");
                return false;
            }
            if (templatePoints == null || templatePoints.Count < 2)
            {
                Log("Rejected: too few template points");
                return false;
            }

            float drawnLength = ComputePathLength(drawnPoints);
            if (drawnLength < minStrokeLength)
            {
                Log($"Rejected: drawn path too short ({drawnLength:F3} < {minStrokeLength:F3})");
                return false;
            }

            // Resample both to equal point count
            Resample(drawnPoints, resamplePointCount, resampledA);
            Resample(templatePoints, resamplePointCount, resampledB);

            // Compute direction similarity before bounding-box normalization
            float dirSim = ComputeDirectionSimilarity(resampledA, resampledB);

            // Bounding-box normalize both
            if (!NormalizeToBoundingBox(resampledA, normalizedA))
            {
                Log("Rejected: drawn stroke is degenerate after normalization");
                return false;
            }
            if (!NormalizeToBoundingBox(resampledB, normalizedB))
            {
                Log("Rejected: template stroke is degenerate after normalization");
                return false;
            }

            // Mean pointwise distance
            float dist = ComputeMeanPointwiseDistance(normalizedA, normalizedB);

            // Combine scores
            float dirPenalty = (1f - dirSim) * 0.5f; // Map [-1,1] to [1,0] then halve
            float finalScore = dist * (1f - directionWeight) + dirPenalty * directionWeight;

            // Evaluate stroke ending (additive adjustment, clamped)
            float endingAdj = EvaluateEnding(drawnPoints, expectedEnding, pressures);
            finalScore += endingAdj;

            bool passed = finalScore < passingThreshold;

            Log($"Validation: dist={dist:F3}, dirSim={dirSim:F3}, dirPenalty={dirPenalty:F3}, " +
                $"endingAdj={endingAdj:F3}, final={finalScore:F3}, threshold={passingThreshold:F3}, " +
                $"ending={expectedEnding}, result={(passed ? "PASS" : "FAIL")}");

            return passed;
        }

        /// <summary>
        /// Evaluates how well the user performed the expected stroke ending.
        /// Analyzes the last 20% of the stroke for velocity and pressure patterns.
        /// Returns an additive score adjustment clamped to [-0.03, +0.02].
        /// Negative = bonus (good ending), Positive = penalty (wrong ending).
        /// </summary>
        private float EvaluateEnding(List<Vector2> points, StrokeEndingType expected, List<float> pressures)
        {
            if (points == null || points.Count < 5) return 0f;

            int totalCount = points.Count;
            int tailStart = Mathf.Max(0, totalCount - Mathf.CeilToInt(totalCount * 0.2f));
            int tailCount = totalCount - tailStart;
            if (tailCount < 2) return 0f;

            // Compute velocity in the tail region
            float tailVelocitySum = 0f;
            float earlyVelocitySum = 0f;
            int earlyCount = Mathf.Max(1, tailStart);

            for (int i = 1; i < tailStart && i < totalCount; i++)
                earlyVelocitySum += Vector2.Distance(points[i], points[i - 1]);
            float avgEarlyVelocity = earlyCount > 1 ? earlyVelocitySum / (earlyCount - 1) : 0.01f;

            for (int i = tailStart + 1; i < totalCount; i++)
                tailVelocitySum += Vector2.Distance(points[i], points[i - 1]);
            float avgTailVelocity = tailCount > 1 ? tailVelocitySum / (tailCount - 1) : 0f;

            float velocityRatio = avgEarlyVelocity > 0.0001f ? avgTailVelocity / avgEarlyVelocity : 1f;

            // Check for direction change at end (for hane detection)
            bool hasDirectionChange = false;
            if (tailCount >= 3)
            {
                Vector2 dirBefore = (points[tailStart + 1] - points[tailStart]).normalized;
                Vector2 dirEnd = (points[totalCount - 1] - points[totalCount - 2]).normalized;
                float dot = Vector2.Dot(dirBefore, dirEnd);
                hasDirectionChange = dot < 0.7f; // More than ~45 degree change
            }

            // Pressure analysis (if available)
            bool hasPressure = pressures != null && pressures.Count >= totalCount;
            float pressureDrop = 0f;
            float pressureMaintained = false ? 0f : 1f; // default
            if (hasPressure)
            {
                int pTailStart = Mathf.Max(0, pressures.Count - Mathf.CeilToInt(pressures.Count * 0.2f));
                float avgEarlyPressure = 0f;
                for (int i = 0; i < pTailStart; i++)
                    avgEarlyPressure += pressures[i];
                avgEarlyPressure = pTailStart > 0 ? avgEarlyPressure / pTailStart : 1f;

                float avgTailPressure = 0f;
                for (int i = pTailStart; i < pressures.Count; i++)
                    avgTailPressure += pressures[i];
                avgTailPressure = (pressures.Count - pTailStart) > 0
                    ? avgTailPressure / (pressures.Count - pTailStart) : 1f;

                pressureDrop = avgEarlyPressure - avgTailPressure;
                pressureMaintained = Mathf.Abs(pressureDrop) < 0.15f ? 1f : 0f;
            }

            float adj = 0f;
            switch (expected)
            {
                case StrokeEndingType.Tome:
                    // Good: velocity slows, pressure maintained
                    if (velocityRatio < 0.6f) adj -= 0.015f; // Bonus for slowing
                    if (hasPressure && pressureMaintained > 0.5f) adj -= 0.015f; // Bonus for firm stop
                    break;

                case StrokeEndingType.Hane:
                    // Good: direction change at end, pressure drops
                    if (hasDirectionChange) adj -= 0.02f; // Bonus for flick
                    if (hasPressure && pressureDrop > 0.2f) adj -= 0.01f; // Bonus for pressure release
                    // Velocity-only fallback (mouse)
                    if (!hasPressure && velocityRatio > 1.2f) adj -= 0.01f; // Speed up at end = flick
                    break;

                case StrokeEndingType.Harai:
                    // Good: gradual pressure decrease
                    if (hasPressure && pressureDrop > 0.3f) adj -= 0.02f; // Bonus for fade
                    if (velocityRatio > 0.8f && velocityRatio < 1.5f) adj -= 0.01f; // Smooth finish
                    // Velocity-only fallback (mouse)
                    if (!hasPressure && velocityRatio > 0.9f) adj -= 0.01f; // Maintained speed = sweep
                    break;
            }

            return Mathf.Clamp(adj, -0.03f, 0.02f);
        }

        /// <summary>
        /// Resamples a polyline to exactly targetCount equidistant points.
        /// Result is written into the output list (cleared first).
        /// </summary>
        private void Resample(List<Vector2> points, int targetCount, List<Vector2> output)
        {
            output.Clear();
            if (points.Count == 0) return;
            if (points.Count == 1)
            {
                for (int i = 0; i < targetCount; i++)
                    output.Add(points[0]);
                return;
            }

            float totalLength = ComputePathLength(points);
            if (totalLength < 0.0001f)
            {
                for (int i = 0; i < targetCount; i++)
                    output.Add(points[0]);
                return;
            }

            float interval = totalLength / (targetCount - 1);
            output.Add(points[0]);
            float accumulated = 0f;
            Vector2 prevPoint = points[0];
            int srcIndex = 1;

            while (output.Count < targetCount && srcIndex < points.Count)
            {
                float segLen = Vector2.Distance(prevPoint, points[srcIndex]);

                if (accumulated + segLen >= interval && interval > 0f)
                {
                    float t = (interval - accumulated) / segLen;
                    t = Mathf.Clamp01(t);
                    Vector2 newPoint = Vector2.Lerp(prevPoint, points[srcIndex], t);
                    output.Add(newPoint);
                    accumulated = 0f;
                    prevPoint = newPoint;
                }
                else
                {
                    accumulated += segLen;
                    prevPoint = points[srcIndex];
                    srcIndex++;
                }
            }

            // Pad with last point if needed
            while (output.Count < targetCount)
                output.Add(points[points.Count - 1]);
        }

        private bool NormalizeToBoundingBox(List<Vector2> points, List<Vector2> output)
        {
            output.Clear();
            if (points.Count == 0) return false;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].x < minX) minX = points[i].x;
                if (points[i].x > maxX) maxX = points[i].x;
                if (points[i].y < minY) minY = points[i].y;
                if (points[i].y > maxY) maxY = points[i].y;
            }

            float rangeX = maxX - minX;
            float rangeY = maxY - minY;

            // For straight lines (horizontal or vertical), use the larger range for both
            float range = Mathf.Max(rangeX, rangeY);
            if (range < 0.001f) return false; // Degenerate (point)

            // Use max range to preserve aspect ratio
            float centerX = (minX + maxX) * 0.5f;
            float centerY = (minY + maxY) * 0.5f;

            for (int i = 0; i < points.Count; i++)
            {
                output.Add(new Vector2(
                    (points[i].x - centerX) / range + 0.5f,
                    (points[i].y - centerY) / range + 0.5f
                ));
            }

            return true;
        }

        private float ComputeMeanPointwiseDistance(List<Vector2> a, List<Vector2> b)
        {
            int count = Mathf.Min(a.Count, b.Count);
            if (count == 0) return float.MaxValue;

            float sum = 0f;
            for (int i = 0; i < count; i++)
                sum += Vector2.Distance(a[i], b[i]);

            return sum / count;
        }

        private float ComputeDirectionSimilarity(List<Vector2> a, List<Vector2> b)
        {
            int count = Mathf.Min(a.Count, b.Count) - 1;
            if (count <= 0) return 0f;

            float sum = 0f;
            int validCount = 0;

            for (int i = 0; i < count; i++)
            {
                Vector2 dirA = (a[i + 1] - a[i]);
                Vector2 dirB = (b[i + 1] - b[i]);

                if (dirA.sqrMagnitude < 0.00001f || dirB.sqrMagnitude < 0.00001f)
                    continue;

                sum += Vector2.Dot(dirA.normalized, dirB.normalized);
                validCount++;
            }

            return validCount > 0 ? sum / validCount : 0f;
        }

        private float ComputePathLength(List<Vector2> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
                length += Vector2.Distance(points[i - 1], points[i]);
            return length;
        }

        private void Log(string msg)
        {
            if (enableDebugLogs)
                Debug.Log($"[StrokeValidator] {msg}");
        }
    }
}
