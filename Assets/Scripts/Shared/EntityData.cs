using System;
using Lockstep.Packets;

[Serializable]
public struct EntityData
{
    public EntityState[] states;
    public EntityMotionConfig[] motionConfigs;
}


[Serializable]
public enum ColliderType
{
    Box,
    Sphere
}

[Serializable]
public struct CollisionBodyState
{
    public ColliderType colliderType;
    public Vector3i position;
    public Vector3i colliderSize;
    public int colliderRadius;
}

[Serializable]
public struct PlayerState
{
    public uint clientId;
    public CommandType commandType;
    public Vector3i targetPosition;
    public Vector3i frameVelocity;
    public CollisionBodyState body;

    public Vector3i position
    {
        readonly get => body.position;
        set => body.position = value;
    }

    public Vector3i colliderSize
    {
        readonly get => body.colliderSize;
        set => body.colliderSize = value;
    }

    public int colliderRadius
    {
        readonly get => body.colliderRadius;
        set => body.colliderRadius = value;
    }
}

public static class PlayerSimulationConfig
{
    public static readonly Vector3i ColliderSize = Vector3i.One;
    public const int ColliderRadius = 500;
}

[Serializable]
public struct EntityState
{
    public uint entityId;
    public CollisionBodyState body;

    public Vector3i position
    {
        readonly get => body.position;
        set => body.position = value;
    }

    public Vector3i colliderSize
    {
        readonly get => body.colliderSize;
        set => body.colliderSize = value;
    }

    public int colliderRadius
    {
        readonly get => body.colliderRadius;
        set => body.colliderRadius = value;
    }
}

[Serializable]
public struct EntityMotionConfig
{
    public uint entityId;
    public bool isDynamic;
    public int dragPermille;
    public int maxSpeedPerTick;
    public int pushImpulsePerCollision;
    public int bouncinessPermille;
}

[Serializable]
public struct EntityMotionFrame
{
    public uint entityId;
    public Vector3i velocity;
}

[Serializable]
public struct GameState
{
    public PlayerState[] playerStates;
    public EntityState[] entityStates;
    public EntityMotionFrame[] entityMotionFrames;
}
