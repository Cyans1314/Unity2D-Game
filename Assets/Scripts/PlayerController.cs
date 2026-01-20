using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI; 

public class Player : MonoBehaviour {
    // 核心组件
    private Rigidbody2D rig;
    private Animator ani; 

    // 出生点
    public Transform spawnPoint;

    // UI
    [Header("--- UI ---")]
    public Slider healthBar; 

    // 音效
    [Header("--- 音效 ---")]
    public AudioSource audioSource; // 喇叭 A (负责短音效)
    
    // 喇叭 B (专门负责跑步，代码自动生成，不用拖)
    private AudioSource runAudioSource; 

    public AudioClip jumpClip;      
    public AudioClip dashClip;      
    public AudioClip attack1Clip;   
    public AudioClip attack2Clip;   
    public AudioClip deathClip;     
    public AudioClip runClip;       

    public void Start() {
        rig = GetComponent<Rigidbody2D>();
        ani = GetComponent<Animator>();
        
        //  获取主喇叭 (喇叭 A)
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // 自动创建一个“副喇叭” (喇叭 B) 专门放跑步声
        runAudioSource = gameObject.AddComponent<AudioSource>();
        runAudioSource.clip = runClip;
        runAudioSource.loop = true; // 跑步声是循环的
        runAudioSource.volume = audioSource.volume; // 音量和主喇叭一样
        runAudioSource.playOnAwake = false;

        // 初始化状态
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
            // 死了只停喇叭 B (跑步声)
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

            // 攻击时只停喇叭 B (跑步声)，不影响挥刀声
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

            // 跑步音效控制 (操作喇叭 B)
            if (isGround && Mathf.Abs(rig.velocity.x) > 0.1f) {
                // 如果没在跑，就开始跑
                if (!runAudioSource.isPlaying) {
                    runAudioSource.clip = runClip; // 确保是跑步声
                    runAudioSource.Play();
                }
            } 
            else {
                // 停下来或跳起来 -> 停止喇叭 B
                if (runAudioSource.isPlaying) {
                    runAudioSource.Stop();
                }
            }
        }
    }

    // 音效播放工具 (使用喇叭 A)
    public void PlaySound(AudioClip clip) {
        if (clip != null && audioSource != null) {
            audioSource.PlayOneShot(clip);
        }
    }

    /******** 移动模块 ***********/
    public float moveSpeed = 3f;
    private float InputX;
    private bool isFlip = false; 

    public void Move(InputAction.CallbackContext context) {
        if (isDead) return; 
        InputX = context.ReadValue<Vector2>().x;
    }


    /******** 跳跃与地面模块 ***********/
    private float[] jumpForces = { 5.2f, 4.2f }; 
    public Transform groundPoint;
    public LayerMask groundMask;
    private bool isGround; 
    private int jumpCount;

    public void Jump(InputAction.CallbackContext context) {
        if (isDead) return; 
        if (context.performed) {
            if (isGround && jumpCount == 0) {
                rig.velocity = new Vector2(rig.velocity.x, jumpForces[0]);
                jumpCount++; 
                PlaySound(jumpClip); // 喇叭 A 响
            } else if (!isGround && jumpCount == 1) {
                rig.velocity = new Vector2(rig.velocity.x, jumpForces[1]);
                jumpCount++; 
                PlaySound(jumpClip); // 喇叭 A 响
            }
        }
    }


    /******** 冲刺模块 ***********/
    public float dashSpeed = 15f;    
    public float dashTime = 0.2f;   
    public float dashCooldown = 1f;  
    private bool isDashing = false;  
    private bool canDash = true;     

    public void Dash(InputAction.CallbackContext context) {
        if (isDead) return; 
        if (context.performed && canDash && !isDashing) {
            StartCoroutine(DashCoroutine());
        }
    }

    private IEnumerator DashCoroutine() {
        isDashing = true;
        canDash = false;
        ani.SetBool("isDash", true); 
        
        // 冲刺时停跑步声 (喇叭 B)
        if (runAudioSource.isPlaying) runAudioSource.Stop();
        
        PlaySound(dashClip); // 冲刺声 (喇叭 A)

        float dir = isFlip ? -1f : 1f; 
        rig.velocity = new Vector2(dir * dashSpeed, 0f);

        yield return new WaitForSeconds(dashTime);

        rig.velocity = Vector2.zero;        
        ani.SetBool("isDash", false);       
        isDashing = false;                  

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }


    /******** 攻击模块 ***********/
    public Transform attackPoint;
    public float attackRange = 1.5f;
    public LayerMask monsterMask;
    public float normalDamage = 10f;      
    public float dashAttackDamage = 30f;

    public void Attack(InputAction.CallbackContext context) {
        if (isDead) return; 
        if (context.performed) {
            ani.SetBool("isDashAttack", false);
            ani.SetBool("isAttack", true);        
            
            // PlaySound(attack1Clip); // 为了解决手速快导致声音重叠，这里不再直接播放，改用动画事件触发
        }
    }  

    // 供动画事件调用的方法
    public void PlayAttack1Sound() {
        PlaySound(attack1Clip);
    }

    public void DashAttack(InputAction.CallbackContext context) {
        if (isDead) return; 
        if (context.performed) {
            ani.SetBool("isAttack", false); 
            ani.SetBool("isDashAttack", true);
        }
    }

    public void PlayAttack2Sound() {
        PlaySound(attack2Clip); // 喇叭 A 响
    }

    //检测攻击范围内是否有敌人
    public void CheckAttack() {
        Collider2D[] detectedObjects = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, monsterMask);
        
        bool isDashAtk = ani.GetBool("isDashAttack");

        float currentDamage = isDashAtk ? dashAttackDamage : normalDamage;

        foreach (Collider2D collider in detectedObjects) {
            Debug.Log("击中: " + collider.gameObject.name);
            collider.gameObject.SendMessage("onDamage", currentDamage);
        } 
    }

    // 结束攻击状态
    public void EndAttack() {
        ani.SetBool("isAttack", false);
        ani.SetBool("isDashAttack", false);
    }


    /******** 受伤与死亡模块 ***********/
    [Header("生命值属性")]
    public float maxHealth = 100f; 
    private float currentHealth;
    private bool isDead = false; 

    public void onDamage(float damage) {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log("Player受到了" + damage + "点伤害，当前血量：" + currentHealth);

        if (healthBar != null) {
            healthBar.value = currentHealth;
        }

        if (currentHealth <= 0) {
            Die();
        } else {
            ani.SetTrigger("isHurt");
        }
    }

    private void Die() {
        // 防止还没死的逻辑继续运行
        StopAllCoroutines(); // 强制停止冲刺协程！
        isDashing = false;   // 强制重置冲刺状态

        isDead = true;
        ani.SetBool("isDead", true); 
        
        // 死亡时清空所有动作指令，防止动画鬼畜
        ani.SetBool("isAttack", false);
        ani.SetBool("isDashAttack", false);
        ani.SetBool("isDash", false);
        ani.SetBool("isRun", false);
        ani.ResetTrigger("isHurt");
        ani.SetBool("isGround", true); // 防止空中死亡卡住
        ani.SetFloat("yVelocity", 0);

        rig.velocity = Vector2.zero;
        rig.bodyType = RigidbodyType2D.Kinematic; 

        // 死了停跑步声 (喇叭 B)
        if (runAudioSource.isPlaying) runAudioSource.Stop();

        PlaySound(deathClip); // 死亡声 (喇叭 A)

        Debug.Log("角色死亡 - Game Over");

        StartCoroutine(RespawnCo());
    }


    // 复活
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

        Debug.Log("复活完成！状态全满！");
    }

    //辅助绘图 (用于在Scene窗口查看攻击范围和地面检测)
    // public void OnDrawGizmos() {
    //     if (groundPoint != null) Gizmos.DrawWireSphere(groundPoint.position, 0.2f);
    //     if (attackPoint != null) Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    // } 

}