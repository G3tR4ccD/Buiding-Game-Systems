using UnityEngine;

// Tracks a single float health value with a max, and fires an event whenever it
// changes so UI (HealthBar) or other systems can react without polling every frame.
// Drop this on the player (or any damageable object) alongside HealthBar's target.
[DisallowMultipleComponent]
public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;

    // (current, max), fired on Start and on every TakeDamage/Heal call
    public event System.Action<float, float> OnHealthChanged;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void Heal(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public bool IsDead()
    {
        return currentHealth <= 0f;
    }

    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
}
