using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class Enemy : MonoBehaviour {

    [Header("--- 基础数值 ---")]
    [Tooltip("怪物的最大生命值")]
    public float maxHealth = 50f;
    
    [Tooltip("怪物每秒移动的单位距离")]
    public float moveSpeed = 4f; 
    
    [Tooltip("怪物的基础攻击力")]
    public float damage = 10f;
    
    [Tooltip("巡逻范围半径，以出生点为中心")]
    public float patrolRange = 10f;
    
    [Tooltip("是否启用巡逻行为，不勾选则为站桩怪")]
    public bool canPatrol = true;

    [Header("--- 战斗逻辑配置 ---")]
    [Tooltip("近战贴脸距离，小于此距离触发普攻连招")]
    public float closeRange = 0.8f;   
    
    [Tooltip("远距离突刺判定距离，大于此距离尝试发动突刺")]
    public float farRange = 3.0f;     
    
    [Tooltip("普攻连打间隔，数值越小攻速越快")]
    public float comboInterval = 0.4f; 
    
    [Tooltip("突刺技能的冷却时间")]
    public float dashCooldown = 3.0f; 
    
    [Tooltip("一套连招结束后的硬直休息时间")]
    public float fullCooldown = 1.0f; 

    [Header("--- 其他配置 ---")]
    [Tooltip("脱战后的每秒回血量")]
    public float healRate = 5f;
    
    [Tooltip("攻击判定的中心点，需拖拽子物体")]
    public Transform attackPoint;
    
    [Tooltip("攻击判定的圆形半径")]
    public float hitRadius = 0.6f;
    
    [Tooltip("玩家所在的图层，用于伤害检测")]
    public LayerMask playerLayer;

    // 内部变量
    private Transform player;   
    private Animator ani;       
    private Rigidbody2D rb;     
    
    private Vector3 startPoint; 
    private float currentHealth; 
    
    // 动作锁：在此时间前禁止移动和攻击
    private float nextActionTime = 0f; 
    // 突刺冷却锁：在此时间前禁止再次突刺
    private float nextDashTime = 0f;

    private bool isDead = false; 
    private bool isFacingRight = false; 
    private bool movingLeft = true;     
    private int comboCount = 0; 

    // 初始朝向
    private bool initialFacingRight; 

    /**
    * 初始化组件与状态
    */
    void Start() {
        // 初始化基础状态
        currentHealth = maxHealth;
        startPoint = transform.position; 
        
        // 获取自身组件
        ani = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        
        // 获取玩家引用，若未找到则报警
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) {
            player = p.transform;
        } else {
            Debug.LogWarning("[Enemy] 警告：未找到 Player 标签的物体，将无法运行。");
        }

        if (transform.localScale.x < 0) {
            // X为负数 -> 脸朝左
            isFacingRight = false;
            movingLeft = true;
        } else {
            // X为正数 -> 脸朝右
            isFacingRight = true;
            movingLeft = false;
        }
        
        //记录初始朝向
        initialFacingRight = isFacingRight;
    }

    /**
    * 每帧逻辑更新
    */
    void Update() {
        // 若死亡或找不到玩家，停止一切逻辑
        if (isDead || player == null) return;

        // 受伤硬直判定：若正在播放受伤动画且未结束，强制停止移动并跳过逻辑
        AnimatorStateInfo stateInfo = ani.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("isHurt") && stateInfo.normalizedTime < 1.0f) {
            rb.velocity = Vector2.zero;
            return; 
        }

        // 攻击动作硬直判定：在攻击后摇结束前，禁止移动和新指令
        if (Time.time < nextActionTime) {
            StopMovement();
            return;
        }
        
        // 计算距离数据
        float distToPlayer = Vector2.Distance(transform.position, player.position);
        bool playerInZone = Mathf.Abs(player.position.x - startPoint.x) < patrolRange;
        
        // 根据玩家是否在领地内切换战斗/和平状态
        if (playerInZone) {
            LookAtPlayer(); 
            HandleCombatLogic(distToPlayer);
        } else {
            HandlePeaceState();
        }
    }

    /**
    * 战斗状态决策逻辑
    */
    private void HandleCombatLogic(float dist) {
        // 距离较远时，优先判断是否可以使用突刺技能
        if (dist > farRange) {
            // 切换策略时重置近战连招计数
            if (comboCount != 0) comboCount = 0; 

            // 检查突刺技能是否冷却完毕
            if (Time.time >= nextDashTime) {
                StopMovement();
                PerformDashAttack();
            } else {
                // 冷却未好，继续移动追击
                ChasePlayer();
            }
        }
        // 距离贴脸时，停止移动并执行近战连招
        else if (dist <= closeRange) {
            StopMovement();
            PerformComboLogic();
        }
        // 距离尴尬（中距离）时，继续移动追击
        else {
            if (comboCount != 0) comboCount = 0;
            ChasePlayer();
        }
    }

    /**
    * 执行近战连招：3次普攻 + 1次重击
    */
    private void PerformComboLogic() {
        if (comboCount < 3) {
            // 前三下：普攻，间隔较短
            ani.SetTrigger("isAttack1");
            comboCount++; 
            nextActionTime = Time.time + comboInterval; 
        } else {
            // 第四下：终结技，重置计数并给予较长硬直
            ani.SetTrigger("isAttack2");
            comboCount = 0; 
            nextActionTime = Time.time + fullCooldown; 
        }
    }

    /**
    * 执行突刺攻击
    */
    private void PerformDashAttack() {
        Debug.Log("[Enemy] 触发突刺技能");
        ani.SetTrigger("isAttack2");
        
        // 设置动作硬直，防止滑步
        nextActionTime = Time.time + 1.0f;
        // 设置技能冷却，防止连续滥用突刺
        nextDashTime = Time.time + dashCooldown;
    }

    /**
    * 脱战与巡逻逻辑
    */
    private void HandlePeaceState() {
        comboCount = 0; // 脱战重置连招

        if (canPatrol) {
            Patrol(); 
        } else {
            ReturnToStart();
        }
        
        // 若血量不满，进行自动回血
        if (currentHealth < maxHealth) {
            int lastHealthInt = (int)currentHealth; // debug
            currentHealth += healRate * Time.deltaTime;
            if (currentHealth > maxHealth) currentHealth = maxHealth;
            if ((int)currentHealth != lastHealthInt) {
                Debug.Log($"[{gameObject.name}] 正在回血... 当前血量: {currentHealth}");
            }
        }
    }

    /**
    * 追逐移动
    */
    private void ChasePlayer() {
        ani.SetBool("isRun", true);
        // 使用 MoveTowards 平滑移动至玩家位置
        transform.position = Vector2.MoveTowards(transform.position, new Vector2(player.position.x, transform.position.y), moveSpeed * Time.deltaTime);
    }

    /**
    * 停止移动
    */
    private void StopMovement() {
        rb.velocity = Vector2.zero;
        ani.SetBool("isRun", false);
    }

    /**
    * 返回出生点
    */
    private void ReturnToStart() {
        float dist = Vector2.Distance(transform.position, startPoint);
        
        // 若已回到出生点附近，停止移动并待机
        if (dist < 0.1f) {
            StopMovement();
            ani.Play("Idle");

            // 如果现在的朝向 不等于 初始朝向，就转回来！
            if (isFacingRight != initialFacingRight) {
                Flip();
            }

        } else {
            // 否则继续往家走，并处理朝向
            ani.SetBool("isRun", true);
            transform.position = Vector2.MoveTowards(transform.position, startPoint, moveSpeed * Time.deltaTime);
            
            if (startPoint.x > transform.position.x && !isFacingRight) Flip();
            else if (startPoint.x < transform.position.x && isFacingRight) Flip();
        }
    }

    /**
    * 巡逻移动逻辑
    */
    private void Patrol() {
        ani.SetBool("isRun", true);
        
        // 动态计算左右巡逻边界
        float leftBorder = startPoint.x - patrolRange;
        float rightBorder = startPoint.x + patrolRange;

        if (movingLeft) {
            // 向左移动
            transform.position += Vector3.left * moveSpeed * Time.deltaTime; 
            if (isFacingRight) Flip(); 
            // 撞到左墙改向右
            if (transform.position.x <= leftBorder) movingLeft = false; 
        } else {
            // 向右移动
            transform.position += Vector3.right * moveSpeed * Time.deltaTime; 
            if (!isFacingRight) Flip(); 
            // 撞到右墙改向左
            if (transform.position.x >= rightBorder) movingLeft = true;
        }
    }

    /**
    * 始终面朝玩家
    */
    private void LookAtPlayer() {
        if (player.position.x > transform.position.x && !isFacingRight) Flip();
        else if (player.position.x < transform.position.x && isFacingRight) Flip();
    }

    /**
    * 翻转角色朝向
    */
    private void Flip() {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    /**
    * 动画事件：Attack1 伤害判定
    */
    public void CheckHit() {
        Collider2D hitPlayer = Physics2D.OverlapCircle(attackPoint.position, hitRadius, playerLayer);
        if (hitPlayer != null) {
            // 确认击中对象包含玩家脚本
            Player p = hitPlayer.GetComponent<Player>();
            if (p != null) {
                p.onDamage(damage);
                Debug.Log($"[Enemy] 普攻命中: {p.name}");
            }
        }
    }

    /**
    * 动画事件：Attack2 伤害判定
    */
    public void CheckHit2() {
        // 重击判定范围略微增大
        Collider2D hitPlayer = Physics2D.OverlapCircle(attackPoint.position, hitRadius + 0.2f, playerLayer);
        if (hitPlayer != null) {
            Player p = hitPlayer.GetComponent<Player>();
            if (p != null) {
                // 造成 1.5 倍伤害
                p.onDamage(damage * 1.5f);
                Debug.Log($"[Enemy] 重击命中: {p.name}");
            }
        }
    }

    /**
    * 受伤处理
    */
    public void onDamage(float dmg) {
        if (isDead) return;
        currentHealth -= dmg;
        Debug.Log($"[{gameObject.name}] 受到 {dmg} 伤害，剩余血量: {currentHealth}");
        ani.SetTrigger("isHurt");
        
        // 受伤时强制打断攻击指令和连招计数
        ani.ResetTrigger("isAttack1");
        ani.ResetTrigger("isAttack2");
        comboCount = 0;
        
        if (currentHealth <= 0) Die();
    }

    /**
    * 死亡处理
    */
    private void Die() {
        isDead = true;
        ani.SetBool("isDead", true);
        rb.velocity = Vector2.zero;
        rb.simulated = false; // 禁用物理模拟
        
        // 禁用碰撞体防止鞭尸
        GetComponent<Collider2D>().enabled = false;
        Destroy(gameObject, 2f);
    }

    /**
    * 编辑器调试绘图
    */
    void OnDrawGizmosSelected() {
        if (attackPoint != null) {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, hitRadius);
        }
        
        // 绘制黄色巡逻线
        Vector3 center = Application.isPlaying ? startPoint : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(center.x - patrolRange, center.y, 0), new Vector3(center.x + patrolRange, center.y, 0));
    }
}