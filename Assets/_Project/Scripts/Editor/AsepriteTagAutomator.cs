using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using EJR.Game.Gameplay;
using EJR.Game.Core;

namespace EJR.Game.Editor
{
    // AssetPostprocessor를 상속받아 파일 변경을 감지합니다.
    internal sealed class AsepriteTagAutomator : AssetPostprocessor
    {
        private static readonly string[] IdleNames = { "idle", "stand", "wait" };
        private static readonly string[] MoveNames = { "run", "walk", "move", "moving", "fly", "flying", "jump", "jump_loop" };
        private static readonly string[] HurtNames = { "hurt", "hit", "damaged", "knockback" };
        private static readonly string[] DieNames = { "die", "death", "dead", "dying" };

        // 파일이 임포트될 때 자동으로 실행됨
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool hasAsepriteChange = importedAssets.Any(p => p.EndsWith(".aseprite", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".ase", StringComparison.OrdinalIgnoreCase));
            if (hasAsepriteChange)
            {
                SyncAllTags();
            }
        }

        [MenuItem("Tools/EJR/Animation/Sync All Aseprite Tags")]
        public static void SyncAllTags()
        {
            var config = FindEnemyConfig();
            if (config == null) return;

            Undo.RecordObject(config, "Auto Sync Aseprite Tags");

            var guids = AssetDatabase.FindAssets("t:DefaultAsset");
            var asePaths = guids.Select(AssetDatabase.GUIDToAssetPath)
                                .Where(p => p.EndsWith(".aseprite", StringComparison.OrdinalIgnoreCase) || 
                                            p.EndsWith(".ase", StringComparison.OrdinalIgnoreCase))
                                .ToArray();

            int syncCount = 0;
            foreach (var path in asePaths)
            {
                if (TrySyncAssetToConfig(path, config)) syncCount++;
            }

            if (syncCount > 0)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"[AsepriteTagAutomator] Automatically synced {syncCount} assets to EnemyConfig.");
            }
        }

        private static EnemyConfig FindEnemyConfig()
        {
            var guid = AssetDatabase.FindAssets("t:EnemyConfig").FirstOrDefault();
            if (string.IsNullOrEmpty(guid)) return null;
            return AssetDatabase.LoadAssetAtPath<EnemyConfig>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static bool TrySyncAssetToConfig(string assetPath, EnemyConfig config)
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (!Enum.TryParse<RuntimeSpriteFactory.EnemyVisualKind>(fileName, true, out var kind))
            {
                if (fileName.Contains("Knight", StringComparison.OrdinalIgnoreCase)) kind = RuntimeSpriteFactory.EnemyVisualKind.Warrior;
                else if (fileName.Contains("Player", StringComparison.OrdinalIgnoreCase)) kind = RuntimeSpriteFactory.EnemyVisualKind.Warrior;
                else
                {
                    bool found = false;
                    foreach (RuntimeSpriteFactory.EnemyVisualKind k in Enum.GetValues(typeof(RuntimeSpriteFactory.EnemyVisualKind)))
                    {
                        if (fileName.Contains(k.ToString(), StringComparison.OrdinalIgnoreCase)) { kind = k; found = true; break; }
                    }
                    if (!found) return false;
                }
            }

            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null) return false;

            var tags = ExtractTagsFromImporter(importer);
            if (tags == null || tags.Count == 0) return false;

            var profile = config.GetAnimationProfile(kind);
            if (profile == null) return false;

            UpdateProfileWithTags(profile, tags);
            return true;
        }

        private static List<EnemyAnimationClipRange> ExtractTagsFromImporter(AssetImporter importer)
        {
            var result = new List<EnemyAnimationClipRange>();
            var tagsField = importer.GetType().GetField("m_Tags", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (tagsField == null) return null;

            var tagsList = tagsField.GetValue(importer) as IEnumerable;
            if (tagsList == null) return null;

            foreach (var tag in tagsList)
            {
                if (tag == null) continue;
                var tagType = tag.GetType();
                result.Add(new EnemyAnimationClipRange(
                    ReadMember<string>(tagType, tag, "name"),
                    ReadMember<int>(tagType, tag, "fromFrame"),
                    ReadMember<int>(tagType, tag, "toFrame"),
                    ReadMember<int>(tagType, tag, "noOfRepeats") == 0
                ));
            }
            return result;
        }

        private static void UpdateProfileWithTags(EnemyAnimationProfile profile, List<EnemyAnimationClipRange> tags)
        {
            profile.clipRanges = tags.ToArray();
            foreach (var tag in tags)
            {
                string n = tag.clipName.ToLower();
                if (IdleNames.Any(c => n.Contains(c))) { profile.idleStartFrame = tag.startFrame; profile.idleEndFrame = tag.endFrame; }
                else if (MoveNames.Any(c => n.Contains(c))) { profile.moveStartFrame = tag.startFrame; profile.moveEndFrame = tag.endFrame; }
                else if (HurtNames.Any(c => n.Contains(c))) { profile.hurtStartFrame = tag.startFrame; profile.hurtEndFrame = tag.endFrame; profile.useHurtAnimation = true; }
                else if (DieNames.Any(c => n.Contains(c))) { profile.dieStartFrame = tag.startFrame; profile.dieEndFrame = tag.endFrame; }
            }
        }

        private static T ReadMember<T>(Type type, object instance, string memberName)
        {
            try
            {
                var prop = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null) return (T)Convert.ChangeType(prop.GetValue(instance), typeof(T));
                var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) return (T)Convert.ChangeType(field.GetValue(instance), typeof(T));
            }
            catch { }
            return default;
        }
    }
}
