using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace XenoDirector
{
    public static class IdeologyXenotypeMatcher
    {
        private static readonly string[] HateTerms =
        {
            "hate", "hated", "hatred", "abhorrent", "despised", "detested",
            "aversion", "dislike", "disfavor", "unfavor", "reject", "loathe",
            "repuls", "xenophob", "unacceptable", "undesirable", "inferior",
            "impure", "forbidden", "revile", "contempt"
        };

        private static readonly string[] RelevantMemberTerms =
        {
            "xeno", "precept", "comp", "hate", "target", "subject", "def", "name"
        };

        public static bool IsHatedByAnyIdeo(IEnumerable<object> ideos, IEnumerable<string> xenotypeNames)
        {
            if (ideos == null || xenotypeNames == null) return false;

            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in xenotypeNames)
                if (!string.IsNullOrWhiteSpace(name))
                    targets.Add(name.Trim());
            if (targets.Count == 0) return false;

            foreach (var ideo in ideos)
            {
                if (ideo == null) continue;
                foreach (var precept in EnumerateMember(ideo, "PreceptsListForReading", "precepts", "Precepts"))
                {
                    if (precept == null || !DescribesXenotypeHatred(precept)) continue;
                    if (ContainsTargetXenotype(precept, targets)) return true;
                }
            }

            return false;
        }

        public static IEnumerable<object> GetIdeologies(object factionIdeologyTracker)
        {
            if (factionIdeologyTracker == null) yield break;

            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (var ideology in EnumerateMember(factionIdeologyTracker, "AllIdeos"))
            {
                if (ideology != null && seen.Add(ideology))
                    yield return ideology;
            }

            foreach (var ideology in EnumerateMember(factionIdeologyTracker, "PrimaryIdeo"))
                if (ideology != null && seen.Add(ideology))
                    yield return ideology;
        }

        public static IEnumerable<string> GetPawnXenotypeNames(Pawn pawn)
        {
            if (pawn?.genes == null) yield break;

            foreach (var xenotype in EnumerateMember(pawn.genes, "Xenotype", "CustomXenotype"))
            {
                if (xenotype == null) continue;
                foreach (var name in EnumerateMember(xenotype, "defName", "name", "label", "LabelCap"))
                    if (name is string text && !string.IsNullOrWhiteSpace(text))
                        yield return text;
            }
        }

        private static bool DescribesXenotypeHatred(object precept)
        {
            bool mentionsXenotype = false;
            bool mentionsHatred = false;
            foreach (var value in EnumerateGraph(precept, 4, 64))
            {
                string descriptor = value is string text ? text : value.GetType().Name;
                descriptor = Normalize(descriptor);
                mentionsXenotype |= descriptor.Contains("xeno");

                foreach (var term in HateTerms)
                    if (descriptor.Contains(term))
                        mentionsHatred = true;
            }

            return mentionsXenotype && mentionsHatred;
        }

        private static bool ContainsTargetXenotype(object root, HashSet<string> targets)
        {
            foreach (var value in EnumerateGraph(root, 4, 128))
            {
                if (value is string text && MatchesTarget(text, targets))
                    return true;

                if (value.GetType().Name.IndexOf("Xenotype", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foreach (var name in EnumerateMember(value, "defName", "name", "label", "LabelCap"))
                        if (name is string xenotypeName && MatchesTarget(xenotypeName, targets))
                            return true;
                }
            }

            return false;
        }

        private static IEnumerable<object> EnumerateGraph(object root, int maxDepth, int maxItems)
        {
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var pending = new Queue<(object Value, int Depth)>();
            pending.Enqueue((root, 0));
            int inspected = 0;

            while (pending.Count > 0 && inspected++ < maxItems)
            {
                var (value, depth) = pending.Dequeue();
                if (value == null) continue;
                yield return value;
                if (depth >= maxDepth || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum)
                    continue;
                if (!value.GetType().IsValueType && !seen.Add(value))
                    continue;

                if (value is IEnumerable sequence)
                {
                    int count = 0;
                    foreach (var item in sequence)
                    {
                        if (item != null) pending.Enqueue((item, depth + 1));
                        if (++count >= 32) break;
                    }
                    continue;
                }

                foreach (var memberValue in EnumerateRelevantMembers(value))
                    if (memberValue != null)
                        pending.Enqueue((memberValue, depth + 1));
            }
        }

        private static IEnumerable<object> EnumerateRelevantMembers(object value)
        {
            for (Type type = value.GetType(); type != null; type = type.BaseType)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!IsRelevantMember(field.Name) || field.FieldType.IsPrimitive || field.FieldType.IsEnum) continue;
                    object fieldValue;
                    try { fieldValue = field.GetValue(value); }
                    catch (Exception exception)
                    {
                        Log.Warning($"[XenoDirector.Ideology] Could not inspect {type.FullName}.{field.Name}: {exception.Message}");
                        continue;
                    }
                    if (fieldValue != null) yield return fieldValue;
                }

                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!IsRelevantMember(property.Name) || property.GetIndexParameters().Length != 0) continue;
                    var getter = property.GetGetMethod(true);
                    if (getter == null || getter.IsStatic) continue;

                    object propertyValue;
                    try { propertyValue = getter.Invoke(value, null); }
                    catch (Exception exception)
                    {
                        Log.Warning($"[XenoDirector.Ideology] Could not inspect {type.FullName}.{property.Name}: {exception.Message}");
                        continue;
                    }
                    if (propertyValue != null) yield return propertyValue;
                }
            }
        }

        private static IEnumerable<object> EnumerateMember(object value, params string[] memberNames)
        {
            if (value == null) yield break;
            var requested = new HashSet<string>(memberNames, StringComparer.OrdinalIgnoreCase);
            for (Type type = value.GetType(); type != null; type = type.BaseType)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!requested.Contains(field.Name)) continue;
                    object fieldValue;
                    try { fieldValue = field.GetValue(value); }
                    catch (Exception exception)
                    {
                        Log.Warning($"[XenoDirector.Ideology] Could not inspect {type.FullName}.{field.Name}: {exception.Message}");
                        continue;
                    }
                    foreach (var item in Expand(fieldValue))
                        yield return item;
                }

                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!requested.Contains(property.Name) || property.GetIndexParameters().Length != 0) continue;
                    var getter = property.GetGetMethod(true);
                    if (getter == null || getter.IsStatic) continue;
                    object propertyValue;
                    try { propertyValue = getter.Invoke(value, null); }
                    catch (Exception exception)
                    {
                        Log.Warning($"[XenoDirector.Ideology] Could not inspect {type.FullName}.{property.Name}: {exception.Message}");
                        continue;
                    }
                    foreach (var item in Expand(propertyValue))
                        yield return item;
                }
            }
        }

        private static IEnumerable<object> Expand(object value)
        {
            if (value == null) yield break;
            if (value is string)
            {
                yield return value;
                yield break;
            }
            if (value is IEnumerable sequence)
            {
                int count = 0;
                foreach (var item in sequence)
                {
                    if (item != null) yield return item;
                    if (++count >= 64) yield break;
                }
                yield break;
            }
            yield return value;
        }

        private static bool IsRelevantMember(string name)
        {
            string normalized = Normalize(name);
            foreach (var term in RelevantMemberTerms)
                if (normalized.Contains(term))
                    return true;
            return false;
        }

        private static bool MatchesTarget(string value, HashSet<string> targets)
        {
            foreach (var target in targets)
                if (string.Equals(value, target, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var chars = new char[value.Length];
            int length = 0;
            foreach (char character in value)
                if (char.IsLetterOrDigit(character))
                    chars[length++] = char.ToLowerInvariant(character);
            return new string(chars, 0, length);
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object left, object right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
    }
}
