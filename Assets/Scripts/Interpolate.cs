using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;
// This bypases the default custom editor for NetworkTransform
// and lets you modify your custom NetworkTransform's properties
// within the inspector view
[CustomEditor(typeof(Interpolate), true)]
public class InterpolateEditor : Editor
{
}
#endif
public class Interpolate : NetworkTransform
{
    // Normal movement speed
    public float Speed = 4.0f;

    // Normal roation speed
    public float RotSpeed = 1.0f;

    // Dash speed
    public float DashSpeed = 6.0f;

    // Distance to dash
    public float DashDistance = 4.0f;

    // Delta threshold when player has reached final dash point
    public float DashDelta = 0.25f;

    public AuthorityModes AuthorityMode;

    public enum AuthorityModes
    {
        Owner,
        Server
    }

    public enum PlayerStates
    {
        Normal,
        PendingDash,
        Dash
    }

    protected override bool OnIsServerAuthoritative()
    {
        return AuthorityMode == AuthorityModes.Server;
    }

    public class PlayerStateUpate : INetworkSerializable
    {
        public PlayerStates PlayerState;
        public Vector3 StartPos;
        public Vector3 EndPos;

        protected virtual void OnNetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PlayerState);
            if (PlayerState == PlayerStates.Dash)
            {
                serializer.SerializeValue(ref StartPos);
                serializer.SerializeValue(ref EndPos);
            }
            OnNetworkSerialize(serializer);
        }
    }

    private NetworkVariable<PlayerStateUpate> m_PlayerState = new NetworkVariable<PlayerStateUpate>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private List<PlayerStateUpate> m_PendingStates = new List<PlayerStateUpate>(new PlayerStateUpate[] { new PlayerStateUpate() { PlayerState = PlayerStates.Normal } });

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            var temp = transform.position;
            temp.y = 0.5f;
            transform.position = temp;
        }
        else
        {
            m_PlayerState.OnValueChanged += OnPlayerStateChanged;
        }
        base.OnNetworkSpawn();
    }

    public override void OnNetworkDespawn()
    {
        m_PlayerState.OnValueChanged -= OnPlayerStateChanged;
        base.OnNetworkDespawn();
    }

    private void OnPlayerStateChanged(PlayerStateUpate previous, PlayerStateUpate current)
    {
        m_PendingStates.Add(current);
    }

    /// <summary>
    /// Override the OnAuthorityPushTransformState to apply dash values before sending player state update.
    /// This assures the local player's position is the most currently known position to other players.
    /// </summary>
    /// <param name="networkTransformState">The most current state sent to the client</param>
    protected override void OnAuthorityPushTransformState(ref NetworkTransformState networkTransformState)
    {
        var pendingState = m_PendingStates[m_PendingStates.Count - 1];

        // If we have a pending dash, then apply the dash values
        if (pendingState.PlayerState == PlayerStates.PendingDash)
        {
            var targetPosition = transform.position + (transform.forward * DashDistance);
            // Used by other players (nonauthority instances) to assure when they begin dashing
            // they are starting from a synchronized position.
            pendingState.StartPos = transform.position;
            // Used by both authority and nonauthority instances to determine when they have reached
            // a "close enough" distance, determined by DashDelta, to end the player's dash sequence.
            pendingState.EndPos = targetPosition;
            // Apply the updated state and mark it dirty
            m_PlayerState.Value = pendingState;
            m_PlayerState.SetDirty(true);
            // Change the local state to Dash
            pendingState.PlayerState = PlayerStates.Dash;
            // Apply these changes back into our pending states list
            m_PendingStates[m_PendingStates.Count - 1] = pendingState;
        }
        base.OnAuthorityPushTransformState(ref networkTransformState);
    }

    /// <summary>
    /// Normal player movement
    /// </summary>
    private void PlayerMove()
    {
        transform.position = Vector3.Lerp(transform.position, transform.position + Input.GetAxis("Vertical") * Speed * transform.forward, Time.fixedDeltaTime);
        var rotation = transform.rotation;
        var euler = rotation.eulerAngles;
        euler.y += Input.GetAxis("Horizontal") * 90 * RotSpeed * Time.fixedDeltaTime;
        rotation.eulerAngles = euler;
        transform.rotation = rotation;
    }

    /// <summary>
    /// Dash state update invoked by both the authority and the nonauthority instances
    /// </summary>
    /// <param name="pendingState"></param>
    private void PlayerDash(ref PlayerStateUpate pendingState)
    {
        transform.position = Vector3.Lerp(transform.position, pendingState.EndPos, Time.fixedDeltaTime * DashSpeed);
        var distance = Vector3.Distance(pendingState.EndPos, transform.position);
        if (distance <= DashDelta)
        {
            pendingState.PlayerState = PlayerStates.Normal;
        }
    }

    /// <summary>
    /// The Authority's primary update
    /// </summary>
    private void AuthorityStateUpdate()
    {
        var pendingState = m_PendingStates[m_PendingStates.Count - 1];
        if (Input.GetKeyDown(KeyCode.Space) && pendingState.PlayerState == PlayerStates.Normal)
        {
            m_PendingStates.Add(new PlayerStateUpate() { PlayerState = PlayerStates.PendingDash });
            Interpolate = false;
        }

        switch (pendingState.PlayerState)
        {
            case PlayerStates.PendingDash:
                {
                    // Nudge the authority instance to kick off a state update
                    // See OnAuthorityPushTransformState
                    var position = transform.position;
                    position += transform.forward * (PositionThreshold * 2);
                    transform.position = position;
                    break;
                }
            case PlayerStates.Dash:
                {
                    // Apply the dash
                    PlayerDash(ref pendingState);
                    // If the pending state was set back to normal, then
                    // remove that pending state (the normal state will always remain)
                    if (pendingState.PlayerState == PlayerStates.Normal)
                    {
                        m_PendingStates.Remove(pendingState);
                        Interpolate = true;
                    }
                    break;
                }
            case PlayerStates.Normal:
                {
                    PlayerMove();
                    break;
                }
        }
    }

    /// <summary>
    /// The nonauthority's primary update
    /// </summary>
    private void NonAuthorityStateUpdate()
    {
        var pendingState = m_PendingStates[m_PendingStates.Count - 1];
        switch (pendingState.PlayerState)
        {
            case PlayerStates.PendingDash:
                {

                    var distance = Vector3.Distance(pendingState.StartPos, transform.position);
                    // Nonauthority will wait until it has interpolated to the StartPos
                    if (pendingState.PlayerState == PlayerStates.PendingDash && distance <= PositionThreshold)
                    {
                        // Once reached, start the dash sequence
                        pendingState.PlayerState = PlayerStates.Dash;
                        // Apply the state's changes
                        m_PendingStates[m_PendingStates.Count - 1] = pendingState;
                    }
                    break;
                }
            case PlayerStates.Dash:
                {
                    // Dash until we have reached the EndPos
                    PlayerDash(ref pendingState);
                    // If we reached the end position, the current pending state
                    // will be Normal and we remove it from the pendings states.
                    if (pendingState.PlayerState == PlayerStates.Normal)
                    {
                        m_PendingStates.Remove(pendingState);
                    }
                    break;
                }
        }
    }

    protected override void Update()
    {
        // If not spawned or the authority, exit early
        if (!IsSpawned || CanCommitToTransform)
        {
            return;
        }
        // If non-authority's current state is Normal, then just interpolate to the
        // authority's last sent state values.
        var pendingState = m_PendingStates[m_PendingStates.Count - 1];
        if (pendingState.PlayerState == PlayerStates.Normal)
        {
            base.Update();
        }
    }

    private void FixedUpdate()
    {
        // If not spawned, exit early
        if (!IsSpawned)
        {
            return;
        }
        // If we can commit to transform we are the authority
        if (CanCommitToTransform)
        {
            // Run authority update
            AuthorityStateUpdate();
        }
        else
        {
            // Otherwise, run the nonauthority update
            NonAuthorityStateUpdate();
        }
    }
}