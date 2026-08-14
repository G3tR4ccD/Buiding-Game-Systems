using UnityEngine;

// Three-hit combo: bite right -> bite left -> paws, then wraps back to the start.
// Each Fire press advances one step; missing the follow-up window resets the combo
// instead of chaining, same idea most action games use for light-attack strings.
//
// Setup in the Inspector:
// - Drag the character's Animator into animator.
// - On PlayerMovement, set attackCombo to this component, and make sure the Fire
//   Action's Unity Event calls PlayerMovement.OnFire (it forwards presses here).
// - The three state names below must match the states in the Animator Controller
//   exactly (state name, not just the clip dragged into it, if they differ).
// - Set each clip's actual length (in seconds) in the three duration fields below.
//   Check a clip's length in the Animation import tab on the FBX, or the Animator
//   window while it's playing. This avoids needing an Animation Event on an imported
//   FBX clip, which Unity can silently wipe on re-import.
[DisallowMultipleComponent]
public class AttackCombo : MonoBehaviour
{
    public Animator animator;

    [Header("Combo Clips (in order)")]
    public string biteRightState = "Rac_Attack Bite Right";
    public string biteLeftState = "Rac_Attack Bite Left";
    public string pawsState = "Rac_Attack Paws";

    [Header("Clip Durations (seconds)")]
    public float biteRightDuration = 0.6f; // Match these to each clip's real length
    public float biteLeftDuration = 0.6f;
    public float pawsDuration = 0.8f;

    [Header("Timing")]
    public float comboResetTime = 1f;   // How long after a swing ends the combo stays "live" before falling back to step 0
    public float attackCooldown = 0.2f; // Minimum gap between accepted presses, guards against a single click double-firing

    private int comboStep = 0;       // 0 = next press is bite right, 1 = bite left, 2 = paws
    private float comboTimer = 0f;   // Counts down once a swing finishes; combo resets when it hits 0
    private float cooldownTimer = 0f;
    private float attackTimer = 0f;  // Counts down while a swing is playing; attacking ends when it hits 0
    private bool attacking = false;  // True for the duration of a swing, blocks overlapping presses

    private void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if (attacking)
        {
            attackTimer -= Time.deltaTime;

            if (attackTimer <= 0f)
            {
                attacking = false;
                comboTimer = comboResetTime;
            }
        }
        // Only ticks down between swings, not during one, so a slow player mid-combo
        // doesn't get punished for how long the animation itself takes to play.
        else if (comboStep > 0)
        {
            comboTimer -= Time.deltaTime;

            if (comboTimer <= 0f)
            {
                comboStep = 0;
            }
        }
    }

    // Called by PlayerMovement.OnFire. Ignored mid-swing or during cooldown, so mashing
    // the button can't cut an attack short or skip ahead in the combo.
    public void TryAttack()
    {
        if (attacking || cooldownTimer > 0f || animator == null)
        {
            return;
        }

        string stateToPlay;
        float duration;

        switch (comboStep)
        {
            case 0:
                stateToPlay = biteRightState;
                duration = biteRightDuration;
                break;
            case 1:
                stateToPlay = biteLeftState;
                duration = biteLeftDuration;
                break;
            default:
                stateToPlay = pawsState;
                duration = pawsDuration;
                break;
        }

        animator.Play(stateToPlay, 0, 0f);

        cooldownTimer = attackCooldown;
        attackTimer = duration;
        attacking = true;
        comboStep = (comboStep + 1) % 3; // wraps: after paws, the next press starts back at bite right
    }
}