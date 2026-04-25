using EJR.Game.Audio;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    /// <summary>
    /// åª›ì’•???¾ë‹¿ë¦??¨ë“¦êº?æ¿¡ì’–ì­???ëº¤ì“½??ë’— ?ëª…ê½£??ì” ??¼ì—¯??ˆë–.
    /// </summary>
    public interface IWeaponStrategy
    {
        WeaponUpgradeId WeaponId { get; }
        
        /// <summary>
        /// ï§??ê¾¨ì …???¾ë‹¿ë¦??ê³¹ê¹­????…ëœ²??„ë“ƒ??¸ë•²??
        /// </summary>
        void Update(WeaponRuntime weapon, AutoWeaponSystem system);
        
        /// <summary>
        /// ?¾ë‹¿ë¦°åª›? è«›ì’–ê¶??ä»¥Â€??¾§? ??ë??????ëª„í…§??¸ë•²??
        /// </summary>
        void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction);
        
        /// <summary>
        /// ?¾ë‹¿ë¦???ˆêº¼??†ì” ???ê³¹ê¹­ è¹‚Â€?????¥ë‡ë¦?ë¶? ?ê¾©ìŠ‚?????ëª„í…§??¸ë•²??
        /// </summary>
        void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system);
        void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color);

        float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system);
        float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system);
        float GetRange(WeaponRuntime weapon, AutoWeaponSystem system);
        Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system);
    }
}



