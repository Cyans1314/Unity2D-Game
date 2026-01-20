/*
 * Script Name: Enemy.cs
 * Author:      Cyans
 * Affiliation: Chang'an University
 * Date:        November 13, 2025
 * 
 * Description: Ground-based enemy AI with patrol behavior, combat system including
 *              combo attacks and dash mechanics, health regeneration, and dynamic
 *              facing direction management.
 */

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class Enemy : MonoBehaviour {

    #region --- Inspector Settings ---

    [Header("Base Stats")]
    [Tooltip("Max health points")]
    public float maxHealth = 50f;
    
    [Tooltip("Movement speed (units/sec)")]
    public float moveSpeed = 4f; 
    
    [Tooltip("Base attack damage")]
    public float damage = 10f;
    
    [Tooltip("Patrol radius from spawn point")]
    public float patrolRange = 10f;
    
    [Tooltip("Enable patrol behavior (uncheck for stationary enemy)")]
    public bool canPatrol = true;

    [Header("Combat Configuration")]
    [Tooltip("Melee range distance (triggers combo attacks)")]
    public float closeRange = 0.8f;   
    
    [Tooltip("Dash attack trigger distance")]
    public float farRange = 3.0f;     
    
    [Tooltip("Combo attack interval (lower = faster attacks)")]
    public float comboInterval = 0.4f; 
    
    [Tooltip("Dash skill cooldown")]
    public float dashCooldown = 3.0f; 
    
    [Tooltip("Recovery time after full combo")]
    public float fullCooldown = 1.0f; 

    [Header("Misc Settings")]
    [Tooltip("HP regen per second out of combat")]
    public float healRate = 5f;
    
    [Tooltip("Attack detection center point (drag child object here)")]
    public Transform attackPoint;
    
    [Tooltip("Attack detection radius")]
    public float hitRadius = 0.6f;
    
    [Tooltip("Player layer mask for damage detection")]
    public LayerMask playerLayer;

    #endregion

    #region --- Internal State ---

    // References
    private Transform player;   
    private Animator ani;       
    private Rigidbody2D rb;     
    
    // State Variables
    private Vector3 startPoint; 
    private float currentHealth; 
    private float nextActionTime = 0f; 
    private float nextDashTime = 0f;

    // Status Flags
    private bool isDead = false; 
    private bool isFacingRight = false; 
    private bool movingLeft = true;     
    private int comboCount = 0; 
    private bool initialFacingRight; 

    #endregion

    #region --- Unity Lifecycle ---

    void Start() {
        currentHealth = maxHealth;
        startPoint = transform.position; 
        
        ani = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) {
            player = p.transform;
        } else {
            Debug.LogWarning("[Enemy] Warning: Player tag not found, AI will not function.");
        }

        if (transform.localScale.x < 0) {
            isFacingRight = false;
            movingLeft = true;
        } else {
            isFacingRight = true;
            movingLeft = false;
        }
        
        initialFacingRight = isFacingRight;
    }

    void Update() {
        if (isDead || player == null) return;

        // Interrupt logic if currently in Hurt state
        AnimatorStateInfo stateInfo = ani.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("isHurt") && stateInfo.normalizedTime < 1.0f) {
            rb.velocity = Vector2.zero;
            return; 
        }

        // Action Lock Check
        if (Time.time < nextActionTime) {
            StopMovement();
            return;
        }
        
        float distToPlayer = Vector2.Distance(transform.position, player.position);
        bool playerInZone = Mathf.Abs(player.position.x - startPoint.x) < patrolRange;
        
        if (playerInZone) {
            LookAtPlayer(); 
            HandleCombatLogic(distToPlayer);
        } else {
            HandlePeaceState();
        }
    }

    #endregion

    #region --- AI Logic ---

    private void HandleCombatLogic(float dist) {
        // Far Range: Dash or Chase
        if (dist > farRange) {
            if (comboCount != 0) comboCount = 0; 

            if (Time.time >= nextDashTime) {
                StopMovement();
                PerformDashAttack();
            } else {
                ChasePlayer();
            }
        }
        // Close Range: Melee Combo
        else if (dist <= closeRange) {
            StopMovement();
            PerformComboLogic();
        }
        // Mid Range: Chase
        else {
            if (comboCount != 0) comboCount = 0;
            ChasePlayer();
        }
    }

    private void PerformComboLogic() {
        if (comboCount < 3) {
            ani.SetTrigger("isAttack1");
            comboCount++; 
            nextActionTime = Time.time + comboInterval; 
        } else {
            ani.SetTrigger("isAttack2"); // Finisher
            comboCount = 0; 
            nextActionTime = Time.time + fullCooldown; 
        }
    }

    private void PerformDashAttack() {
        Debug.Log("[Enemy] Dash attack triggered");
        ani.SetTrigger("isAttack2");
        nextActionTime = Time.time + 1.0f; // Hardcoded dash duration
        nextDashTime = Time.time + dashCooldown;
    }

    private void HandlePeaceState() {
        comboCount = 0;

        if (canPatrol) {
            Patrol(); 
        } else {
            ReturnToStart();
        }
        
        if (currentHealth < maxHealth) {
            int lastHealthInt = (int)currentHealth;
            currentHealth += healRate * Time.deltaTime;
            if (currentHealth > maxHealth) currentHealth = maxHealth;
            if ((int)currentHealth != lastHealthInt) {
                Debug.Log($"[{gameObject.name}] Regenerating... Current health: {currentHealth}");
            }
        }
    }

    private void ChasePlayer() {
        ani.SetBool("isRun", true);
        transform.position = Vector2.MoveTowards(transform.position, new Vector2(player.position.x, transform.position.y), moveSpeed * Time.deltaTime);
    }

    private void StopMovement() {
        rb.velocity = Vector2.zero;
        ani.SetBool("isRun", false);
    }

    private void ReturnToStart() {
        float dist = Vector2.Distance(transform.position, startPoint);
        
        if (dist < 0.1f) {
            StopMovement();
            ani.Play("Idle");

            // Revert to initial facing direction
            if (isFacingRight != initialFacingRight) {
                Flip();
            }

        } else {
            ani.SetBool("isRun", true);
            transform.position = Vector2.MoveTowards(transform.position, startPoint, moveSpeed * Time.deltaTime);
            
            if (startPoint.x > transform.position.x && !isFacingRight) Flip();
            else if (startPoint.x < transform.position.x && isFacingRight) Flip();
        }
    }

    private void Patrol() {
        ani.SetBool("isRun", true);
        
        float leftBorder = startPoint.x - patrolRange;
        float rightBorder = startPoint.x + patrolRange;

        if (movingLeft) {
            transform.position += Vector3.left * moveSpeed * Time.deltaTime; 
            if (isFacingRight) Flip(); 
            if (transform.position.x <= leftBorder) movingLeft = false; 
        } else {
            transform.position += Vector3.right * moveSpeed * Time.deltaTime; 
            if (!isFacingRight) Flip(); 
            if (transform.position.x >= rightBorder) movingLeft = true;
        }
    }

    private void LookAtPlayer() {
        if (player.position.x > transform.position.x && !isFacingRight) Flip();
        else if (player.position.x < transform.position.x && isFacingRight) Flip();
    }

    private void Flip() {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    #endregion

    #region --- Public & Animation Events ---

    /// <summary>
    /// Apply damage to this enemy entity.
    /// </summary>
    public void onDamage(float dmg) {
        if (isDead) return;
        currentHealth -= dmg;
        Debug.Log($"[{gameObject.name}] Took {dmg} damage, remaining health: {currentHealth}");
        ani.SetTrigger("isHurt");
        
        // Reset combat state
        ani.ResetTrigger("isAttack1");
        ani.ResetTrigger("isAttack2");
        comboCount = 0;
        
        if (currentHealth <= 0) Die();
    }

    // Called via Animation Event (Normal Attack)
    public void CheckHit() {
        Collider2D hitPlayer = Physics2D.OverlapCircle(attackPoint.position, hitRadius, playerLayer);
        if (hitPlayer != null) {
            Player p = hitPlayer.GetComponent<Player>();
            if (p != null) {
                p.onDamage(damage);
                Debug.Log($"[Enemy] Normal attack hit: {p.name}");
            }
        }
    }

    // Called via Animation Event (Heavy Attack)
    public void CheckHit2() {
        Collider2D hitPlayer = Physics2D.OverlapCircle(attackPoint.position, hitRadius + 0.2f, playerLayer);
        if (hitPlayer != null) {
            Player p = hitPlayer.GetComponent<Player>();
            if (p != null) {
                p.onDamage(damage * 1.5f);
                Debug.Log($"[Enemy] Heavy attack hit: {p.name}");
            }
        }
    }

    private void Die() {
        isDead = true;
        ani.SetBool("isDead", true);
        rb.velocity = Vector2.zero;
        rb.simulated = false;
        
        GetComponent<Collider2D>().enabled = false;
        Destroy(gameObject, 2f);
    }

    #endregion

    #region --- Debug ---

    void OnDrawGizmosSelected() {
        if (attackPoint != null) {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, hitRadius);
        }
        
        Vector3 center = Application.isPlaying ? startPoint : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(center.x - patrolRange, center.y, 0), new Vector3(center.x + patrolRange, center.y, 0));
    }

    #endregion
}
