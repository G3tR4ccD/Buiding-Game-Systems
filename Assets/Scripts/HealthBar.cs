using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Drives a fill-image health bar from a Health component. Subscribes to
// Health.OnHealthChanged rather than polling, same pattern Slot uses for its own UI.
//
// Setup in the Inspector:
// - UI_StatusBar_BG: a plain Image sized to the full bar. Not referenced by this
//   script at all, it just sits behind the fill as the "empty" backdrop.
// - UI_StatusBar_Fill_HP: an Image placed on top of the BG, Source Image set to that
//   sprite, Image Type set to "Filled", Fill Method "Horizontal" (or "Radial 360" if
//   the sprite's a circular bar). Drag this Image into fillImage below.
// - amountText (optional): a TextMeshProUGUI showing "current/max", e.g. "72/100".
//   Leave empty if you don't want the number, the bar itself works fine without it.
// - Drag the player's Health component into health.
[DisallowMultipleComponent]
public class HealthBar : MonoBehaviour
{
    public Health health;
    public Image fillImage; // UI_StatusBar_Fill_HP - Image Type must be "Filled"
    public TextMeshProUGUI amountText; // Optional - shows "current/max" as whole numbers

    [Header("Smoothing")]
    public bool smoothFill = true;
    public float fillSpeed = 5f; // Higher = the bar catches up to real health faster

    private float targetFillAmount = 1f;

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnHealthChanged += HandleHealthChanged;
            HandleHealthChanged(health.GetCurrentHealth(), health.GetMaxHealth());
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        targetFillAmount = max > 0f ? current / max : 0f;

        if (!smoothFill && fillImage != null)
        {
            fillImage.fillAmount = targetFillAmount;
        }

        if (amountText != null)
        {
            // Rounded to whole numbers for display, e.g. "72/100" rather than "72.4/100"
            amountText.text = Mathf.CeilToInt(current) + "/" + Mathf.CeilToInt(max);
        }
    }

    private void Update()
    {
        if (!smoothFill || fillImage == null)
        {
            return;
        }

        fillImage.fillAmount = Mathf.Lerp(fillImage.fillAmount, targetFillAmount, fillSpeed * Time.deltaTime);
    }
}