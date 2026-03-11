using UnityEngine;

namespace FreeWorld.Inventory
{
    public enum ItemCategory
    {
        Weapon, Ammo, Food, Water, Medical,
        Material, Tool, Armor, Misc
    }

    /// <summary>
    /// Immutable data asset that describes a single item type.
    /// Create via Assets → FreeWorld → Item Definition.
    /// </summary>
    [CreateAssetMenu(menuName = "FreeWorld/Item Definition", fileName = "Item_New")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string       itemName    = "New Item";
        [TextArea(2, 4)]
        public string       description = "";
        public ItemCategory category    = ItemCategory.Misc;
        public Sprite       icon;               // shown in inventory grid

        [Header("Stack")]
        public bool  stackable   = true;
        public int   maxStack    = 99;
        public float weightKg    = 0.1f;

        [Header("On Use")]
        public float healAmount     = 0f;   // restores HP
        public float foodAmount     = 0f;   // restores Hunger
        public float waterAmount    = 0f;   // restores Thirst
        public float staminaAmount  = 0f;   // restores Stamina

        [Header("World Prefab")]
        public GameObject dropPrefab;        // spawned when dropped to ground
    }
}
