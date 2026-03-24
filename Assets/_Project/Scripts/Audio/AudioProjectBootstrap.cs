#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Audio;

namespace EJR.Game.Audio
{
    [InitializeOnLoad]
    internal static class AudioProjectBootstrap
    {
        private const string AudioRootPath = "Assets/_Project/Audio";
        private const string MixersPath = AudioRootPath + "/Mixers";
        private const string ResourcesPath = AudioRootPath + "/Resources";
        private const string MixerAssetPath = MixersPath + "/MainAudioMixer.mixer";
        private const string CatalogAssetPath = ResourcesPath + "/AudioCueCatalog.asset";
        static AudioProjectBootstrap()
        {
            EditorApplication.delayCall += EnsureProjectAudioAssets;
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            EnsureProjectAudioAssets();
        }

        [MenuItem("Tools/EJR/Audio/Rebuild Project Audio")]
        private static void RebuildProjectAudioMenu()
        {
            EnsureProjectAudioAssets();
        }

        public static void RebuildProjectAudioFromBatchMode()
        {
            EnsureProjectAudioAssets();
        }

        private static void EnsureProjectAudioAssets()
        {
            EnsureFolder("Assets", "_Project");
            EnsureFolder("Assets/_Project", "Audio");
            EnsureFolder(AudioRootPath, "Bgm");
            EnsureFolder(AudioRootPath, "Sfx");
            EnsureFolder(AudioRootPath, "Ui");
            EnsureFolder(AudioRootPath, "Mixers");
            EnsureFolder(AudioRootPath, "Resources");

            AssetDatabase.Refresh();
            var mixer = EnsureMixerAsset();
            EnsureCatalogAsset(mixer);
        }

        private static void EnsureFolder(string parentPath, string folderName)
        {
            var combined = $"{parentPath}/{folderName}";
            if (AssetDatabase.IsValidFolder(combined))
            {
                return;
            }

            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static AudioMixer EnsureMixerAsset()
        {
            var existingMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            if (existingMixer != null)
            {
                EnsureMixerGroups(existingMixer);
                return existingMixer;
            }

            TryCreateMixerViaMenu();

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            if (mixer == null)
            {
                TryCreateMixerViaReflection();
                mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            }

            if (mixer == null)
            {
                Debug.LogWarning("AudioProjectBootstrap could not create MainAudioMixer.mixer.");
                return null;
            }

            EnsureMixerGroups(mixer);
            return mixer;
        }

        private static void TryCreateMixerViaMenu()
        {
            var mixersFolder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MixersPath);
            Selection.activeObject = mixersFolder;

            var menuPaths = new[]
            {
                "Assets/Create/Audio/Audio Mixer",
                "Assets/Create/Audio Mixer",
            };

            for (var i = 0; i < menuPaths.Length; i++)
            {
                if (!EditorApplication.ExecuteMenuItem(menuPaths[i]))
                {
                    continue;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var mixerGuids = AssetDatabase.FindAssets("t:AudioMixer", new[] { MixersPath });
                if (mixerGuids.Length <= 0)
                {
                    continue;
                }

                var createdPath = AssetDatabase.GUIDToAssetPath(mixerGuids[0]);
                if (!string.Equals(createdPath, MixerAssetPath, StringComparison.OrdinalIgnoreCase))
                {
                    var moveError = AssetDatabase.MoveAsset(createdPath, MixerAssetPath);
                    if (!string.IsNullOrEmpty(moveError))
                    {
                        Debug.LogWarning($"AudioProjectBootstrap could not move audio mixer asset: {moveError}");
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return;
            }
        }

        private static void TryCreateMixerViaReflection()
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (TryCreateMixerViaEndNameEditAction(loadedAssemblies))
            {
                return;
            }

            var controllerType = FindLoadedEditorType(
                loadedAssemblies,
                "UnityEditor.Audio.AudioMixerController",
                "UnityEditor.AudioMixerController");
            if (controllerType == null || !typeof(ScriptableObject).IsAssignableFrom(controllerType))
            {
                Debug.LogWarning("AudioProjectBootstrap could not resolve an AudioMixerController type for reflection creation.");
                return;
            }

            var existingObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MixerAssetPath);
            if (existingObject != null)
            {
                return;
            }

            try
            {
                var controller = ScriptableObject.CreateInstance(controllerType);
                controller.name = Path.GetFileNameWithoutExtension(MixerAssetPath);
                AssetDatabase.CreateAsset(controller, MixerAssetPath);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"AudioProjectBootstrap reflection mixer creation failed with {controllerType.FullName}: {exception.Message}");
            }
        }

        private static bool TryCreateMixerViaEndNameEditAction(System.Reflection.Assembly[] loadedAssemblies)
        {
            var creatorType = FindLoadedEditorType(
                loadedAssemblies,
                "UnityEditor.DoCreateAudioMixer",
                "UnityEditor.Audio.DoCreateAudioMixer");
            if (creatorType == null || !typeof(ScriptableObject).IsAssignableFrom(creatorType))
            {
                return false;
            }

            var actionMethod = creatorType.GetMethod(
                "Action",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(string), typeof(string) },
                null);
            if (actionMethod == null)
            {
                return false;
            }

            try
            {
                var creator = ScriptableObject.CreateInstance(creatorType);
                actionMethod.Invoke(creator, new object[] { 0, MixerAssetPath, null });
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath) != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"AudioProjectBootstrap end-name action mixer creation failed with {creatorType.FullName}: {exception.Message}");
                return false;
            }
        }

        private static Type FindLoadedEditorType(System.Reflection.Assembly[] loadedAssemblies, params string[] fullNames)
        {
            for (var nameIndex = 0; nameIndex < fullNames.Length; nameIndex++)
            {
                var fullName = fullNames[nameIndex];
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    continue;
                }

                var resolved = Type.GetType(fullName, false);
                if (resolved != null)
                {
                    return resolved;
                }

                for (var assemblyIndex = 0; assemblyIndex < loadedAssemblies.Length; assemblyIndex++)
                {
                    var assembly = loadedAssemblies[assemblyIndex];
                    if (assembly == null)
                    {
                        continue;
                    }

                    resolved = assembly.GetType(fullName, false);
                    if (resolved != null)
                    {
                        return resolved;
                    }
                }
            }

            return null;
        }

        private static void EnsureMixerGroups(AudioMixer mixer)
        {
            if (mixer == null)
            {
                return;
            }

            var masterGroup = FindGroup(mixer, "Master");
            if (masterGroup == null)
            {
                return;
            }

            TryCreateGroup(mixer, masterGroup, "Bgm");
            TryCreateGroup(mixer, masterGroup, "Sfx");
            TryCreateGroup(mixer, masterGroup, "Ui");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void TryCreateGroup(AudioMixer mixer, AudioMixerGroup parentGroup, string groupName)
        {
            if (mixer == null || parentGroup == null || string.IsNullOrWhiteSpace(groupName) || FindGroup(mixer, groupName) != null)
            {
                return;
            }

            var controller = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MixerAssetPath);
            if (controller == null)
            {
                return;
            }

            var methods = controller.GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (!method.Name.Contains("Create"))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length < 2)
                {
                    continue;
                }

                var arguments = new object[parameters.Length];
                var canInvoke = true;
                for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    var parameter = parameters[parameterIndex];
                    if (parameter.ParameterType == typeof(string))
                    {
                        arguments[parameterIndex] = groupName;
                    }
                    else if (parameter.ParameterType.IsInstanceOfType(parentGroup))
                    {
                        arguments[parameterIndex] = parentGroup;
                    }
                    else if (parameter.ParameterType == typeof(bool))
                    {
                        arguments[parameterIndex] = false;
                    }
                    else if (parameter.HasDefaultValue)
                    {
                        arguments[parameterIndex] = parameter.DefaultValue;
                    }
                    else
                    {
                        canInvoke = false;
                        break;
                    }
                }

                if (!canInvoke)
                {
                    continue;
                }

                try
                {
                    method.Invoke(controller, arguments);
                    EditorUtility.SetDirty(controller);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(MixerAssetPath, ImportAssetOptions.ForceUpdate);
                    if (FindGroup(mixer, groupName) != null)
                    {
                        return;
                    }
                }
                catch
                {
                }
            }
        }

        private static void EnsureCatalogAsset(AudioMixer mixer)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCueCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            var masterGroup = FindGroup(mixer, "Master");
            var bgmGroup = FindGroup(mixer, "Bgm");
            var sfxGroup = FindGroup(mixer, "Sfx");
            var uiGroup = FindGroup(mixer, "Ui");
            var entries = BuildEntries(masterGroup, bgmGroup, sfxGroup, uiGroup);
            catalog.Configure(mixer, masterGroup, bgmGroup, sfxGroup, uiGroup, entries.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
        {
            if (mixer == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var groups = mixer.FindMatchingGroups(name);
            if (groups != null && groups.Length > 0)
            {
                return groups[0];
            }

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(MixerAssetPath);
            for (var i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is AudioMixerGroup group && string.Equals(group.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return group;
                }
            }

            return null;
        }

        private static List<AudioCueCatalog.Entry> BuildEntries(
            AudioMixerGroup masterGroup,
            AudioMixerGroup bgmGroup,
            AudioMixerGroup sfxGroup,
            AudioMixerGroup uiGroup)
        {
            return new List<AudioCueCatalog.Entry>
            {
                CreateEntry(AudioCueId.MainTheme, "Assets/_Project/Audio/Bgm/techno_chiptale.ogg", AudioBus.Bgm, bgmGroup ?? masterGroup, 0.55f, 0f, 0f, 1, true),
                CreateEntry(AudioCueId.UiConfirm, "Assets/_Project/Audio/Ui/ui_button_click.mp3", AudioBus.Ui, uiGroup ?? masterGroup, 0.82f, 0.01f, 0.02f, 2, false),
                CreateEntry(AudioCueId.UiBack, "Assets/_Project/Audio/Ui/ui_button_click.mp3", AudioBus.Ui, uiGroup ?? masterGroup, 0.72f, 0.01f, 0.02f, 2, false),
                CreateEntry(AudioCueId.UiAdjust, "Assets/_Project/Audio/Ui/ui_button_click.mp3", AudioBus.Ui, uiGroup ?? masterGroup, 0.58f, 0.01f, 0.03f, 1, false),
                CreateEntry(AudioCueId.LevelUpAppear, "Assets/_Project/Audio/Sfx/system_level_up.mp3", AudioBus.Sfx, sfxGroup ?? masterGroup, 0.88f, 0.01f, 0.05f, 1, false),
                CreateEntry(AudioCueId.LevelUpSelect, "Assets/_Project/Audio/Ui/ui_button_click.mp3", AudioBus.Ui, uiGroup ?? masterGroup, 0.95f, 0.01f, 0.02f, 2, false),
                CreateEntry(AudioCueId.BossWarning, "Assets/_Project/Audio/Sfx/system_boss_warning.mp3", AudioBus.Sfx, sfxGroup ?? masterGroup, 0.72f, 0f, 0.5f, 1, false),
                CreateEntry(AudioCueId.PlayerHurt, "Assets/_Project/Audio/Sfx/system_player_hurt.mp3", AudioBus.Sfx, sfxGroup ?? masterGroup, 0.85f, 0.02f, 0.05f, 1, false),
                CreateEntry(AudioCueId.XpPickup, "Assets/_Project/Audio/Sfx/system_xp_pickup.mp3", AudioBus.Sfx, sfxGroup ?? masterGroup, 0.78f, 0.02f, 0.03f, 2, false),
                CreateEntry(AudioCueId.WeaponRifle, "Assets/_Project/Audio/Sfx/weapon_rifle_lazer_shot_2.mp3", AudioBus.Sfx, sfxGroup ?? masterGroup, 0.84f, 0.03f, 0.02f, 3, false),
                CreateEntry(AudioCueId.WeaponShotgun, "Assets/_Project/Audio/Sfx/weapon_shotgun_shot.mp3", AudioBus.Sfx, sfxGroup ?? masterGroup, 0.92f, 0.03f, 0.05f, 2, false),
                CreateEntry(AudioCueId.WeaponFireball, "Assets/_Project/Audio/Sfx/weapon_fireball_cast.mp3", AudioBus.Sfx, sfxGroup ?? masterGroup, 0.88f, 0.03f, 0.04f, 2, false),
                CreateEntry(AudioCueId.WeaponKatana, "Assets/_Project/Audio/Sfx/weapon_katana_slash.mp3", AudioBus.Sfx, sfxGroup ?? masterGroup, 0.84f, 0.04f, 0.03f, 3, false),
                CreateEntry(AudioCueId.WeaponBfSword, "Assets/_Project/Audio/Sfx/weapon_bfsword_swing.mp3", AudioBus.Sfx, sfxGroup ?? masterGroup, 0.9f, 0.03f, 0.18f, 1, false),
                CreateEntry(AudioCueId.WeaponChainAttack, "Assets/_Project/Audio/Sfx/weapon_chain_zap.mp3", AudioBus.Sfx, sfxGroup ?? masterGroup, 0.88f, 0.02f, 0.05f, 2, false),
                CreateEntry(AudioCueId.WeaponTurretDeploy, "Assets/_Project/Audio/Sfx/weapon_turret_deploy.mp3", AudioBus.Sfx, sfxGroup ?? masterGroup, 0.86f, 0.02f, 0.08f, 1, false),
                CreateEntry(AudioCueId.WeaponBatFlap, "Assets/_Project/Audio/Sfx/weapon_bat_wings.mp3", AudioBus.Sfx, sfxGroup ?? masterGroup, 0.82f, 0.01f, 0.04f, 2, false),
                CreateEntry(AudioCueId.WeaponBatLatch, "Assets/_Project/Audio/Sfx/weapon_bat_chirp.mp3", AudioBus.Sfx, sfxGroup ?? masterGroup, 0.78f, 0.01f, 0.05f, 2, false),
            };
        }

        private static AudioCueCatalog.Entry CreateEntry(
            AudioCueId cueId,
            string clipPath,
            AudioBus bus,
            AudioMixerGroup mixerGroup,
            float volume,
            float pitchVariance,
            float minRetriggerInterval,
            int maxVoices,
            bool loop)
        {
            return new AudioCueCatalog.Entry
            {
                cueId = cueId,
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath),
                mixerGroup = mixerGroup,
                bus = bus,
                volume = volume,
                pitchVariance = pitchVariance,
                minRetriggerInterval = minRetriggerInterval,
                maxVoices = maxVoices,
                loop = loop,
            };
        }
    }
}
#endif
