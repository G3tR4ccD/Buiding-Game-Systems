using UnityEngine;

// A ScriptableObject that defines an item type. One asset per item (e.g. "Heart", "Shield"),
// created via the Assets > Create > New Item menu. Slots and Inventory reference these assets
// rather than storing item data directly, so every copy of the same item shares one source of truth.
[CreateAssetMenu(fileName = "Item", menuName = "New Item")]
public class ItemSO : ScriptableObject
{
    public string itemName;
    public Sprite icon;                // Shown in the Slot UI
    public int maxStackSize = 64;      // How many of this item can sit in a single slot
    public GameObject itemPrefab;      // World-space pickup/drop representation, if used
    public GameObject handItemPrefab;  // What the player holds when this item is equipped, if used

    [Header("Equipment")]
    public EquipmentType equipmentType = EquipmentType.None; // None means this item can't go in an equipment slot
    public GameObject equippedPrefab;  // Attached to the matching body anchor on the player when equipped
}

// The four gear slot types. Add more here if you introduce new equipment categories later,
// Slot.allowedEquipmentType and Equipment's anchors would need a matching entry too.
public enum EquipmentType
{
    None,
    Helmet,
    Chestplate,
    Leggings,
    Boots
}