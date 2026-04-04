using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace EJR.Game.Editor
{
    internal static class AsepriteTagDumpUtility
    {
        private const string KnightAssetPath = "Assets/Resources/Aseprite/Knight.aseprite";
        private const string WizardAssetPath = "Assets/Resources/Aseprite/Wizard.aseprite";
        private const string OutputPath = "Temp/AsepriteTagDump.txt";

        [MenuItem("Tools/EJR/Debug/Dump Knight Wizard Aseprite Tags")]
        public static void DumpKnightWizardTags()
        {
            var builder = new StringBuilder(512);
            AppendAssetDump(builder, KnightAssetPath);
            builder.AppendLine();
            AppendAssetDump(builder, WizardAssetPath);

            File.WriteAllText(OutputPath, builder.ToString(), Encoding.UTF8);
            Debug.Log($"Aseprite tag dump written to {OutputPath}\n{builder}");
            AssetDatabase.Refresh();
        }

        private static void AppendAssetDump(StringBuilder builder, string assetPath)
        {
            builder.AppendLine($"[{assetPath}]");

            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
            {
                builder.AppendLine("  importer: <missing>");
                return;
            }

            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath).OfType<Sprite>().ToArray();
            builder.AppendLine($"  sprites: {sprites.Length}");

            var tagsField = importer.GetType().GetField("m_Tags", BindingFlags.Instance | BindingFlags.NonPublic);
            if (tagsField?.GetValue(importer) is not IEnumerable tags)
            {
                builder.AppendLine("  tags: <unavailable>");
                return;
            }

            var tagCount = 0;
            foreach (var tag in tags)
            {
                if (tag == null)
                {
                    continue;
                }

                var tagType = tag.GetType();
                var name = ReadMember<string>(tagType, tag, "name") ?? "<unnamed>";
                var fromFrame = ReadMember<int>(tagType, tag, "fromFrame");
                var toFrame = ReadMember<int>(tagType, tag, "toFrame");
                var repeats = ReadMember<int>(tagType, tag, "noOfRepeats");
                builder.AppendLine($"  - {name}: {fromFrame}..{toFrame} (repeats={repeats})");
                tagCount++;
            }

            if (tagCount == 0)
            {
                builder.AppendLine("  tags: <empty>");
            }
        }

        private static T ReadMember<T>(Type type, object instance, string memberName)
        {
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                var value = property.GetValue(instance);
                return value is T cast ? cast : default;
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var value = field.GetValue(instance);
                return value is T cast ? cast : default;
            }

            return default;
        }
    }
}
