using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI enemiesKilledText;

    [SerializeField] GameObject enemyPrefab;
    [SerializeField] float spawnRadius = 15f; // Distance from the player

    public NetworkVariable<int> totalEnemies = new NetworkVariable<int>();
    public NetworkVariable<int> enemiesKilled = new NetworkVariable<int>();

    private NetworkVariable<int> round = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
    }

    [Rpc(SendTo.Server)]
    public void NextRoundRpc()
    {
        if (!IsOwner) return;

        enemiesKilled.Value = 0;
        round.Value++;
        switch(round.Value)
        {
            case 1:
                totalEnemies.Value = 10;
                StartRoundRpc(totalEnemies.Value);
                break;
            case 2:
                totalEnemies.Value = 20;
                StartRoundRpc(totalEnemies.Value);
                break;
            case 3:
                totalEnemies.Value = 40;
                StartRoundRpc(totalEnemies.Value);
                break;
            case 4:
                totalEnemies.Value = 60;
                StartRoundRpc(totalEnemies.Value);
                break;
        }
    }

    [Rpc(SendTo.Server)]
    public void StartRoundRpc(int _totalEnemies)
    {
        if (!IsOwner) return;

        for (int i = 0; i < _totalEnemies; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2); // Random angle in radians
            float x = Mathf.Cos(angle) * spawnRadius;
            float y = Mathf.Sin(angle) * spawnRadius;

            Vector3 spawnPosition = new Vector3(transform.position.x + x, transform.position.y + y, 0);

            GameObject InstansiatedEnemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);

            InstansiatedEnemy.GetComponent<NetworkObject>().Spawn();
        }
    }

    private void Update()
    {
        UpdateEnemiesKilledText();
    }

    public void UpdateEnemieskilled()
    {
        if (IsServer) enemiesKilled.Value++;

        if (enemiesKilled.Value >= totalEnemies.Value)
        {
            NextRoundRpc();
        }
    }

    public void UpdateEnemiesKilledText()
    {
        roundText.text = "Round: " + round.Value;
        enemiesKilledText.text = "Enemies killed: " + enemiesKilled.Value + "/" + totalEnemies.Value;
    }
}