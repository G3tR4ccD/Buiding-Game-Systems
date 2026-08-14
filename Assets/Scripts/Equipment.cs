using UnityEngine;

// Manages the four gear slots (helmet, chestplate, leggings, boots) and keeps whatever's
// visually attached to the player in sync with what's actually equipped in each slot.
//
// Setup in the Inspector:
// - Drag each Slot UI element into the matching field below, then on that Slot component
//   itself, set Allowed Equipment Type to the matching type. That's what makes drag/drop
//   reject the wrong gear in the wrong slot.
// - Drag a Transform on the player (a head bone, chest bone, etc.) into each anchor field.
//   Equipping an item with an equippedPrefab set instantiates it under that anchor;
//   unequipping destroys it. Leave an anchor empty if you don't want a visual for it yet.
[DisallowMultipleComponent]
public class Equipment : MonoBehaviour
{
    [System.Serializable]
    private class GearSlot
    {
        public Slot slot;
        public Transform anchor;

        [System.NonSerialized] public GameObject currentVisual;
    }

    [Header("Slots")]
    public Slot helmetSlot;
    public Slot chestplateSlot;
    public Slot leggingsSlot;
    public Slot bootsSlot;

    [Header("Body Anchors")]
    public Transform helmetAnchor;
    public Transform chestplateAnchor;
    public Transform leggingsAnchor;
    public Transform bootsAnchor;

    private GearSlot[] gearSlots;

    private void Awake()
    {
        gearSlots = new GearSlot[]
        {
            new GearSlot { slot = helmetSlot, anchor = helmetAnchor },
            new GearSlot { slot = chestplateSlot, anchor = chestplateAnchor },
            new GearSlot { slot = leggingsSlot, anchor = leggingsAnchor },
            new GearSlot { slot = bootsSlot, anchor = bootsAnchor },
        };

        // Equipment slots deliberately never call Slot.Initialize(): leaving owningInventory
        // null means shift/ctrl click quietly do nothing here, since quick-transfer and
        // distribute don't have a defined meaning for gear yet. Drag and drop equip/unequip
        // still works fine either way, since that path doesn't depend on Initialize() at all.
        foreach (GearSlot gearSlot in gearSlots)
        {
            if (gearSlot.slot != null)
            {
                gearSlot.slot.OnChanged += () => RefreshVisual(gearSlot);
            }
        }
    }

    // Destroys whatever was attached before and spawns the new one, if any. Runs
    // automatically any time the matching slot's contents change (equip, unequip, or swap).
    private void RefreshVisual(GearSlot gearSlot)
    {
        if (gearSlot.currentVisual != null)
        {
            Destroy(gearSlot.currentVisual);
            gearSlot.currentVisual = null;
        }

        if (gearSlot.slot.HasItem() && gearSlot.anchor != null)
        {
            GameObject prefab = gearSlot.slot.GetItem().equippedPrefab;

            if (prefab != null)
            {
                gearSlot.currentVisual = Instantiate(prefab, gearSlot.anchor);
            }
        }
    }
}
