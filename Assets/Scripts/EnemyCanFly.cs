using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class EnemyCanFly : MonoBehaviour {

    [Header("--- 基础数值 ---")]
    [Tooltip("怪物的最大生命值")]
    public float maxHealth = 50f;
    
    [Tooltip("怪物巡逻时的飞行速度")]
    public float moveSpeed = 4f; 

    [Tooltip("追击玩家时的速度倍率")]
    public float chaseSpeedMultiplier = 1.5f;
    
    [Tooltip("怪物的基础攻击力")]
    public float damage = 10f;
    
    [Tooltip("巡逻范围矩形 X为宽度 Y为高度")]
    public Vector2 patrolBoxSize = new Vector2(10f, 6f);
    
    [Tooltip("是否启用巡逻行为")]
    public bool canPatrol = true;

    [Header("--- 飞行物理与手感 ---")]
    [Tooltip("移动平滑度 数值越大惯性越大")]
    public float movementSmoothTime = 0.5f;

    [Tooltip("悬浮呼吸幅度")]
    public float hoverBobAmount = 0.5f;

    [Tooltip("悬浮呼吸频率")]
    public float hoverBobSpeed = 2.0f;

    [Tooltip("悬浮弹力强度")]
    public float springStrength = 1000f; 
    
    [Tooltip("悬浮阻尼")]
    public float springDamping = 15f;
    
    [Tooltip("战斗时悬停高度")]
    public float hoverHeight = 3.5f;

    [Header("--- 战斗逻辑配置 ---")]
    [Tooltip("近战贴脸距离")]
    public float closeRange = 1.0f;   
    
    [Tooltip("开始追击与盘旋的判定距离")]
    public float farRange = 7.0f;     
    
    [Tooltip("普攻连打间隔")]
    public float comboInterval = 0.4f; 
    
    [Tooltip("连招结束后的撤离飞行时间")]
    public float retreatDuration = 2.0f; 

    [Header("--- 其他配置 ---")]
    [Tooltip("脱战后的每秒回血量")]
    public float healRate = 5f;
    
    [Tooltip("攻击判定的中心点")]
    public Transform attackPoint;
    
    [Tooltip("攻击判定的圆形半径")]
    public float hitRadius = 0.6f;
    
    [Tooltip("玩家所在的图层")]
    public LayerMask playerLayer;

    [Tooltip("地面图层 用于检测死亡落地")]
    public LayerMask groundLayer;

    // 内部变量
    private Transform player;   
    private Animator ani;       
    private Rigidbody2D rb;     
    
    private Vector2 startPoint; 
    private float currentHealth; 
    
    private float nextActionTime = 0f; 
    
    // 物理悬浮变量
    private float targetHeightY; 
    private float smoothedHeightY;

    // 平滑移动速度缓存
    private float currentVelocityX; 

    // 巡逻变量
    private Vector2 currentPatrolTarget;
    private float patrolWaitTime = 0f;

    // 状态锁
    private bool isRetreating = false;
    private float retreatEndTime = 0f;

    private bool isDead = false;         
    private bool isFallingToDie = false; 
    private bool isFacingRight = false; 
    private int comboCount = 0; 

    private bool initialFacingRight; 

    /**
    * 初始化组件与状态
    */
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
            Debug.LogWarning("[EnemyCanFly] 警告：未找到 Player 标签的物体。");
        }

        if (transform.localScale.x < 0) {
            isFacingRight = false;
        } else {
            isFacingRight = true;
        }
        
        initialFacingRight = isFacingRight;
    }

    /**
    * 物理计算循环
    * 这里处理核心的悬浮力计算
    */
    void FixedUpdate() {
        // 如果正在坠落等待死亡 或者是彻底死亡 都不再施加悬浮力
        if (isDead || isFallingToDie) return;

        // 垂直移动平滑处理
        // 让物理锚点平滑移动 避免垂直方向的瞬移感
        smoothedHeightY = Mathf.MoveTowards(smoothedHeightY, targetHeightY, moveSpeed * Time.fixedDeltaTime);

        // 呼吸感计算
        // 使用正弦波叠加一个微小的上下浮动 模拟生物在空中悬停时的不稳定性
        float bobbingOffset = Mathf.Sin(Time.time * hoverBobSpeed) * hoverBobAmount;
        float finalTargetY = smoothedHeightY + bobbingOffset;

        // 弹簧力计算
        float forceY = 0f;
        float yDifference = finalTargetY - transform.position.y;

        // 应用胡克定律
        float springForce = yDifference * springStrength;
        // 应用阻尼力防止抖动
        float dampingForce = -rb.velocity.y * springDamping;

        // 加上重力补偿
        forceY = springForce + dampingForce + (rb.mass * Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale);

        rb.AddForce(new Vector2(0f, forceY));
    }

    /**
    * 逻辑更新循环
    */
    void Update() {
        // 死亡状态下停止逻辑更新
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

    /**
    * 通用平滑移动函数
    * 使用 SmoothDamp 模拟惯性移动
    */
    private void SmoothMoveToX(float targetX, float speed) {
        float newX = Mathf.SmoothDamp(transform.position.x, targetX, ref currentVelocityX, movementSmoothTime, speed);
        transform.position = new Vector2(newX, transform.position.y);
    }

    /**
    * 战斗状态决策逻辑
    */
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

    /**
    * 执行近战连招
    */
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

    /**
    * 触发撤离
    */
    private void TriggerRetreat() {
        isRetreating = true;
        retreatEndTime = Time.time + retreatDuration;
    }

    /**
    * 执行撤离飞行
    */
    private void PerformRetreat() {
        ani.SetBool("isRun", true);

        float retreatDirX = isFacingRight ? -1f : 1f;
        float targetX = transform.position.x + retreatDirX * 5f;
        
        // 撤离时稍微加速
        SmoothMoveToX(targetX, moveSpeed * 1.2f);

        targetHeightY = startPoint.y + hoverHeight;
    }

    /**
    * 脱战与巡逻逻辑
    */
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

    /**
    * 追逐移动
    * 战斗状态下应用加速倍率
    */
    private void ChasePlayer(bool isHovering) {
        ani.SetBool("isRun", true);
        
        float actualSpeed = moveSpeed * chaseSpeedMultiplier;
        SmoothMoveToX(player.position.x, actualSpeed);

        if (isHovering) {
            targetHeightY = player.position.y + hoverHeight;
        } else {
            // 俯冲攻击时稍微留出一点高度 防止穿模
            targetHeightY = player.position.y + 0.5f;
        }
    }

    /**
    * 矩形随机巡逻
    */
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
            // 巡逻使用普通速度
            SmoothMoveToX(currentPatrolTarget.x, moveSpeed);
            targetHeightY = currentPatrolTarget.y;

            if (currentPatrolTarget.x > transform.position.x && !isFacingRight) Flip();
            else if (currentPatrolTarget.x < transform.position.x && isFacingRight) Flip();
        }
    }

    /**
    * 获取新巡逻点
    * 包含防折返逻辑
    */
    private void GetNewPatrolPoint() {
        Vector2 candidatePoint = Vector2.zero;
        int attempts = 0;
        
        // 尝试找一个距离当前位置足够远的点 防止原地鬼畜
        while (attempts < 10) {
            float randomX = Random.Range(-patrolBoxSize.x, patrolBoxSize.x);
            float randomY = Random.Range(-patrolBoxSize.y, patrolBoxSize.y);
            candidatePoint = startPoint + new Vector2(randomX, randomY);
            
            // 如果新点距离当前位置足够远 则接受
            if (Vector2.Distance(transform.position, candidatePoint) > 3.0f) {
                break;
            }
            attempts++;
        }
        
        currentPatrolTarget = candidatePoint;
        patrolWaitTime = Time.time + Random.Range(2.0f, 4.0f);
    }

    /**
    * 返回出生点
    */
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

    public void CheckHit() {
        Collider2D hitPlayer = Physics2D.OverlapCircle(attackPoint.position, hitRadius, playerLayer);
        if (hitPlayer != null) hitPlayer.GetComponent<Player>()?.onDamage(damage);
    }

    public void CheckHit2() {
        Collider2D hitPlayer = Physics2D.OverlapCircle(attackPoint.position, hitRadius + 0.2f, playerLayer);
        if (hitPlayer != null) hitPlayer.GetComponent<Player>()?.onDamage(damage * 1.5f);
    }

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

    /**
    * 死亡第一阶段 失去动力坠落
    */
    private void Die() {
        isFallingToDie = true;
        ani.SetBool("isRun", false); 
        rb.velocity = Vector2.zero;
        
        // 稍微给一点向上的初速度 模拟被击落的效果
        rb.AddForce(Vector2.up * 5f, ForceMode2D.Impulse);
        
        // 确保重力生效 让它掉下去
        rb.gravityScale = 3.0f; 
    }

    /**
    * 碰撞检测 用于检测尸体落地
    */
    private void OnCollisionEnter2D(Collision2D collision) {
        // 如果正在坠落 并且撞到了地面
        if (isFallingToDie && !isDead) {
            // 检查碰撞体是否属于地面图层
            if (((1 << collision.gameObject.layer) & groundLayer) != 0) {
                PerformFinalDeath();
            }
        }
    }

    /**
    * 死亡第二阶段 落地播放动画并销毁
    */
    private void PerformFinalDeath() {
        isDead = true;
        ani.SetBool("isDead", true);
        
        // 停止物理运动 防止尸体滑行
        rb.velocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static; 
        
        GetComponent<Collider2D>().enabled = false;

        Destroy(gameObject, 2f);
    }

    void OnDrawGizmosSelected() {
        if (attackPoint != null) {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, hitRadius);
        }
        
        Vector3 center = Application.isPlaying ? (Vector3)startPoint : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, new Vector3(patrolBoxSize.x * 2, patrolBoxSize.y * 2, 0));
    }
}