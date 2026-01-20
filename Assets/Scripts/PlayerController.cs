/*
 * Script Name: PlayerController.cs
 * Author:      Cyans
 * Affiliation: Chang'an University
 * Date:        November 12, 2025
 * 
 * Description: Main player controller handling movement, jumping, dashing, 
 *              combat, health system, and audio feedback. Supports double jump,
 *              dash mechanics, combo attacks, and respawn system.
 */

using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI; 

public class Player : MonoBehaviour {

    #region --- Inspector Settings ---

    public Transform spawnPoint;

    [Header("UI")]
    public Slider healthBar; 

    [Header("Audio")]
    public AudioSource audioSource;
    
    private AudioSource runAudioSource; 

    public AudioClip jumpClip;      
    public AudioClip dashClip;      
    public AudioClip attack1Clip;   
    public AudioClip attack2Clip;   
    public AudioClip deathClip;     
    public AudioClip runClip;

    public float moveSpeed = 3f;
    private float[] jumpForces = { 5.2f, 4.2f }; 
    public Transform groundPoint;
    public LayerMask groundMask;

    public float dashSpeed = 15f;    
    public float dashTime = 0.2f;   
    public float dashCooldown = 1f;

    public Transform attackPoint;
    public float attackRange = 1.5f;
    public LayerMask monsterMask;
    public float normalDamage = 10f;      
    public float dashAttackDamage = 30f;

    [Header("Health")]
    public float maxHealth = 100f;

    #endregion

    #region --- Internal State ---

    // Core components
    private Rigidbody2D rig;
    private Animator ani; 

    // State Variables
    private float InputX;
    private bool isFlip = false; 
    private bool isGround; 
    private int jumpCount;
    private bool isDashing = false;  
    private bool canDash = true;
    private float currentHealth;
    private bool isDead = false;

    #endregion

    #region --- Unity Lifecycle ---

    public void Start() {
        rig = GetComponent<Rigidbody2D>();
        ani = GetComponent<Animator>();
        
        // Get main speaker (Speaker A)
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        runAudioSource = gameObject.AddComponent<AudioSource>();
        runAudioSource.clip = runClip;
        runAudioSource.loop = true; // Running sound loops
        runAudioSource.volume = audioSource.volume; // Same volume as main speaker
        runAudioSource.playOnAwake = false;

        // Initialize state
        jumpCount = 0;
        currentHealth = maxHealth; 

        if (spawnPoint != null) {
            transform.position = spawnPoint.position;
        }

        if (healthBar != null) {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }
    }

    public void Update() {
        if (isDead) {
            rig.velocity = Vector2.zero;
            // When dead, only stop Speaker B (running sound)
            if (runAudioSource.isPlaying) runAudioSource.Stop();
            return; 
        }

        if (isDashing) {
            return; 
        }

        isGround = Physics2D.OverlapCircle(groundPoint.position, 0.2f, groundMask);
        if (isGround && rig.velocity.y <= 0) {
            jumpCount = 0;
        }

        bool isAttacking = ani.GetBool("isAttack") || ani.GetBool("isDashAttack");

        if (isAttacking) {
            ani.SetFloat("yVelocity", 0); 
            ani.SetBool("isGround", true); 
            ani.SetBool("isRun", false);

            // When attacking, only stop Speaker B
            if (runAudioSource.isPlaying) runAudioSource.Stop();
        } else {
            rig.velocity = new Vector2(moveSpeed * InputX, rig.velocity.y);
            ani.SetBool("isRun", Mathf.Abs(rig.velocity.x) > 0);
            ani.SetFloat("yVelocity", rig.velocity.y);
            ani.SetBool("isGround", isGround);
            
            if (!isFlip) {
                if (rig.velocity.x < 0) {
                    isFlip = true;
                    transform.Rotate(0.0f, 180.0f, 0.0f);
                }
            } else {
                if (rig.velocity.x >  0) {
                    isFlip = false;
                    transform.Rotate(0.0f, 180.0f, 0.0f);
                }
            }

            // Running sound control
            if (isGround && Mathf.Abs(rig.velocity.x) > 0.1f) {
                // If not running, start running
                if (!runAudioSource.isPlaying) {
                    runAudioSource.clip = runClip; // Ensure it's running sound
                    runAudioSource.Play();
                }
            } 
            else {
                // Stop or jump -> stop Speaker B
                if (runAudioSource.isPlaying) {
                    runAudioSource.Stop();
                }
            }
        }
    }

    #endregion

    #region --- Input Handlers ---

    public void Move(InputAction.CallbackContext context) {
        if (isDead) return; 
        InputX = context.ReadValue<Vector2>().x;
    }

    public void Jump(InputAction.CallbackContext context) {
        if (isDead) return; 
        if (context.performed) {
            if (isGround && jumpCount == 0) {
                rig.velocity = new Vector2(rig.velocity.x, jumpForces[0]);
                jumpCount++; 
                PlaySound(jumpClip); // Speaker A plays
            } else if (!isGround && jumpCount == 1) {
                rig.velocity = new Vector2(rig.velocity.x, jumpForces[1]);
                jumpCount++; 
                PlaySound(jumpClip); // Speaker A plays
            }
        }
    }

    public void Dash(InputAction.CallbackContext context) {
        if (isDead) return; 
        if (context.performed && canDash && !isDashing) {
            StartCoroutine(DashCoroutine());
        }
    }

    public void Attack(InputAction.CallbackContext context) {
        if (isDead) return; 
        if (context.performed) {
            ani.SetBool("isDashAttack", false);
            ani.SetBool("isAttack", true);        
            
            // To solve sound overlap from fast input, no longer play directly here, use animation event instead
        }
    }

    public void DashAttack(InputAction.CallbackContext context) {
        if (isDead) return; 
        if (context.performed) {
            ani.SetBool("isAttack", false); 
            ani.SetBool("isDashAttack", true);
        }
    }

    #endregion

    #region --- Coroutines ---

    private IEnumerator DashCoroutine() {
        isDashing = true;
        canDash = false;
        ani.SetBool("isDash", true); 
        
        // Stop running sound during dash (Speaker B)
        if (runAudioSource.isPlaying) runAudioSource.Stop();
        
        PlaySound(dashClip); // Dash sound (Speaker A)

        float dir = isFlip ? -1f : 1f; 
        rig.velocity = new Vector2(dir * dashSpeed, 0f);

        yield return new WaitForSeconds(dashTime);

        rig.velocity = Vector2.zero;        
        ani.SetBool("isDash", false);       
        isDashing = false;                  

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private IEnumerator RespawnCo() {
        yield return new WaitForSeconds(1.1f);

        if (spawnPoint != null) {
            transform.position = spawnPoint.position;
        }

        transform.rotation = Quaternion.identity; 
        isFlip = false; 

        currentHealth = maxHealth;
        
        if (healthBar != null) {
            healthBar.value = maxHealth;
        }

        rig.bodyType = RigidbodyType2D.Dynamic;
        rig.velocity = Vector2.zero;

        ani.SetBool("isDead", false);
        ani.Play("Idle"); 

        canDash = true; 
        isDashing = false; 

        isDead = false;

        Debug.Log("Respawn complete! Full health!");
    }

    #endregion

    #region --- Public & Animation Events ---

    // Sound playback utility
    public void PlaySound(AudioClip clip) {
        if (clip != null && audioSource != null) {
            audioSource.PlayOneShot(clip);
        }
    }

    // Called by animation event
    public void PlayAttack1Sound() {
        PlaySound(attack1Clip);
    }

    // Called by animation event
    public void PlayAttack2Sound() {
        PlaySound(attack2Clip); // Speaker A plays
    }

    // Check if there are enemies within attack range
    public void CheckAttack() {
        Collider2D[] detectedObjects = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, monsterMask);
        
        bool isDashAtk = ani.GetBool("isDashAttack");

        float currentDamage = isDashAtk ? dashAttackDamage : normalDamage;

        foreach (Collider2D collider in detectedObjects) {
            Debug.Log("Hit: " + collider.gameObject.name);
            collider.gameObject.SendMessage("onDamage", currentDamage);
        } 
    }

    // End attack state
    public void EndAttack() {
        ani.SetBool("isAttack", false);
        ani.SetBool("isDashAttack", false);
    }

    /// <summary>
    /// Apply damage to player.
    /// </summary>
    public void onDamage(float damage) {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log("Player took " + damage + " damage, current health: " + currentHealth);

        if (healthBar != null) {
            healthBar.value = currentHealth;
        }

        if (currentHealth <= 0) {
            Die();
        } else {
            ani.SetTrigger("isHurt");
        }
    }

    #endregion

    #region --- Helper Methods ---

    private void Die() {
        // Prevent logic from continuing before death
        StopAllCoroutines(); // Force stop dash coroutine!
        isDashing = false;   // Force reset dash state

        isDead = true;
        ani.SetBool("isDead", true); 
        
        // Clear all action commands on death to prevent animation glitches
        ani.SetBool("isAttack", false);
        ani.SetBool("isDashAttack", false);
        ani.SetBool("isDash", false);
        ani.SetBool("isRun", false);
        ani.ResetTrigger("isHurt");
        ani.SetBool("isGround", true); // Prevent getting stuck when dying in air
        ani.SetFloat("yVelocity", 0);

        rig.velocity = Vector2.zero;
        rig.bodyType = RigidbodyType2D.Kinematic; 

        // When dead, stop running sound (Speaker B)
        if (runAudioSource.isPlaying) runAudioSource.Stop();

        PlaySound(deathClip); // Death sound (Speaker A)

        Debug.Log("Player died - Game Over");

        StartCoroutine(RespawnCo());
    }

    #endregion

    // Helper drawing (for viewing attack range and ground detection in Scene window)
    // public void OnDrawGizmos() {
    //     if (groundPoint != null) Gizmos.DrawWireSphere(groundPoint.position, 0.2f);
    //     if (attackPoint != null) Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    // } 
}
