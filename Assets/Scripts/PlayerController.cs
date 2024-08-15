using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float speed = 3f;
    private Camera mainCamera;
    private Vector3 mouseInput = Vector3.zero;
    private Vector3 targetDirection;

    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private GameObject enemySpawner;

    private string enemyTag = "Enemy";

    private void Initialize()
    {
        mainCamera = Camera.main;
    }

    public override void OnNetworkSpawn()
    {
        Initialize();

        if (!IsOwner) return;

        CreateEnemySpawnerServerRpc();
        enemySpawner.GetComponent<EnemySpawner>().NextRoundRpc();
    }

    [ServerRpc]
    private void CreateEnemySpawnerServerRpc()
    {
        if (!IsOwner)
        {
            enemySpawner = FindObjectOfType<EnemySpawner>().gameObject;
        }
        else
        {
            enemySpawner = Instantiate(enemySpawner, Vector3.zero, Quaternion.identity);
            enemySpawner.GetComponent<NetworkObject>().Spawn();
        }

    }

    private void Update()
    {
        if (!IsOwner || !Application.isFocused) return;

        //Movement
        mouseInput.x = Input.mousePosition.x;
        mouseInput.y = Input.mousePosition.y;
        mouseInput.z = mainCamera.nearClipPlane;
        Vector3 mouseWorldCoordinates = mainCamera.ScreenToWorldPoint(mouseInput);
        mouseWorldCoordinates.z = 0f;
        if (Input.GetKey(KeyCode.W))
            transform.position = Vector3.MoveTowards(transform.position, mouseWorldCoordinates, Time.deltaTime * speed);

        //Rotate
        if (mouseWorldCoordinates != transform.position)
        {
            targetDirection = mouseWorldCoordinates - transform.position;
            transform.up = targetDirection;
            targetDirection.z = 0;
        }

        if (Input.GetMouseButtonDown(0))
        {
            SpawnBulletServerRPC(transform.position, transform.rotation);
        }
    }

    [ServerRpc]
    private void SpawnBulletServerRPC(Vector2 position, Quaternion rotation)
    {
        GameObject InstansiatedBullet = Instantiate(bulletPrefab, position, rotation);

        InstansiatedBullet.GetComponent<NetworkObject>().Spawn();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == enemyTag)
        {
            if (!IsServer) return;

            Destroy(gameObject);
        }
    }
}
