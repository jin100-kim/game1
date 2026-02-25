using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class LevelUpSystem
    {
        private readonly List<LevelUpOption> _workingOptions = new(3);
        private int _pendingChoices;
        private bool _awaitingChoice;

        public int Level { get; private set; } = 1;
        public int CurrentExperience { get; private set; }
        public int RequiredExperience { get; private set; } = ProgressionMath.RequiredExperienceForLevel(1);
        public bool IsAwaitingChoice => _awaitingChoice;
        public bool HasPendingChoices => _pendingChoices > 0;

        public event Action<int, int, int> ExperienceChanged;
        public event Action<LevelUpOption[]> OptionsGenerated;

        public void AddExperience(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            CurrentExperience += amount;

            while (CurrentExperience >= RequiredExperience)
            {
                CurrentExperience -= RequiredExperience;
                Level++;
                RequiredExperience = ProgressionMath.RequiredExperienceForLevel(Level);
                _pendingChoices++;
            }

            ExperienceChanged?.Invoke(CurrentExperience, RequiredExperience, Level);

            if (_pendingChoices > 0 && !_awaitingChoice)
            {
                _awaitingChoice = true;
                OptionsGenerated?.Invoke(GenerateOptions());
            }
        }

        public void ApplyOption(int optionIndex, IReadOnlyList<LevelUpOption> options, PlayerStatsRuntime stats)
        {
            if (!_awaitingChoice || stats == null || options == null || options.Count == 0)
            {
                return;
            }

            optionIndex = Mathf.Clamp(optionIndex, 0, options.Count - 1);
            stats.ApplyUpgrade(options[optionIndex]);

            _pendingChoices = Mathf.Max(0, _pendingChoices - 1);
            _awaitingChoice = false;

            if (_pendingChoices > 0)
            {
                _awaitingChoice = true;
                OptionsGenerated?.Invoke(GenerateOptions());
            }

            ExperienceChanged?.Invoke(CurrentExperience, RequiredExperience, Level);
        }

        private LevelUpOption[] GenerateOptions()
        {
            _workingOptions.Clear();
            _workingOptions.Add(new LevelUpOption(LevelUpUpgradeType.Damage, 0.25f, "Damage +25%"));
            _workingOptions.Add(new LevelUpOption(LevelUpUpgradeType.AttackSpeed, 0.12f, "Attack Speed +12%"));
            _workingOptions.Add(new LevelUpOption(LevelUpUpgradeType.MoveSpeed, 0.10f, "Move Speed +10%"));
            return _workingOptions.ToArray();
        }
    }
}
