using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnapPose.Editor
{
    /// <summary>
    /// Stores a set of (source bone path → target bone path) pairs used for mirroring.
    /// Source is typically a left-side bone; target is the right-side counterpart, or vice versa.
    /// </summary>
    [Serializable]
    public class BoneMirrorMap
    {
        [Serializable]
        public class BonePair
        {
            public string source;
            public string target;
            public bool   enabled = true;

            public BonePair(string src, string tgt)
            {
                source = src;
                target = tgt;
            }
        }

        public List<BonePair> pairs = new List<BonePair>();

        public BoneMirrorMap Clone()
        {
            var copy = new BoneMirrorMap();
            foreach (var p in pairs)
                copy.pairs.Add(new BonePair(p.source, p.target) { enabled = p.enabled });
            return copy;
        }

        /// <summary>Returns source→target dict for enabled pairs only.</summary>
        public Dictionary<string, string> BuildLookup()
        {
            var dict = new Dictionary<string, string>();
            foreach (var p in pairs)
            {
                if (!p.enabled) continue;
                if (!string.IsNullOrEmpty(p.source) && !string.IsNullOrEmpty(p.target))
                    dict[p.source] = p.target;
            }
            return dict;
        }
    }

    /// <summary>
    /// Detects left/right bone pairs from a rig's transform hierarchy by
    /// matching common naming conventions (prefix/suffix L_/R_, _L/_R, Left/Right).
    /// </summary>
    public static class MirrorPairDetector
    {
        // Pattern table: (left pattern, right pattern, match type)
        // Match type: 0 = prefix, 1 = suffix, 2 = contains (case-insensitive)
        static readonly (string left, string right, int type)[] Patterns =
        {
            ("Left",  "Right",  2),
            ("left",  "right",  2),
            ("_L_",   "_R_",    2),
            ("_l_",   "_r_",    2),
            (".L.",   ".R.",    2),
            (".l.",   ".r.",    2),
            ("L_",    "R_",     0),
            ("l_",    "r_",     0),
            ("_L",    "_R",     1),
            ("_l",    "_r",     1),
        };

        /// <summary>
        /// Scans all bone paths of <paramref name="root"/> and returns detected pairs.
        /// Only the leaf bone name is matched; the full path is preserved in the pair.
        /// </summary>
        public static BoneMirrorMap Detect(GameObject root)
        {
            var map = new BoneMirrorMap();
            if (root == null) return map;

            var allPaths = PoseSampler.GetAllBonePaths(root);

            // Build a set for fast lookup
            var pathSet = new HashSet<string>(allPaths);
            var matched = new HashSet<string>(); // prevent duplicates

            foreach (var path in allPaths)
            {
                if (matched.Contains(path)) continue;

                // Extract leaf name
                string leafName = path.Contains("/") ? path.Substring(path.LastIndexOf('/') + 1) : path;
                string dir      = path.Contains("/") ? path.Substring(0, path.LastIndexOf('/') + 1) : "";

                // Try each pattern
                foreach (var (left, right, type) in Patterns)
                {
                    string mirrorLeaf = TrySwap(leafName, left, right, type);
                    if (mirrorLeaf == null) continue;

                    string mirrorPath = dir + mirrorLeaf;
                    if (!pathSet.Contains(mirrorPath)) continue;
                    if (matched.Contains(mirrorPath))   continue;

                    // Determine canonical order: left side is source
                    bool isLeft = ContainsPattern(leafName, left, type);
                    string src  = isLeft ? path       : mirrorPath;
                    string tgt  = isLeft ? mirrorPath : path;

                    map.pairs.Add(new BoneMirrorMap.BonePair(src, tgt));
                    matched.Add(path);
                    matched.Add(mirrorPath);
                    break; // first matching pattern wins
                }
            }

            return map;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Tries to swap the left pattern with right (or right with left) in <paramref name="name"/>.
        /// Returns the swapped string, or null if no match.
        /// </summary>
        static string TrySwap(string name, string left, string right, int type)
        {
            string result;

            // Try left → right
            result = Replace(name, left, right, type);
            if (result != null && result != name) return result;

            // Try right → left (mirror direction)
            result = Replace(name, right, left, type);
            if (result != null && result != name) return result;

            return null;
        }

        static string Replace(string name, string from, string to, int type)
        {
            switch (type)
            {
                case 0: // prefix
                    if (name.StartsWith(from, StringComparison.Ordinal))
                        return to + name.Substring(from.Length);
                    return null;

                case 1: // suffix
                    if (name.EndsWith(from, StringComparison.Ordinal))
                        return name.Substring(0, name.Length - from.Length) + to;
                    return null;

                case 2: // contains (first occurrence)
                    int idx = name.IndexOf(from, StringComparison.Ordinal);
                    if (idx >= 0)
                        return name.Substring(0, idx) + to + name.Substring(idx + from.Length);
                    return null;

                default: return null;
            }
        }

        static bool ContainsPattern(string name, string pattern, int type)
        {
            switch (type)
            {
                case 0: return name.StartsWith(pattern, StringComparison.Ordinal);
                case 1: return name.EndsWith(pattern, StringComparison.Ordinal);
                case 2: return name.IndexOf(pattern, StringComparison.Ordinal) >= 0;
                default: return false;
            }
        }
    }
}
