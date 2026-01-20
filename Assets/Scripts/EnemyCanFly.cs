/*
 * Script Name: EnemyCanFly.cs
 * Author:      Cyans
 * Affiliation: Chang'an University
 * Date:        November 14, 2025
 * 
 * Description: Flying enemy  with hover physics, patrol behavior, combat system,
 *              and retreat mechanics. Uses spring-based hovering simulation and
 *              smooth movement for realistic flight behavior.
 */

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class EnemyCanFly : MonoBehaviour {

    #region --- Inspector Settings ---

    [Header("Base Stats")]
    [Tooltip("Max health points")]
    public float maxHealth = 50f;
    
    [Tooltip("Patrol flight speed")]
    public float moveSpeed = 4f; 

    [Tooltip("Speed multiplier when chasing player")]
    public float chaseSpeedMultiplier = 1.5f;
    
    [Tooltip("Base attack damage")]
    public float damage = 10f;
    
    [Tooltip("Patrol area rectangle (X=width, Y=height)")]
    public Vector2 patrolBoxSize = new Vector2(10f, 6f);
    
    [Tooltip("Enable patrol behavior")]
    public bool canPatrol = true;

    [Header("Flight Physics")]
    [Tooltip("Movement smoothness (higher = more inertia)")]
    public float movementSmoothTime = 0.5f;

    [Tooltip("Hover bobbing amplitude")]
    public float hoverBobAmount = 0.5f;

    [Tooltip("Hover bobbing frequency")]
    public float hoverBobSpeed = 2.0f;

    [Tooltip("Hover spring force strength")]
    public float springStrength = 1000f; 
    
    [Tooltip("Hover damping")]
    public float springDamping = 15f;
    
    [Tooltip("Combat hover height")]
    public float hoverHeight = 3.5f;

    [Header("Combat Configuration")]
    [Tooltip("Melee range distance")]
    public float closeRange = 1.0f;   
    
    [Tooltip("Chase and hover trigger distance")]
    public float farRange = 7.0f;     
    
    [Tooltip("Combo attack interval")]
    public float comboInterval = 0.4f; 
    
    [Tooltip("Retreat flight duration after combo")]
    public float retreatDuration = 2.0f; 

    [Header("Misc Settings")]
    [Tooltip("HP regen per second out of combat")]
    public float healRate = 5f;
    
    [Tooltip("Attack detection center point")]
    public Transform attackPoint;
    
    [Tooltip("Attack detection radius")]
    public float hitRadius = 0.6f;
    
    [Tooltip("Player layer mask")]
    public LayerMask playerLayer;

    [Tooltip("Ground layer for death landing detection")]
    public LayerMask groundLayer;

    #endregion

    #region --- Internal State ---

    // References
    private Transform player;   
    private Animator ani;       
    private Rigidbody2D rb;     
    
    // State Variables
    private Vector2 startPoint; 
    private float currentHealth; 
    private float nextActionTime = 0f; 
    
    // Hover physics variables
    private float targetHeightY; 
    private float smoothedHeightY;

    // Smooth movement velocity cache
    private float currentVelocityX; 

    // Patrol variables
    private Vector2 currentPatrolTarget;
    private float patrolWaitTime = 0f;

    // Status locks
    private bool isRetreating = false;
    private float retreatEndTime = 0f;

    // Status Flags
    private bool isDead = false;         
    private bool isFallingToDie = false; 
    private bool isFacingRight = false; 
    private int comboCount = 0; 
    private bool initialFacingRight; 

    #endregion

    #region --- Unity Lifecycle ---

    void Start() {
        currentHealth = maxHealth;
        startPoint = transform.position;
        targetHeightY = transform.position.y;
        smoothedHeightY = transform.position.y;
        currentPatrolTarget = startPoint;
        
        ani = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) {
            player = p.transform;
        } else {
            Debug.LogWarning("[EnemyCanFly] Warning: Player tag not found.");
        }

        if (transform.localScale.x < 0) {
            isFacingRight = false;
        } else {
            isFacingRight = true;
        }
        
        initialFacingRight = isFacingRight;
    }

    void FixedUpdate() {
        // If falling to death or already dead, stop applying hover force
        if (isDead || isFallingToDie) return;

        smoothedHeightY = Mathf.MoveTowards(smoothedHeightY, targetHeightY, moveSpeed * Time.fixedDeltaTime);

        // Use sine wave to add small up-down float, simulating creature instability while hovering
        float bobbingOffset = Mathf.Sin(Time.time * hoverBobSpeed) * hoverBobAmount;
        float finalTargetY = smoothedHeightY + bobbingOffset;

        // Spring force calculation
        float forceY = 0f;
        float yDifference = finalTargetY - transform.position.y;

        float springForce = yDifference * springStrength;
        // Apply damping force to prevent shaking
        float dampingForce = -rb.velocity.y * springDamping;

        // Add gravity compensation
        forceY = springForce + dampingForce + (rb.mass * Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale);

        rb.AddForce(new Vector2(0f, forceY));
    }

    void Update() {
        // Stop logic update when dead
        if (isDead || isFallingToDie || player == null) return;

        AnimatorStateInfo stateInfo = ani.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("isHurt") && stateInfo.normalizedTime < 1.0f) {
            return; 
        }

        if (Time.time < nextActionTime) {
            ani.SetBool("isRun", false);
            return;
        }

        if (isRetreating) {
            if (Time.time > retreatEndTime) {
                isRetreating = false;
            } else {
                PerformRetreat();
                return;
            }
        }
        
        float distX = Mathf.Abs(player.position.x - startPoint.x);
        float distY = Mathf.Abs(player.position.y - startPoint.y);
        bool playerInZone = (distX < patrolBoxSize.x) && (distY < patrolBoxSize.y);
        
        if (playerInZone) {
            LookAtPlayer(); 
            HandleCombatLogic(Vector2.Distance(transform.position, player.position));
        } else {
            HandlePeaceState();
        }
    }

    #endregion

    #region ---  Logic ---

    // Generic smooth movement function - uses SmoothDamp to simulate inertia movement
    private void SmoothMoveToX(float targetX, float speed) {
        float newX = Mathf.SmoothDamp(transform.position.x, targetX, ref currentVelocityX, movementSmoothTime, speed);
        transform.position = new Vector2(newX, transform.position.y);
    }

    private void HandleCombatLogic(float dist) {
        if (dist > farRange) {
            if (comboCount != 0) comboCount = 0; 
            ChasePlayer(true); 
        }
        else if (dist <= closeRange) {
            ani.SetBool("isRun", false);
            PerformComboLogic();
        }
        else {
            if (comboCount != 0) comboCount = 0;
            ChasePlayer(false); 
        }
    }

    private void PerformComboLogic() {
        if (comboCount < 2) {
            ani.SetTrigger("isAttack1");
            comboCount++; 
            nextActionTime = Time.time + comboInterval; 
        } else {
            ani.SetTrigger("isAttack2");
            comboCount = 0; 
            nextActionTime = Time.time + 1.0f; 
            TriggerRetreat();
        }
    }

    private void TriggerRetreat() {
        isRetreating = true;
        retreatEndTime = Time.time + retreatDuration;
    }

    private void PerformRetreat() {
        ani.SetBool("isRun", true);

        float retreatDirX = isFacingRight ? -1f : 1f;
        float targetX = transform.position.x + retreatDirX * 5f;
        
        // Speed up slightly during retreat
        SmoothMoveToX(targetX, moveSpeed * 1.2f);

        targetHeightY = startPoint.y + hoverHeight;
    }

    private void HandlePeaceState() {
        comboCount = 0; 

        if (canPatrol) {
            PatrolRect(); 
        } else {
            ReturnToStart();
        }
        
        if (currentHealth < maxHealth) {
            currentHealth += healRate * Time.deltaTime;
            if (currentHealth > maxHealth) currentHealth = maxHealth;
        }
    }

    // Chase movement - apply speed multiplier during combat
    private void ChasePlayer(bool isHovering) {
        ani.SetBool("isRun", true);
        
        float actualSpeed = moveSpeed * chaseSpeedMultiplier;
        SmoothMoveToX(player.position.x, actualSpeed);

        if (isHovering) {
            targetHeightY = player.position.y + hoverHeight;
        } else {
            // Leave some height during dive attack to prevent clipping
            targetHeightY = player.position.y + 0.5f;
        }
    }

    // Rectangular random patrol
    private void PatrolRect() {
        ani.SetBool("isRun", true);

        float distToTargetX = Mathf.Abs(transform.position.x - currentPatrolTarget.x);
        float distToTargetY = Mathf.Abs(transform.position.y - currentPatrolTarget.y);

        if (distToTargetX < 0.5f && distToTargetY < 1.5f) {
            ani.SetBool("isRun", false);
            
            if (Time.time > patrolWaitTime) {
                GetNewPatrolPoint();
            }
        } else {
            // Use normal speed for patrol
            SmoothMoveToX(currentPatrolTarget.x, moveSpeed);
            targetHeightY = currentPatrolTarget.y;

            if (currentPatrolTarget.x > transform.position.x && !isFacingRight) Flip();
            else if (currentPatrolTarget.x < transform.position.x && isFacingRight) Flip();
        }
    }

    // Get new patrol point - includes anti-backtrack logic
    private void GetNewPatrolPoint() {
        Vector2 candidatePoint = Vector2.zero;
        int attempts = 0;
        
        // Try to find a point far enough from current position to prevent jittering
        while (attempts < 10) {
            float randomX = Random.Range(-patrolBoxSize.x, patrolBoxSize.x);
            float randomY = Random.Range(-patrolBoxSize.y, patrolBoxSize.y);
            candidatePoint = startPoint + new Vector2(randomX, randomY);
            
            // Accept if new point is far enough from current position
            if (Vector2.Distance(transform.position, candidatePoint) > 3.0f) {
                break;
            }
            attempts++;
        }
        
        currentPatrolTarget = candidatePoint;
        patrolWaitTime = Time.time + Random.Range(2.0f, 4.0f);
    }

    private void ReturnToStart() {
        float dist = Vector2.Distance(transform.position, startPoint);
        
        if (dist < 0.5f) {
            ani.SetBool("isRun", false);
            targetHeightY = startPoint.y;
            
            if (isFacingRight != initialFacingRight) {
                Flip();
            }
        } else {
            ani.SetBool("isRun", true);
            SmoothMoveToX(startPoint.x, moveSpeed);
            targetHeightY = startPoint.y;
            
            if (startPoint.x > transform.position.x && !isFacingRight) Flip();
            else if (startPoint.x < transform.position.x && isFacingRight) Flip();
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
    /// Take damage and interrupt current action.
    /// </summary>
    public void onDamage(float dmg) {
        if (isDead || isFallingToDie) return;
        
        currentHealth -= dmg;
        ani.SetTrigger("isHurt");
        
        ani.ResetTrigger("isAttack1");
        ani.ResetTrigger("isAttack2");
        comboCount = 0;
        isRetreating = false; 
        
        if (currentHealth <= 0) Die();
    }

    // Called via Animation Event
    public void CheckHit() {
        Collider2D hitPlayer = Physics2D.OverlapCircle(attackPoint.position, hitRadius, playerLayer);
        if (hitPlayer != null) hitPlayer.GetComponent<Player>()?.onDamage(damage);
    }

    // Called via Animation Event
    public void CheckHit2() {
        Collider2D hitPlayer = Physics2D.OverlapCircle(attackPoint.position, hitRadius + 0.2f, playerLayer);
        if (hitPlayer != null) hitPlayer.GetComponent<Player>()?.onDamage(damage * 1.5f);
    }

    // Death phase 1: Lose power and fall
    private void Die() {
        isFallingToDie = true;
        ani.SetBool("isRun", false); 
        rb.velocity = Vector2.zero;
        
        // Give slight upward velocity to simulate being shot down
        rb.AddForce(Vector2.up * 5f, ForceMode2D.Impulse);
        
        // Ensure gravity takes effect to make it fall
        rb.gravityScale = 3.0f; 
    }

    // Collision detection - used to detect corpse landing
    private void OnCollisionEnter2D(Collision2D collision) {
        // If falling and hit the ground
        if (isFallingToDie && !isDead) {
            // Check if collider belongs to ground layer
            if (((1 << collision.gameObject.layer) & groundLayer) != 0) {
                PerformFinalDeath();
            }
        }
    }

    // Death phase 2: Land, play animation and destroy
    private void PerformFinalDeath() {
        isDead = true;
        ani.SetBool("isDead", true);
        
        // Stop physics movement to prevent corpse sliding
        rb.velocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static; 
        
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
        
        Vector3 center = Application.isPlaying ? (Vector3)startPoint : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, new Vector3(patrolBoxSize.x * 2, patrolBoxSize.y * 2, 0));
    }

    #endregion
}
