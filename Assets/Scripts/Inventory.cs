using UnityEngine;
using System.Collections.Generic;

// Owns the player's inventory and hotbar slots, handles adding items to them,
// and handles opening/closing the inventory UI.
[DisallowMultipleComponent]
public class Inventory : MonoBehaviour
{
    // Test items, added via debug keys in PlayerMovement purely for quick manual testing
    public ItemSO heartItem;
    public ItemSO shieldItem;

    public GameObject hotbarObj;          // Parent object whose children are the hotbar Slots (always visible)
    public GameObject inventoryObj;       // Parent object whose children are the inventory Slots (toggled open/closed)
    public PlayerMovement playerMovement; // Paused while the inventory is open, so the player can't move or look around

    private List<Slot> inventorySlots = new List<Slot>();
    private List<Slot> hotbarSlots = new List<Slot>();
    private List<Slot> allSlots = new List<Slot>(); // inventorySlots + hotbarSlots combined, used by AddItem

    private int selectedHotbarIndex = 0; // which hotbar slot is currently highlighted/selected

    private void Awake()
    {
        if (playerMovement == null)
        {
            // Without this reference ToggleInventory() can never pause look/move input,
            // so the camera stays controllable the whole time the panel is open.
            Debug.LogWarning("Inventory: playerMovement is not assigned in the Inspector. " +
                "Mouse look and movement won't pause while the inventory is open.", this);
        }

        // includeInactive: true matters here, since inventoryObj starts disabled (see Start())
        // and GetComponentsInChildren skips inactive hierarchies by default.
        inventorySlots.AddRange(inventoryObj.GetComponentsInChildren<Slot>(true));
        hotbarSlots.AddRange(hotbarObj.GetComponentsInChildren<Slot>(true));

        // Equipment slots (allowedEquipmentType != None) are excluded here even if they
        // happen to sit somewhere underneath inventoryObj/hotbarObj in the hierarchy.
        // GetComponentsInChildren doesn't know or care about logical grouping, only actual
        // parenting, so without this a restricted gear slot nested under the inventory panel
        // would get treated as ordinary storage and AddItem could dump items into it.
        inventorySlots.RemoveAll(slot => slot.allowedEquipmentType != EquipmentType.None);
        hotbarSlots.RemoveAll(slot => slot.allowedEquipmentType != EquipmentType.None);

        allSlots.AddRange(inventorySlots);
        allSlots.AddRange(hotbarSlots);

        foreach (Slot slot in inventorySlots)
        {
            slot.Initialize(this, false);
        }

        foreach (Slot slot in hotbarSlots)
        {
            slot.Initialize(this, true);
        }
    }

    private void Start()
    {
        // Inventory panel starts closed, regardless of how it was left active in the Editor
        inventoryObj.SetActive(false);

        SelectHotbarSlot(0); // highlight the first slot by default
    }

    // Called by PlayerMovement.OnToggleInventory. Flips the inventory panel open/closed,
    // and matches the cursor and player input to it: open = free cursor, player input
    // paused. Closed = locked cursor, player input resumed.
    public void ToggleInventory()
    {
        bool willBeOpen = !inventoryObj.activeSelf;
        inventoryObj.SetActive(willBeOpen);

        Cursor.lockState = willBeOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = willBeOpen;

        if (playerMovement != null)
        {
            playerMovement.SetInputEnabled(!willBeOpen);
        }
    }

    // Called by PlayerMovement.OnSelectHotbarSlot with the pressed key already translated
    // into a 0-based index. If the player is hovering an inventory slot at the time, swaps
    // that item straight into the target hotbar slot instead of just changing the selection,
    // the same quick-assign most inventory UIs support.
    public void SelectOrSwapHotbarSlot(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= hotbarSlots.Count)
        {
            return;
        }

        Slot hoveredInventorySlot = inventorySlots.Find(slot => slot.hovering);

        if (hoveredInventorySlot != null && hoveredInventorySlot.HasItem())
        {
            Slot targetHotbarSlot = hotbarSlots[targetIndex];

            ItemSO invItem = hoveredInventorySlot.GetItem();
            int invAmount = hoveredInventorySlot.GetAmount();

            ItemSO hotbarItem = targetHotbarSlot.GetItem();
            int hotbarAmount = targetHotbarSlot.GetAmount();

            targetHotbarSlot.SetItem(invItem, invAmount);

            if (hotbarItem != null)
            {
                hoveredInventorySlot.SetItem(hotbarItem, hotbarAmount);
            }
            else
            {
                hoveredInventorySlot.ClearSlot();
            }
        }
        else
        {
            SelectHotbarSlot(targetIndex);
        }
    }

    // Called by PlayerMovement.OnScrollHotbar with the scroll direction already reduced
    // to a plain +1 or -1. SelectHotbarSlot handles wrapping around at both ends.
    public void ScrollHotbar(int direction)
    {
        SelectHotbarSlot(selectedHotbarIndex + direction);
    }

    // Moves the highlight to the given hotbar slot. Wraps around at both ends,
    // so scrolling past the last slot loops back to the first and vice versa.
    public void SelectHotbarSlot(int index)
    {
        if (hotbarSlots.Count == 0)
        {
            return;
        }

        index = ((index % hotbarSlots.Count) + hotbarSlots.Count) % hotbarSlots.Count;

        hotbarSlots[selectedHotbarIndex].SetHighlighted(false);
        selectedHotbarIndex = index;
        hotbarSlots[selectedHotbarIndex].SetHighlighted(true);
    }

    // Handy for hooking up an equip/use system later without duplicating the index logic here
    public Slot GetSelectedHotbarSlot()
    {
        return hotbarSlots.Count > 0 ? hotbarSlots[selectedHotbarIndex] : null;
    }

    // Adds `amount` of itemToAdd across the inventory and hotbar, stacking onto existing
    // slots first and only falling back to empty slots for whatever doesn't fit.
    public void AddItem(ItemSO itemToAdd, int amount)
    {
        int remaining = amount;

        // Pass 1: stack onto slots that already hold this item
        foreach (Slot slot in allSlots)
        {
            if (remaining <= 0)
            {
                return;
            }

            if (slot.HasItem() && slot.GetItem() == itemToAdd)
            {
                int currentAmount = slot.GetAmount();
                int maxStackSize = itemToAdd.maxStackSize;

                if (currentAmount < maxStackSize)
                {
                    int spaceLeft = maxStackSize - currentAmount;
                    int amountToAdd = Mathf.Min(spaceLeft, remaining);

                    slot.SetItem(itemToAdd, currentAmount + amountToAdd);
                    remaining -= amountToAdd;
                }
            }
        }

        if (remaining <= 0)
        {
            return;
        }

        // Pass 2: place whatever's left into empty slots
        foreach (Slot slot in allSlots)
        {
            if (remaining <= 0)
            {
                return;
            }

            if (!slot.HasItem() && slot.CanAccept(itemToAdd))
            {
                int amountToPlace = Mathf.Min(itemToAdd.maxStackSize, remaining);
                slot.SetItem(itemToAdd, amountToPlace);
                remaining -= amountToPlace;
            }
        }

        // Still remaining after both passes means the inventory and hotbar are genuinely full
        if (remaining > 0)
        {
            Debug.Log("Not enough space in inventory for " + remaining + " of " + itemToAdd.itemName);
        }
    }

    // Shift+click: moves a slot's whole stack to the other container (inventory -> hotbar,
    // or hotbar -> inventory), stacking onto matches there first and only using empty
    // slots for whatever's left over. Whatever doesn't fit anywhere stays where it was.
    public void QuickTransfer(Slot sourceSlot, bool sourceIsHotbar)
    {
        if (!sourceSlot.HasItem())
        {
            return;
        }

        List<Slot> targetList = sourceIsHotbar ? inventorySlots : hotbarSlots;
        ItemSO item = sourceSlot.GetItem();
        int startingAmount = sourceSlot.GetAmount();
        int remaining = startingAmount;

        // Pass 1: stack onto matching slots in the other container
        foreach (Slot slot in targetList)
        {
            if (remaining <= 0)
            {
                break;
            }

            if (slot.HasItem() && slot.GetItem() == item)
            {
                int spaceLeft = item.maxStackSize - slot.GetAmount();
                int amountToAdd = Mathf.Min(spaceLeft, remaining);

                if (amountToAdd > 0)
                {
                    slot.AddAmount(amountToAdd);
                    remaining -= amountToAdd;
                }
            }
        }

        // Pass 2: place whatever's left into empty slots over there
        foreach (Slot slot in targetList)
        {
            if (remaining <= 0)
            {
                break;
            }

            if (!slot.HasItem() && slot.CanAccept(item))
            {
                int amountToPlace = Mathf.Min(item.maxStackSize, remaining);
                slot.SetItem(item, amountToPlace);
                remaining -= amountToPlace;
            }
        }

        int transferred = startingAmount - remaining;
        if (transferred > 0)
        {
            sourceSlot.RemoveAmount(transferred);
        }
    }

    // Ctrl+click: spreads a slot's stack across every other slot already holding the
    // same item, topping each one off before moving to the next. Leftover that doesn't
    // fit anywhere stays behind in the slot that was clicked.
    public void DistributeToMatchingSlots(Slot sourceSlot)
    {
        if (!sourceSlot.HasItem())
        {
            return;
        }

        ItemSO item = sourceSlot.GetItem();
        int startingAmount = sourceSlot.GetAmount();
        int remaining = startingAmount;

        foreach (Slot slot in allSlots)
        {
            if (remaining <= 0)
            {
                break;
            }

            if (slot == sourceSlot || !slot.HasItem() || slot.GetItem() != item)
            {
                continue;
            }

            int spaceLeft = item.maxStackSize - slot.GetAmount();
            int amountToAdd = Mathf.Min(spaceLeft, remaining);

            if (amountToAdd > 0)
            {
                slot.AddAmount(amountToAdd);
                remaining -= amountToAdd;
            }
        }

        int distributed = startingAmount - remaining;
        if (distributed > 0)
        {
            sourceSlot.RemoveAmount(distributed);
        }
    }
}