using UnityEngine;
using UnityEngine.UI;

// A lightweight, non-functional companion to a real hotbar Slot: just a number label
// (e.g. "1"-"5") that lights up in sync with its slot's selection highlight. Doesn't
// hold items and isn't a Slot itself, so it can never get mistaken for real inventory
// storage the way a duplicated Slot component sitting under hotbarObj could.
public class SlotNumberLabel : MonoBehaviour
{
    private Image backgroundImage;
    private Color defaultColor;

    // Defaults to match Slot's own selectedColor; change independently if you want
    // the number label to light up differently than the slot background itself.
    public Color selectedColor = new Color(1f, 0.92f, 0.5f, 1f);

    private void Awake()
    {
        backgroundImage = GetComponent<Image>();

        if (backgroundImage != null)
        {
            defaultColor = backgroundImage.color;
        }
    }

    public void SetHighlighted(bool highlighted)
    {
        if (backgroundImage == null)
        {
            return;
        }

        backgroundImage.color = highlighted ? selectedColor : defaultColor;
    }
}
