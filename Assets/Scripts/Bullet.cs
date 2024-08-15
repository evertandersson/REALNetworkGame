using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Bullet : NetworkBehaviour
{
    public Transform networkedOwner;
    [SerializeField] private float speed = 10f;
    public float destroyDistance = 25f;
    private Vector3 startPosition;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        startPosition = transform.position;
    }

    void Update()
    {
        float distanceTraveled = Vector3.Distance(startPosition, transform.position);

        if (distanceTraveled > destroyDistance)
        {
            if (!IsServer) return;

            Destroy(gameObject);
        }

        transform.position += transform.up * speed * Time.deltaTime;
    }
}
