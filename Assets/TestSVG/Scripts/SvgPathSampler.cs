using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class SvgPathSampler
{
    private static readonly HashSet<char> CommandChars = new HashSet<char>
    {
        'M','m','L','l','H','h','V','v','C','c','Q','q','Z','z'
    };

    public static List<Vector2> SamplePath(string pathData, int curveSteps = 24)
    {
        var tokens = Tokenize(pathData);
        var points = new List<Vector2>();

        int i = 0;
        char cmd = ' ';
        Vector2 current = Vector2.zero;
        Vector2 subPathStart = Vector2.zero;

        while (i < tokens.Count)
        {
            if (IsCommand(tokens[i]))
            {
                cmd = tokens[i][0];
                i++;
            }
            else if (cmd == ' ')
            {
                Debug.LogWarning("SVG path parse error: missing command.");
                break;
            }

            switch (cmd)
            {
                case 'M':
                    {
                        float x = ReadFloat(tokens, ref i);
                        float y = ReadFloat(tokens, ref i);
                        current = new Vector2(x, y);
                        subPathStart = current;
                        AddPointIfNeeded(points, current);

                        // Subsequent pairs after M are treated as L
                        while (HasNumberAhead(tokens, i))
                        {
                            x = ReadFloat(tokens, ref i);
                            y = ReadFloat(tokens, ref i);
                            Vector2 next = new Vector2(x, y);
                            AddLine(points, current, next);
                            current = next;
                        }
                        break;
                    }

                case 'm':
                    {
                        float dx = ReadFloat(tokens, ref i);
                        float dy = ReadFloat(tokens, ref i);
                        current += new Vector2(dx, dy);
                        subPathStart = current;
                        AddPointIfNeeded(points, current);

                        while (HasNumberAhead(tokens, i))
                        {
                            dx = ReadFloat(tokens, ref i);
                            dy = ReadFloat(tokens, ref i);
                            Vector2 next = current + new Vector2(dx, dy);
                            AddLine(points, current, next);
                            current = next;
                        }
                        break;
                    }

                case 'L':
                    {
                        while (HasNumberAhead(tokens, i))
                        {
                            float x = ReadFloat(tokens, ref i);
                            float y = ReadFloat(tokens, ref i);
                            Vector2 next = new Vector2(x, y);
                            AddLine(points, current, next);
                            current = next;
                        }
                        break;
                    }

                case 'l':
                    {
                        while (HasNumberAhead(tokens, i))
                        {
                            float dx = ReadFloat(tokens, ref i);
                            float dy = ReadFloat(tokens, ref i);
                            Vector2 next = current + new Vector2(dx, dy);
                            AddLine(points, current, next);
                            current = next;
                        }
                        break;
                    }

                case 'H':
                    {
                        while (HasNumberAhead(tokens, i))
                        {
                            float x = ReadFloat(tokens, ref i);
                            Vector2 next = new Vector2(x, current.y);
                            AddLine(points, current, next);
                            current = next;
                        }
                        break;
                    }

                case 'h':
                    {
                        while (HasNumberAhead(tokens, i))
                        {
                            float dx = ReadFloat(tokens, ref i);
                            Vector2 next = new Vector2(current.x + dx, current.y);
                            AddLine(points, current, next);
                            current = next;
                        }
                        break;
                    }

                case 'V':
                    {
                        while (HasNumberAhead(tokens, i))
                        {
                            float y = ReadFloat(tokens, ref i);
                            Vector2 next = new Vector2(current.x, y);
                            AddLine(points, current, next);
                            current = next;
                        }
                        break;
                    }

                case 'v':
                    {
                        while (HasNumberAhead(tokens, i))
                        {
                            float dy = ReadFloat(tokens, ref i);
                            Vector2 next = new Vector2(current.x, current.y + dy);
                            AddLine(points, current, next);
                            current = next;
                        }
                        break;
                    }

                case 'C':
                    {
                        while (HasNumberAhead(tokens, i))
                        {
                            Vector2 p1 = new Vector2(ReadFloat(tokens, ref i), ReadFloat(tokens, ref i));
                            Vector2 p2 = new Vector2(ReadFloat(tokens, ref i), ReadFloat(tokens, ref i));
                            Vector2 p3 = new Vector2(ReadFloat(tokens, ref i), ReadFloat(tokens, ref i));
                            AddCubic(points, current, p1, p2, p3, curveSteps);
                            current = p3;
                        }
                        break;
                    }

                case 'c':
                    {
                        while (HasNumberAhead(tokens, i))
                        {
                            Vector2 p1 = current + new Vector2(ReadFloat(tokens, ref i), ReadFloat(tokens, ref i));
                            Vector2 p2 = current + new Vector2(ReadFloat(tokens, ref i), ReadFloat(tokens, ref i));
                            Vector2 p3 = current + new Vector2(ReadFloat(tokens, ref i), ReadFloat(tokens, ref i));
                            AddCubic(points, current, p1, p2, p3, curveSteps);
                            current = p3;
                        }
                        break;
                    }

                case 'Q':
                    {
                        while (HasNumberAhead(tokens, i))
                        {
                            Vector2 p1 = new Vector2(ReadFloat(tokens, ref i), ReadFloat(tokens, ref i));
                            Vector2 p2 = new Vector2(ReadFloat(tokens, ref i), ReadFloat(tokens, ref i));
                            AddQuadratic(points, current, p1, p2, curveSteps);
                            current = p2;
                        }
                        break;
                    }

                case 'q':
                    {
                        while (HasNumberAhead(tokens, i))
                        {
                            Vector2 p1 = current + new Vector2(ReadFloat(tokens, ref i), ReadFloat(tokens, ref i));
                            Vector2 p2 = current + new Vector2(ReadFloat(tokens, ref i), ReadFloat(tokens, ref i));
                            AddQuadratic(points, current, p1, p2, curveSteps);
                            current = p2;
                        }
                        break;
                    }

                case 'Z':
                case 'z':
                    {
                        AddLine(points, current, subPathStart);
                        current = subPathStart;
                        break;
                    }

                default:
                    {
                        Debug.LogWarning($"Unsupported SVG path command: {cmd}");
                        // Avoid infinite loop if unsupported command appears
                        while (i < tokens.Count && !IsCommand(tokens[i]))
                            i++;
                        break;
                    }
            }
        }

        return RemoveDuplicateNeighbors(points);
    }

    private static void AddLine(List<Vector2> points, Vector2 from, Vector2 to)
    {
        AddPointIfNeeded(points, from);
        AddPointIfNeeded(points, to);
    }

    private static void AddQuadratic(List<Vector2> points, Vector2 p0, Vector2 p1, Vector2 p2, int steps)
    {
        AddPointIfNeeded(points, p0);
        for (int s = 1; s <= steps; s++)
        {
            float t = s / (float)steps;
            float omt = 1f - t;
            Vector2 pt = omt * omt * p0 + 2f * omt * t * p1 + t * t * p2;
            AddPointIfNeeded(points, pt);
        }
    }

    private static void AddCubic(List<Vector2> points, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int steps)
    {
        AddPointIfNeeded(points, p0);
        for (int s = 1; s <= steps; s++)
        {
            float t = s / (float)steps;
            float omt = 1f - t;
            Vector2 pt =
                omt * omt * omt * p0 +
                3f * omt * omt * t * p1 +
                3f * omt * t * t * p2 +
                t * t * t * p3;

            AddPointIfNeeded(points, pt);
        }
    }

    private static void AddPointIfNeeded(List<Vector2> points, Vector2 p)
    {
        if (points.Count == 0 || Vector2.Distance(points[points.Count - 1], p) > 0.0001f)
            points.Add(p);
    }

    private static List<Vector2> RemoveDuplicateNeighbors(List<Vector2> input)
    {
        var output = new List<Vector2>();
        foreach (var p in input)
            AddPointIfNeeded(output, p);
        return output;
    }

    private static List<string> Tokenize(string d)
    {
        var tokens = new List<string>();
        int i = 0;

        while (i < d.Length)
        {
            char c = d[i];

            if (char.IsWhiteSpace(c) || c == ',')
            {
                i++;
                continue;
            }

            if (CommandChars.Contains(c))
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            // Number token
            int start = i;

            if (c == '+' || c == '-')
                i++;

            bool dotSeen = false;
            bool expSeen = false;

            while (i < d.Length)
            {
                char ch = d[i];

                if (char.IsDigit(ch))
                {
                    i++;
                    continue;
                }

                if (ch == '.' && !dotSeen)
                {
                    dotSeen = true;
                    i++;
                    continue;
                }

                if ((ch == 'e' || ch == 'E') && !expSeen)
                {
                    expSeen = true;
                    i++;
                    if (i < d.Length && (d[i] == '+' || d[i] == '-'))
                        i++;
                    continue;
                }

                // Important: "-10" right after "20" should start a new token
                if ((ch == '-' || ch == '+') && i > start)
                    break;

                break;
            }

            tokens.Add(d.Substring(start, i - start));
        }

        return tokens;
    }

    private static bool IsCommand(string token)
    {
        return token.Length == 1 && CommandChars.Contains(token[0]);
    }

    private static bool HasNumberAhead(List<string> tokens, int i)
    {
        return i < tokens.Count && !IsCommand(tokens[i]);
    }

    private static float ReadFloat(List<string> tokens, ref int i)
    {
        if (i >= tokens.Count)
            throw new Exception("Unexpected end of SVG path tokens.");

        float value = float.Parse(tokens[i], CultureInfo.InvariantCulture);
        i++;
        return value;
    }
}
