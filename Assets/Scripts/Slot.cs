using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

// A single inventory or hotbar UI slot. Holds one item type and a stack amount,
// keeps its icon/amount text in sync whenever that changes, and supports
// dragging a stack onto another slot to move, merge, or swap items.
public class Slot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
{
    public bool hovering; // True while the mouse is over this slot, read by other UI (tooltips, drag-and-drop, etc.)

    private ItemSO heldItem;
    private int itemAmount;

    private Image iconImage;
    private TextMeshProUGUI amountText;

    private Canvas rootCanvas;     // Drag icon gets parented here so it renders above every other slot
    private GameObject dragIconObj; // Temporary ghost icon that follows the cursor while dragging
    private int dragAmount = 0;     // How much of the stack THIS drag is carrying (full stack, or half on right-click)

    private Image backgroundImage;  // This slot's own background, used for the selection highlight
    private Color defaultColor;     // Whatever color the background started with, restored when deselected
    public Color selectedColor = new Color(1f, 0.92f, 0.5f, 1f); // Highlight color for the selected hotbar slot

    [Header("Number Label (optional)")]
    // If this slot has a number-key indicator as a child (a copied, non-functional label,
    // not a real Slot), drag it here so it lights up together with this slot's own highlight.
    public SlotNumberLabel numberLabel;

    private Inventory owningInventory; // Set by Inventory.Awake() so shift/ctrl click can reach the other slots
    private bool isHotbarSlot;         // Which container this slot belongs to, needed for shift-click quick transfer

    // Shared by every Slot instance, there's only ever one tooltip on screen at a time,
    // so it's built once on first use rather than each slot having its own copy.
    private static GameObject tooltipObj;
    private static RectTransform tooltipRect;
    private static TextMeshProUGUI tooltipText;
    private static bool isAnyDragInProgress; // hides the tooltip while a drag ghost is on screen instead

    [Header("Equipment Restriction")]
    // Leave as None for a normal, unrestricted slot (inventory, hotbar). Set to a specific
    // type to turn this into a gear slot that only accepts matching equipment.
    public EquipmentType allowedEquipmentType = EquipmentType.None;

    // Fired whenever this slot's item or amount changes. Used by Equipment to know when
    // to update the visual attached to the player, but anything can listen to this.
    public event Action OnChanged;

    // Called once by Inventory right after it collects all the slots, so this slot
    // knows which container it belongs to and can ask Inventory to move items around.
    public void Initialize(Inventory inventory, bool inHotbar)
    {
        owningInventory = inventory;
        isHotbarSlot = inHotbar;
    }

    // Whether the given item is allowed to be placed in this slot. Always true for
    // unrestricted slots; for a gear slot, only an item of the matching EquipmentType.
    public bool CanAccept(ItemSO item)
    {
        if (allowedEquipmentType == EquipmentType.None)
        {
            return true;
        }

        return item != null && item.equipmentType == allowedEquipmentType;
    }

    private void Awake()
    {
        EnsureUICached();
    }

    // Caches the icon/text/background references. Normally happens once via Awake(), but
    // a Slot sitting under a GameObject that starts inactive (inventoryObj, before the
    // panel is ever opened) never gets its Awake() called by Unity until that object is
    // activated at least once. UpdateSlot() can still be reached before then though, via
    // AddItem() debug-spawning an item straight into an inventory slot, so this same
    // caching also runs lazily from UpdateSlot() itself as a fallback.
    private void EnsureUICached()
    {
        if (iconImage != null)
        {
            return; // already cached, either by Awake() or an earlier lazy call
        }

        // Assumes a fixed child layout: child 0 is the icon Image, child 1 is the amount text.
        // If the slot prefab's hierarchy changes, these indices need to change too.
        iconImage = transform.GetChild(0).GetComponent<Image>();
        amountText = transform.GetChild(1).GetComponent<TextMeshProUGUI>();
        rootCanvas = GetComponentInParent<Canvas>();

        backgroundImage = GetComponent<Image>();
        if (backgroundImage != null)
        {
            defaultColor = backgroundImage.color;
        }
    }

    // Toggles this slot's highlight on or off. Used by Inventory to mark the selected hotbar slot.
    public void SetHighlighted(bool highlighted)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = highlighted ? selectedColor : defaultColor;
        }

        if (numberLabel != null)
        {
            numberLabel.SetHighlighted(highlighted);
        }
    }

    public ItemSO GetItem()
    {
        return heldItem;
    }

    public int GetAmount()
    {
        return itemAmount;
    }

    // Overwrites whatever this slot currently holds with a new item and amount.
    public void SetItem(ItemSO item, int amount = 1)
    {
        heldItem = item;
        itemAmount = amount;
        UpdateSlot();
    }

    // Refreshes the icon and amount text to match the slot's current state.
    // Call this any time heldItem or itemAmount changes.
    public void UpdateSlot()
    {
        EnsureUICached();

        if (heldItem != null)
        {
            iconImage.enabled = true;
            iconImage.sprite = heldItem.icon;

            // Equipment slots show the item's name (a stack count doesn't mean much for
            // a single piece of gear); everything else shows how many are in the stack.
            bool isEquipmentSlot = allowedEquipmentType != EquipmentType.None;
            amountText.text = isEquipmentSlot ? heldItem.itemName : itemAmount.ToString();
        }
        else
        {
            iconImage.enabled = false;
            amountText.text = "";
        }

        OnChanged?.Invoke();
        RefreshTooltipIfHovering();
    }

    public int AddAmount(int amountToAdd)
    {
        itemAmount += amountToAdd;
        UpdateSlot();
        return itemAmount;
    }

    // Removes amountToRemove from the stack. Clears the slot entirely if that brings it to 0 or below.
    public int RemoveAmount(int amountToRemove)
    {
        itemAmount -= amountToRemove;

        if (itemAmount <= 0)
        {
            ClearSlot();
        }
        else
        {
            UpdateSlot();
        }

        return itemAmount;
    }

    public void ClearSlot()
    {
        heldItem = null;
        itemAmount = 0;
        UpdateSlot();
    }

    // --- Hover tooltip ---
    // Shows the item's name next to the cursor while hovering any slot that has one.

    private void ShowTooltip(Vector2 screenPosition)
    {
        if (isAnyDragInProgress)
        {
            return;
        }

        if (tooltipObj == null)
        {
            CreateTooltip();
        }

        if (tooltipObj == null) // still null means there was no canvas to parent it to
        {
            return;
        }

        tooltipText.text = heldItem.itemName;
        tooltipObj.SetActive(true);
        PositionTooltip(screenPosition);
    }

    private void HideTooltip()
    {
        if (tooltipObj != null)
        {
            tooltipObj.SetActive(false);
        }
    }

    private void PositionTooltip(Vector2 screenPosition)
    {
        if (tooltipRect != null)
        {
            // Small offset so the tooltip sits beside the cursor rather than under it
            tooltipRect.position = screenPosition + new Vector2(16f, -16f);
        }
    }

    // Called from UpdateSlot() so a tooltip already on screen doesn't go stale if this
    // slot's contents change without the mouse ever leaving it (a shift-click transfer,
    // a number-key swap, etc. can all empty a slot the cursor is still sitting on).
    // Only refreshes text/visibility, not position, the next OnPointerMove call handles
    // that, keeping this free of any direct Input System dependency.
    private void RefreshTooltipIfHovering()
    {
        if (!hovering)
        {
            return;
        }

        if (HasItem())
        {
            if (tooltipObj == null)
            {
                CreateTooltip();
            }

            if (tooltipObj != null)
            {
                tooltipText.text = heldItem.itemName;
                tooltipObj.SetActive(true);
            }
        }
        else
        {
            HideTooltip();
        }
    }

    private void CreateTooltip()
    {
        if (rootCanvas == null)
        {
            return;
        }

        tooltipObj = new GameObject("SlotTooltip", typeof(RectTransform));
        tooltipObj.transform.SetParent(rootCanvas.transform, false);
        tooltipObj.transform.SetAsLastSibling(); // draw on top of everything, including drag icons

        Image background = tooltipObj.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.85f);
        background.raycastTarget = false;

        tooltipRect = tooltipObj.GetComponent<RectTransform>();
        tooltipRect.sizeDelta = new Vector2(140f, 32f);
        tooltipRect.pivot = new Vector2(0f, 1f); // anchor top-left so it grows away from the cursor, not over it

        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(tooltipObj.transform, false);

        tooltipText = textObj.AddComponent<TextMeshProUGUI>();
        tooltipText.fontSize = 18f;
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAlignmentOptions.MidlineLeft;
        tooltipText.raycastTarget = false;

        RectTransform textRect = tooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);

        tooltipObj.SetActive(false);
    }

    public bool HasItem()
    {
        return heldItem != null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;

        if (HasItem())
        {
            ShowTooltip(eventData.position);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        HideTooltip();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (hovering && HasItem())
        {
            PositionTooltip(eventData.position);
        }
    }

    // --- Drag and drop ---
    // Dragging a stack moves it to an empty slot, merges it into a matching stack,
    // or swaps it with a different item, depending on what it's dropped onto.

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!HasItem() || rootCanvas == null)
        {
            return;
        }

        // Left click drags the full stack, right click drags half of it (rounded up).
        // Anything else (middle click) is ignored.
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            dragAmount = itemAmount;
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            dragAmount = Mathf.CeilToInt(itemAmount / 2f);
        }
        else
        {
            return;
        }

        if (dragAmount <= 0)
        {
            return;
        }

        isAnyDragInProgress = true;
        HideTooltip();

        // Spawn a temporary icon that follows the cursor. This is purely visual, Unity
        // already tracks this slot as eventData.pointerDrag for the whole drag, so OnDrop
        // on the target slot can find it without any extra bookkeeping here.
        dragIconObj = new GameObject("DragIcon", typeof(RectTransform));
        dragIconObj.transform.SetParent(rootCanvas.transform, false);
        dragIconObj.transform.SetAsLastSibling(); // draw on top of every other slot

        Image dragImage = dragIconObj.AddComponent<Image>();
        dragImage.sprite = heldItem.icon;
        dragImage.raycastTarget = false; // let raycasts pass through to whatever slot is underneath the cursor

        RectTransform dragIconRect = dragIconObj.GetComponent<RectTransform>();
        dragIconRect.sizeDelta = iconImage.rectTransform.sizeDelta;
        dragIconRect.position = eventData.position; // assumes a Screen Space - Overlay canvas

        // Clone the slot's own amount text onto the ghost icon so a half-stack drag shows how
        // much is actually being carried. Reuses the original's styling; reposition in the
        // Inspector afterward if it doesn't line up on the smaller ghost icon.
        TextMeshProUGUI dragText = Instantiate(amountText, dragIconObj.transform, false);
        dragText.text = dragAmount.ToString();
        dragText.raycastTarget = false;

        // Dim the source icon so it's clear this stack is mid-move
        iconImage.color = new Color(1f, 1f, 1f, 0.35f);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIconObj != null)
        {
            dragIconObj.GetComponent<RectTransform>().position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIconObj != null)
        {
            Destroy(dragIconObj);
        }

        iconImage.color = Color.white;
        dragAmount = 0;
        isAnyDragInProgress = false;
    }

    // How much of the stack this slot is currently carrying mid-drag. Read by the
    // target slot's OnDrop so partial (right-click) drags only move part of the stack.
    public int GetDragAmount()
    {
        return dragAmount;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null)
        {
            return;
        }

        Slot sourceSlot = eventData.pointerDrag.GetComponent<Slot>();

        if (sourceSlot == null || sourceSlot == this || !sourceSlot.HasItem())
        {
            return;
        }

        ItemSO sourceItem = sourceSlot.GetItem();
        int transferAmount = sourceSlot.GetDragAmount();

        if (transferAmount <= 0)
        {
            return;
        }

        // Reject anything that doesn't belong here, e.g. dragging a sword onto a helmet slot
        if (!CanAccept(sourceItem))
        {
            return;
        }

        if (!HasItem())
        {
            // Target is empty, move the dragged amount over. RemoveAmount clears the
            // source automatically if that was the whole stack, and leaves the rest
            // behind otherwise (e.g. a right-click half-stack drag).
            SetItem(sourceItem, transferAmount);
            sourceSlot.RemoveAmount(transferAmount);
        }
        else if (heldItem == sourceItem)
        {
            // Same item in both slots, merge as much as fits and leave the rest behind
            int spaceLeft = sourceItem.maxStackSize - itemAmount;
            int amountToMove = Mathf.Min(spaceLeft, transferAmount);

            if (amountToMove > 0)
            {
                AddAmount(amountToMove);
                sourceSlot.RemoveAmount(amountToMove);
            }
        }
        else
        {
            // Different items: only swap on a full-stack drag, and only if the source slot
            // is also willing to accept what's currently sitting here (e.g. swapping two
            // different helmets between an equipment slot and inventory is fine, but
            // dragging a helmet onto a sword in an unrestricted slot swapping back a sword
            // into the helmet slot would not be, CanAccept on the source catches that).
            if (transferAmount == sourceSlot.GetAmount() && sourceSlot.CanAccept(heldItem))
            {
                ItemSO targetItem = heldItem;
                int targetAmount = itemAmount;

                SetItem(sourceItem, transferAmount);
                sourceSlot.SetItem(targetItem, targetAmount);
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!HasItem() || eventData.button != PointerEventData.InputButton.Left)
        {
            return; // right click is handled by the drag system above (half-stack pickup)
        }

        if (PlayerMovement.IsCtrlHeld() && owningInventory != null)
        {
            owningInventory.DistributeToMatchingSlots(this);
        }
        else if (PlayerMovement.IsShiftHeld() && owningInventory != null)
        {
            owningInventory.QuickTransfer(this, isHotbarSlot);
        }
    }
}