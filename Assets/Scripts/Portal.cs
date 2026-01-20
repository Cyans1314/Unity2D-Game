using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement; 

public class Portal : MonoBehaviour
{
    [Header("关卡设置")]
    [Tooltip("下一关的名字 要和Build Settings里的一样")]
    public string nextSceneName = "Level2";

    [Tooltip("传送前的等待时间 用来播放淡出效果")]
    public float waitTime = 0.2f;

    [Header("战斗检测")]
    [Tooltip("把场景里的 Enemys 总父物体拖到这里")]
    public Transform enemiesParent;

    // 防止重复触发
    private bool isTriggered = false; 

    private void OnTriggerEnter2D(Collider2D other) {
        if (isTriggered) return;
        
        if (other.CompareTag("Player") || other.CompareTag("player")) {
            
            // 进门前先检查是否还有活着的怪物
            if (CheckEnemiesAlive()) {
                Debug.Log("传送门未激活 还有怪物存活");
                return;
            }

            StartCoroutine(TeleportProcess(other.gameObject));
        } 
    }

    // 检查是否有怪物存活
    private bool CheckEnemiesAlive() {
        // 如果未绑定父物体 则默认放行
        if (enemiesParent == null) return false;

        // 获取子物体中所有的刚体组件
        // 因为AttackPoint是空物体且没有刚体 所以不会被统计
        // 只有活着的怪物会被统计进去
        Rigidbody2D[] remainingEnemies = enemiesParent.GetComponentsInChildren<Rigidbody2D>();

        // 如果数量大于0 说明还有怪没死透
        if (remainingEnemies.Length > 0) {
            Debug.Log("当前剩余怪物数量 " + remainingEnemies.Length);
            return true;
        } else {
            return false;
        }
    }

    // 传送流程协程
    private IEnumerator TeleportProcess(GameObject player) {
        isTriggered = true;
        Debug.Log("检测到玩家 准备传送至 " + nextSceneName);

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        Animator ani = player.GetComponent<Animator>();
        SpriteRenderer sr = player.GetComponent<SpriteRenderer>();

        // 冻结玩家物理状态
        if (rb != null) {
            rb.velocity = Vector2.zero; 
            rb.bodyType = RigidbodyType2D.Kinematic; 
        }

        // 播放待机动画
        if (ani != null) {
            ani.SetBool("isRun", false); 
            ani.Play("Idle"); 
        }

        // 玩家透明度渐变
        float timer = 0f;
        while (timer < waitTime) {
            timer += Time.deltaTime;
            if (sr != null) {
                float alpha = Mathf.Lerp(1f, 0f, timer / waitTime);
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);
            } 
            yield return null; 
        }

        // 加载场景
        if (Application.CanStreamedLevelBeLoaded(nextSceneName)) {
            SceneManager.LoadScene(nextSceneName);
        } else {
            Debug.LogError("找不到场景 " + nextSceneName + " 请检查Build Settings");
            isTriggered = false;
            
            // 恢复玩家状态
            if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
            if (sr != null) sr.color = Color.white; 
        }
    }
}