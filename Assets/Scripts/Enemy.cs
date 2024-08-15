using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Enemy : NetworkBehaviour
{
    float speed = 1f;
    float detectionRadius = 5f;

    Vector3 startDirection = Vector3.zero;
    Vector3 targetPosition = Vector3.zero;

    bool aimedAtPlayer = false;

    public bool isDead = false;

    GameObject Player;
    EnemySpawner enemySpawner;

    private string bulletTag = "Bullet";
    [SerializeField] LayerMask playerLayer;

    public override void OnNetworkSpawn()
    {
        enemySpawner = FindObjectOfType<EnemySpawner>().GetComponent<EnemySpawner>();
        NewTargetPosition(startDirection);

        //enemySpawner.enemyDeath += DestroyEnemy;
    }

    private void Update()
    {
        if (!isDead)
        {
            transform.position += transform.up * speed * Time.deltaTime;

            if (!aimedAtPlayer)
            {
                Collider2D checkRadius = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);

                if (checkRadius != null)
                {
                    Player = checkRadius.gameObject;
                    aimedAtPlayer = true;
                }
            }
            else
            {
                if (Player != null)
                {
                    targetPosition = Player.transform.position;
                    NewTargetPosition(targetPosition);
                }
                else
                {
                    aimedAtPlayer = false;
                }
            }
        }
    }

    private void NewTargetPosition(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90;
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isDead)
        {
            if (collision.gameObject.tag == bulletTag)
            {
                enemySpawner.UpdateEnemieskilled();

                if (!IsServer) return;

                Destroy(collision.gameObject);
                Destroy(gameObject);

                isDead = true;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }

}
