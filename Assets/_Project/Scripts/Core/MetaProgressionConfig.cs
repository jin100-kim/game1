using System;
using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Core
{
    public enum MetaNodeId
    {
        AttackI = 0,
        AttackII = 1,
        AttackIII = 2,
        AttackSpeedI = 3,
        RangeI = 4,
        HealthI = 5,
        HealthII = 6,
        RegenI = 7,
        MoveSpeedI = 8,
        LuckI = 9,
        LuckII = 10,
        LuckIII = 11,
    }

    [Serializable]
    public struct MetaBonusValues
    {
        public float attackPowerPercent;
        public float attackSpeedPercent;
        public float maxHealthFlat;
        public float healthRegenPerSecond;
        public float moveSpeedPercent;
        public float attackRangePercent;
        public float luck;

        public static MetaBonusValues operator +(MetaBonusValues a, MetaBonusValues b)
        {
            return new MetaBonusValues
            {
                attackPowerPercent = a.attackPowerPercent + b.attackPowerPercent,
                attackSpeedPercent = a.attackSpeedPercent + b.attackSpeedPercent,
                maxHealthFlat = a.maxHealthFlat + b.maxHealthFlat,
                healthRegenPerSecond = a.healthRegenPerSecond + b.healthRegenPerSecond,
                moveSpeedPercent = a.moveSpeedPercent + b.moveSpeedPercent,
                attackRangePercent = a.attackRangePercent + b.attackRangePercent,
                luck = a.luck + b.luck,
            };
        }
    }

    [Serializable]
    public struct MetaNodeDefinition
    {
        public MetaNodeDefinition(
            MetaNodeId id,
            string title,
            string description,
            int cost,
            MetaBonusValues bonuses,
            bool hasPrerequisite = false,
            MetaNodeId prerequisiteId = MetaNodeId.AttackI)
        {
            Id = id;
            Title = title;
            Description = description;
            Cost = cost;
            Bonuses = bonuses;
            HasPrerequisite = hasPrerequisite;
            PrerequisiteId = prerequisiteId;
        }

        public MetaNodeId Id;
        public string Title;
        public string Description;
        public int Cost;
        public MetaBonusValues Bonuses;
        public bool HasPrerequisite;
        public MetaNodeId PrerequisiteId;
    }

    [Serializable]
    public sealed class MetaProgressionConfig : ScriptableObject
    {
        [SerializeField, Min(0)] private int baseParticipationCredits = 25;
        [SerializeField, Min(0)] private int creditsPerLevel = 5;
        [SerializeField, Min(0)] private int bossReachedCredits = 10;
        [SerializeField, Min(0)] private int clearCredits = 75;
        [SerializeField, Min(1)] private int killsPerCredit = 10;

        private readonly List<MetaNodeDefinition> _nodeDefinitions = new();
        private readonly Dictionary<MetaNodeId, MetaNodeDefinition> _nodeLookup = new();

        public IReadOnlyList<MetaNodeDefinition> NodeDefinitions => _nodeDefinitions;

        public static MetaProgressionConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<MetaProgressionConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;
            config.BuildDefaults();
            return config;
        }

        public int CalculateCredits(int finalLevel, bool bossReached, bool cleared, int enemiesDefeated)
        {
            var credits = baseParticipationCredits;
            credits += Mathf.Max(0, finalLevel) * creditsPerLevel;
            if (bossReached)
            {
                credits += bossReachedCredits;
            }

            if (cleared)
            {
                credits += clearCredits;
            }

            credits += Mathf.Max(0, enemiesDefeated) / Mathf.Max(1, killsPerCredit);
            return Mathf.Max(0, credits);
        }

        public bool TryGetNodeDefinition(MetaNodeId nodeId, out MetaNodeDefinition definition)
        {
            EnsureLookups();
            return _nodeLookup.TryGetValue(nodeId, out definition);
        }

        private void BuildDefaults()
        {
            _nodeDefinitions.Clear();
            _nodeDefinitions.Add(new MetaNodeDefinition(MetaNodeId.AttackI, "Attack I", "Attack +6%", 60, new MetaBonusValues { attackPowerPercent = 6f }));
            _nodeDefinitions.Add(new MetaNodeDefinition(MetaNodeId.AttackII, "Attack II", "Attack +6%", 80, new MetaBonusValues { attackPowerPercent = 6f }, true, MetaNodeId.AttackI));
            _nodeDefinitions.Add(new MetaNodeDefinition(MetaNodeId.AttackIII, "Attack III", "Attack +8%", 100, new MetaBonusValues { attackPowerPercent = 8f }, true, MetaNodeId.AttackII));
            _nodeDefinitions.Add(new MetaNodeDefinition(MetaNodeId.AttackSpeedI, "Attack Speed I", "Attack Speed +3%", 120, new MetaBonusValues { attackSpeedPercent = 3f }, true, MetaNodeId.AttackI));
            _nodeDefinitions.Add(new MetaNodeDefinition(MetaNodeId.RangeI, "Range I", "Attack Range +6%", 140, new MetaBonusValues { attackRangePercent = 6f }, true, MetaNodeId.AttackI));
            _nodeDefinitions.Add(new MetaNodeDefinition(MetaNodeId.HealthI, "Health I", "Max Health +15", 60, new MetaBonusValues { maxHealthFlat = 15f }));
            _nodeDefinitions.Add(new MetaNodeDefinition(MetaNodeId.HealthII, "Health II", "Max Health +15", 80, new MetaBonusValues { maxHealthFlat = 15f }, true, MetaNodeId.HealthI));
            _nodeDefinitions.Add(new MetaNodeDefinition(MetaNodeId.RegenI, "Regen I", "Health Regen +0.25/s", 120, new MetaBonusValues { healthRegenPerSecond = 0.25f }, true, MetaNodeId.HealthI));
            _nodeDefinitions.Add(new MetaNodeDefinition(MetaNodeId.MoveSpeedI, "Move Speed I", "Move Speed +4%", 140, new MetaBonusValues { moveSpeedPercent = 4f }, true, MetaNodeId.HealthI));
            _nodeDefinitions.Add(new MetaNodeDefinition(MetaNodeId.LuckI, "Luck I", "Luck +1", 100, new MetaBonusValues { luck = 1f }));
            _nodeDefinitions.Add(new MetaNodeDefinition(MetaNodeId.LuckII, "Luck II", "Luck +1", 140, new MetaBonusValues { luck = 1f }, true, MetaNodeId.LuckI));
            _nodeDefinitions.Add(new MetaNodeDefinition(MetaNodeId.LuckIII, "Luck III", "Luck +1", 220, new MetaBonusValues { luck = 1f }, true, MetaNodeId.LuckII));
            EnsureLookups();
        }

        private void EnsureLookups()
        {
            if (_nodeLookup.Count == _nodeDefinitions.Count)
            {
                return;
            }

            _nodeLookup.Clear();
            for (var i = 0; i < _nodeDefinitions.Count; i++)
            {
                _nodeLookup[_nodeDefinitions[i].Id] = _nodeDefinitions[i];
            }
        }
    }
}
