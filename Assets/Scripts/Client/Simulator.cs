using System;
using System.Collections.Generic;
using System.Linq;
using Lockstep.Packets;
using UnityEngine;

public enum ColliderType
{
    Box,
    Sphere
}

public struct CollisionBodyState
{
    public ColliderType colliderType;
    public Vector3i position;
    public Vector3i colliderSize;
    public int colliderRadius;
}

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

public struct EntityMotionConfig
{
    public uint entityId;
    public bool isDynamic;
    public int dragPermille;
    public int maxSpeedPerTick;
    public int pushImpulsePerCollision;
    public int bouncinessPermille;
}

public struct EntityMotionFrame
{
    public uint entityId;
    public Vector3i velocity;
}

public struct GameState
{
    public PlayerState[] playerStates;
    public EntityState[] entityStates;
    public EntityMotionFrame[] entityMotionFrames;
}

sealed class EntityMotionRuntime
{
    public bool isDynamic;
    public int dragPerTick;
    public int maxSpeedPerTick;
    public int pushImpulsePerCollision;
    public int bouncinessPermille;
    public Vector3i velocity;
}

public class Simulator
{
    public Dictionary<uint, PlayerState> playerStates { get; private set; } = new Dictionary<uint, PlayerState>();
    public Dictionary<uint, EntityState> entityStates { get; private set; } = new Dictionary<uint, EntityState>();
    readonly Dictionary<uint, EntityMotionRuntime> entityMotionStates = new Dictionary<uint, EntityMotionRuntime>();
    // 模拟层保存10tick历史状态
    public RingBuffer<GameState> gameStateHistory = new RingBuffer<GameState>(10);
    // 拿脚填的数值
    int moveSpeedPerTick = 200;

    public void SimulateFrame(FramePacket framePacket)
    {
#if UNITY_EDITOR
        Debug.Log($"[Simulator] Tick={framePacket.tick}, inputCount={(framePacket.inputs == null ? 0 : framePacket.inputs.Length)}");
#endif
        ApplyFrameInputs(framePacket);
        CalculateMovement();
        SimulateEntityMotion();
        CalculateCollision();
        SaveGameState(framePacket.tick);
    }

    public void SetPlayerState(PlayerState[] players)
    {
        if (players.Length == 0)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.LogWarning("No Player");
#endif
            return;
        }

        foreach (PlayerState player in players)
        {
            playerStates[player.clientId] = player;
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[Simulator] Sync clientId={player.clientId}, commandType={player.commandType}");
#endif
        }
    }

    public void SetEntityState(EntityState[] entities)
    {
        if (entities.Length == 0)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.LogWarning("No Entity");
#endif
            return;
        }

        foreach (EntityState entity in entities)
        {
            entityStates[entity.entityId] = entity;
        }
    }

    public void SetEntityMotionConfigs(EntityMotionConfig[] configs)
    {
        foreach (var config in configs)
        {
            if (!entityMotionStates.TryGetValue(config.entityId, out var runtime))
            {
                runtime = new EntityMotionRuntime();
                entityMotionStates[config.entityId] = runtime;
            }

            runtime.isDynamic = config.isDynamic;
            runtime.dragPerTick = Clamp(config.dragPermille, 0, 1000);
            runtime.maxSpeedPerTick = Math.Max(0, config.maxSpeedPerTick);
            runtime.pushImpulsePerCollision = Math.Max(0, config.pushImpulsePerCollision);
            runtime.bouncinessPermille = Clamp(config.bouncinessPermille, 0, 1000);
        }
    }

    void ApplyFrameInputs(FramePacket framePacket)
    {
        foreach (InputPacket input in framePacket.inputs)
        {
            if (!playerStates.TryGetValue(input.clientId, out PlayerState playerState))
            {
                playerState = new PlayerState();
            }

            switch (input.commandType)
            {
                case CommandType.Move:
                    playerState.targetPosition = input.inputPos;
                    playerState.commandType = CommandType.Move;
                    break;
                case CommandType.Cancel:
                    playerState.commandType = CommandType.None;
                    break;
                case CommandType.None:
                    break;
            }
            playerStates[input.clientId] = playerState;
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[Simulator] Input clientId={input.clientId}, commandType={input.commandType}, target={playerState.targetPosition}");
#endif
        }
    }

    void CalculateMovement()
    {
        foreach (var clientId in playerStates.Keys.ToArray())
        {
            var state = playerStates[clientId];
            state.frameVelocity = Vector3i.Zero;

            if (state.commandType == CommandType.Move)
            {
                Vector3i previousPosition = state.position;
                Vector3i delta = state.targetPosition - state.position;
                int distanceToTarget = Vector3i.Distance(state.position, state.targetPosition);
#if UNITY_EDITOR
                UnityEngine.Debug.Log($"[Simulator] Move clientId={clientId}, from={state.position}, target={state.targetPosition}, delta={delta}, dist={distanceToTarget}, moveSpeedPerTick={moveSpeedPerTick}");
#endif
                if (distanceToTarget <= moveSpeedPerTick)
                {
                    state.position = state.targetPosition; // 到达目标位置
                    state.commandType = CommandType.None; // 停止移动
#if UNITY_EDITOR
                    UnityEngine.Debug.Log($"[Simulator] Arrived clientId={clientId}, position={state.position}");
#endif
                }
                else
                {
                    Vector3i step = delta.ScaleTo(moveSpeedPerTick);
                    state.position += step;
#if UNITY_EDITOR
                    UnityEngine.Debug.Log($"[Simulator] Step clientId={clientId}, step={step}, newPosition={state.position}");
#endif   
                }

                state.frameVelocity = state.position - previousPosition;
            }

            playerStates[clientId] = state; // 写回
        }
    }

    void SimulateEntityMotion()
    {
        foreach (var entityId in entityStates.Keys.ToArray())
        {
            if (!entityMotionStates.TryGetValue(entityId, out var motion) || !motion.isDynamic)
            {
                continue;
            }

            var state = entityStates[entityId]; 
            motion.velocity = motion.velocity.MultiplyByScalar(1000 - Clamp(motion.dragPerTick, 0, 1000)).DivideByScalar(1000);
            state.position += motion.velocity;
            entityStates[entityId] = state;
        }
    }

    void SaveGameState(uint tick)
    {
#if UNITY_EDITOR
        Debug.Log($"[Simulator] Saving game state for tick {tick}...");
#endif
        gameStateHistory[tick] = new GameState
        {
            playerStates = playerStates.Values.ToArray(),
            entityStates = entityStates.Values.ToArray(),
            entityMotionFrames = entityMotionStates.Select(pair => new EntityMotionFrame
            {
                entityId = pair.Key,
                velocity = pair.Value.velocity
            }).ToArray()
        };
    }

    public void CaptureGameState(uint tick)
    {
#if UNITY_EDITOR
        Debug.Log($"[Simulator] Capturing game state for tick {tick}");
#endif
        SaveGameState(tick);
    }

    public void LoadGameState(uint tick)
    {
        playerStates.Clear();
        foreach (PlayerState playerState in gameStateHistory[tick].playerStates)
        {
            playerStates.Add(playerState.clientId, playerState);
        }
        entityStates.Clear();
        foreach (EntityState entityState in gameStateHistory[tick].entityStates)
        {
            entityStates.Add(entityState.entityId, entityState);
        }

        foreach (EntityMotionFrame motionFrame in gameStateHistory[tick].entityMotionFrames ?? Array.Empty<EntityMotionFrame>())
        {
            if (!entityMotionStates.TryGetValue(motionFrame.entityId, out var runtime))
            {
                runtime = new EntityMotionRuntime();
                entityMotionStates[motionFrame.entityId] = runtime;
            }

            runtime.velocity = motionFrame.velocity;
        }
    }

    void CalculateCollision()
    {
        // 1. 玩家与玩家碰撞
        uint[] players = playerStates.Keys.ToArray();
        for (int i = 0; i < players.Length; i++)
        {
            for (int j = i + 1; j < players.Length; j++)
            {
                uint playerA = players[i];
                uint playerB = players[j];
                
                PlayerState stateA = playerStates[playerA];
                PlayerState stateB = playerStates[playerB];
                
                if (CheckOverlapSphere(stateA.body, stateB.body))
                {
                    HandlePlayerPlayerCollisionSphere(playerA, playerB, ref stateA, ref stateB);
                    playerStates[playerA] = stateA;
                    playerStates[playerB] = stateB;
                }
            }
        }
        
        // 2. 玩家与物体碰撞
        uint[] entityIds = entityStates.Keys.ToArray();
        for (int i = 0; i < players.Length; i++)
        {
            uint playerId = players[i];
            PlayerState player = playerStates[playerId];
            
            foreach (uint entityId in entityIds)
            {
                EntityState entity = entityStates[entityId];
                if (entity.body.colliderType == ColliderType.Box)
                {
                    if (CheckBoxShpereOverlap(entity.body, player.body))
                    {
                        HandlePlayerEntityCollisionWall(playerId, entityId, ref player, ref entity);
                        playerStates[playerId] = player;
                        entityStates[entityId] = entity;
                    }
                    continue;
                }

                if (CheckOverlapSphere(player.body, entity.body))
                {
                    HandlePlayerEntityCollisionSphere(playerId, entityId, ref player, ref entity);
                    playerStates[playerId] = player;
                    entityStates[entityId] = entity;
                }
            }
        }
        
        // 3. 物体与物体碰撞
        uint[] entities = entityStates.Keys.ToArray();
        for (int i = 0; i < entities.Length; i++)
        {
            for (int j = i + 1; j < entities.Length; j++)
            {
                uint entityA = entities[i];
                uint entityB = entities[j];
                
                EntityState stateA = entityStates[entityA];
                EntityState stateB = entityStates[entityB];
                
                if (stateA.body.colliderType == ColliderType.Box)
                {
                    if (stateB.body.colliderType == ColliderType.Box)
                    {
                        // TODO: Skip box to box collision for now
                        continue;
                    }

                    if (CheckBoxShpereOverlap(stateA.body, stateB.body))
                    {
                        HandleEntityEntityCollisionWall(entityB, entityA, ref stateB, ref stateA);
                        entityStates[entityA] = stateA;
                        entityStates[entityB] = stateB;
                    }
                    continue;
                }

                if (stateB.body.colliderType == ColliderType.Box)
                {
                    if (CheckBoxShpereOverlap(stateB.body, stateA.body))
                    {
                        HandleEntityEntityCollisionWall(entityA, entityB, ref stateA, ref stateB);
                        entityStates[entityA] = stateA;
                        entityStates[entityB] = stateB;
                    }
                    continue;
                }

                if (CheckOverlapSphere(stateA.body, stateB.body))
                {
                    HandleEntityEntityCollisionSphere(entityA, entityB, ref stateA, ref stateB);
                    entityStates[entityA] = stateA;
                    entityStates[entityB] = stateB;
                }
            }
        }
    }

    bool CheckOverlapSphere(CollisionBodyState body1, CollisionBodyState body2)
    {
        int radiusSum = body1.colliderRadius + body2.colliderRadius;
        return Vector3i.DistanceSquared(body1.position, body2.position) < (long)radiusSum * radiusSum;
    }

    bool CheckBoxShpereOverlap(CollisionBodyState box, CollisionBodyState sphere)
    {
        Vector3i halfExtents = box.colliderSize.DivideByScalar(2);
        Vector3i d = sphere.position - box.position;
        int x = Clamp(d.x, -halfExtents.x, halfExtents.x);
        int y = Clamp(d.y, -halfExtents.y, halfExtents.y);
        int z = Clamp(d.z, -halfExtents.z, halfExtents.z);
        Vector3i closestPoint = box.position + new Vector3i(x, y, z);
        Vector3i delta = sphere.position - closestPoint;

        return delta.SquaredMagnitude() < (long)sphere.colliderRadius * sphere.colliderRadius;
    }

    void HandlePlayerPlayerCollisionSphere(uint playerA, uint playerB, ref PlayerState stateA, ref PlayerState stateB)
    {
        if (!TryGetSphereCollision(stateA.body, stateB.body, out Vector3i normal, out int penetration))
        {
            return;
        }

        Vector3i separation = normal.ScaleTo(penetration);
        Vector3i half = separation.DivideByScalar(2);
        stateA.position -= half;
        stateB.position += separation - half;
#if UNITY_EDITOR
        UnityEngine.Debug.Log($"[Collision] Player-Player Sphere: {playerA} <-> {playerB}");
#endif
    }

    void HandlePlayerEntityCollisionSphere(uint playerId, uint entityId, ref PlayerState player, ref EntityState entity)
    {
        if (!TryGetSphereCollision(player.body, entity.body, out Vector3i normal, out int penetration))
        {
            return;
        }

        Vector3i separation = normal.ScaleTo(penetration);
        if (entityMotionStates.TryGetValue(entityId, out var motion) && motion.isDynamic)
        {
            Vector3i playerCorrection = separation.MultiplyByScalar(2).DivideByScalar(3);
            Vector3i entityCorrection = separation - playerCorrection;
            player.position -= playerCorrection;
            entity.position += entityCorrection;
            ApplyPlayerHitToEntity(player.frameVelocity, motion, normal);
        }
        else
        {
            player.position -= separation;
        }
#if UNITY_EDITOR
        UnityEngine.Debug.Log($"[Collision] Player-Entity Sphere: playerId={playerId} <-> entityId={entityId}");
#endif
    }

    void HandlePlayerEntityCollisionWall(uint playerId, uint entityId, ref PlayerState player, ref EntityState entity)
    {
        if (!TryGetBoxSphereCollision(entity.body, player.body, out Vector3i normal, out int penetration))
        {
            return;
        }

        Vector3i separation = normal.ScaleTo(penetration);
        player.position += separation;
#if UNITY_EDITOR
        UnityEngine.Debug.Log($"[Collision] Player-Entity Box: playerId={playerId} <-> entityId={entityId}, normal={normal}, penetration={penetration}");
#endif
    }

    // sphereEntityId is the dynamic sphere candidate, wallEntityId is the static box.
    void HandleEntityEntityCollisionWall(uint sphereEntityId, uint wallEntityId, ref EntityState sphere, ref EntityState wall)
    {
        if (!TryGetBoxSphereCollision(wall.body, sphere.body, out Vector3i normal, out int penetration))
        {
            return;
        }

        if (!entityMotionStates.TryGetValue(sphereEntityId, out EntityMotionRuntime motion) || !motion.isDynamic)
        {
            return;
        }

        Vector3i separation = normal.ScaleTo(penetration);
        sphere.position += separation;
        ReflectDynamicEntityFromStatic(motion, -normal);
    }
    
    void HandleEntityEntityCollisionSphere(uint entityA, uint entityB, ref EntityState stateA, ref EntityState stateB)
    {
        if (!TryGetSphereCollision(stateA.body, stateB.body, out Vector3i normal, out int penetration))
        {
            return;
        }

        bool dynamicA = entityMotionStates.TryGetValue(entityA, out var motionA) && motionA.isDynamic;
        bool dynamicB = entityMotionStates.TryGetValue(entityB, out var motionB) && motionB.isDynamic;

        Vector3i separation = normal.ScaleTo(penetration);
        if (dynamicA && dynamicB)
        {
            Vector3i half = separation.DivideByScalar(2);
            stateA.position -= half;
            stateB.position += separation - half;
            ResolveEntityCollision(motionA, motionB, normal);
        }
        else if (dynamicA)
        {
            stateA.position -= separation;
            ReflectDynamicEntityFromStatic(motionA, normal);
        }
        else if (dynamicB)
        {
            stateB.position += separation;
            ReflectDynamicEntityFromStatic(motionB, -normal);
        }
#if UNITY_EDITOR
        UnityEngine.Debug.Log($"[Collision] Entity-Entity Sphere: {entityA} <-> {entityB}");
#endif
    }

    static bool TryGetSphereCollision(CollisionBodyState body1, CollisionBodyState body2, out Vector3i normal, out int penetration)
    {
        Vector3i delta = body2.position - body1.position;
        int radiusSum = body1.colliderRadius + body2.colliderRadius;
        int distance = Vector3i.Distance(body1.position, body2.position);

        if (distance >= radiusSum)
        {
            normal = Vector3i.Zero;
            penetration = 0;
            return false;
        }

        if (distance == 0)
        {
            normal = new Vector3i(Vector3i.Scale, 0, 0);
            penetration = radiusSum;
            return true;
        }

        normal = delta.Normalize();
        penetration = radiusSum - distance;
        return true;
    }

    static bool TryGetBoxSphereCollision(CollisionBodyState box, CollisionBodyState sphere, out Vector3i normal, out int penetration)
    {
        Vector3i halfExtents = box.colliderSize.DivideByScalar(2);
        Vector3i localCenter = sphere.position - box.position;
        int clampedX = Clamp(localCenter.x, -halfExtents.x, halfExtents.x);
        int clampedY = Clamp(localCenter.y, -halfExtents.y, halfExtents.y);
        int clampedZ = Clamp(localCenter.z, -halfExtents.z, halfExtents.z);
        Vector3i closestPoint = box.position + new Vector3i(clampedX, clampedY, clampedZ);
        Vector3i delta = sphere.position - closestPoint;
        int distance = delta.Magnitude();

        if (distance > 0)
        {
            if (distance >= sphere.colliderRadius)
            {
                normal = Vector3i.Zero;
                penetration = 0;
                return false;
            }

            normal = delta.Normalize();
            penetration = sphere.colliderRadius - distance;
            return true;
        }

        int distanceToFaceX = halfExtents.x - Math.Abs(localCenter.x);
        int distanceToFaceY = halfExtents.y - Math.Abs(localCenter.y);
        int distanceToFaceZ = halfExtents.z - Math.Abs(localCenter.z);

        normal = new Vector3i(localCenter.x >= 0 ? Vector3i.Scale : -Vector3i.Scale, 0, 0);
        penetration = sphere.colliderRadius + distanceToFaceX;

        if (distanceToFaceY < penetration - sphere.colliderRadius)
        {
            normal = new Vector3i(0, localCenter.y >= 0 ? Vector3i.Scale : -Vector3i.Scale, 0);
            penetration = sphere.colliderRadius + distanceToFaceY;
        }

        if (distanceToFaceZ < penetration - sphere.colliderRadius)
        {
            normal = new Vector3i(0, 0, localCenter.z >= 0 ? Vector3i.Scale : -Vector3i.Scale);
            penetration = sphere.colliderRadius + distanceToFaceZ;
        }

        return true;
    }

    void ApplyPlayerHitToEntity(Vector3i playerVelocity, EntityMotionRuntime motion, Vector3i normal)
    {
        int approachSpeed = Vector3i.Dot(playerVelocity - motion.velocity, normal);
        if (approachSpeed <= 0)
        {
            return;
        }
        int transferSpeed = approachSpeed * (Vector3i.Scale + motion.bouncinessPermille) / Vector3i.Scale;
        int bounceSpeed = Math.Max(transferSpeed, motion.pushImpulsePerCollision);
        Vector3i normalVelocity = normal.ScaleTo(Vector3i.Dot(motion.velocity, normal));
        Vector3i tangentialVelocity = motion.velocity - normalVelocity.MultiplyByScalar(5);
        motion.velocity = tangentialVelocity + normal.ScaleTo(bounceSpeed);
#if UNITY_EDITOR
        UnityEngine.Debug.Log($"[Collision] Player hit Entity: playerVelocity={playerVelocity}, entityVelocity={motion.velocity}, normal={normal}, approachSpeed={approachSpeed}, transferSpeed={transferSpeed}, bounceSpeed={bounceSpeed}");
#endif
    }

    void ResolveEntityCollision(EntityMotionRuntime motionA, EntityMotionRuntime motionB, Vector3i normal)
    {
        int normalSpeedA = Vector3i.Dot(motionA.velocity, normal);
        int normalSpeedB = Vector3i.Dot(motionB.velocity, normal);
        int approachSpeed = normalSpeedA - normalSpeedB;
        if (approachSpeed <= 0)
        {
            return;
        }

        int restitutionPermille = (motionA.bouncinessPermille + motionB.bouncinessPermille) / 2;
        int minimumBounce = (motionA.pushImpulsePerCollision + motionB.pushImpulsePerCollision) / 2;
        int restitutionApproach = Math.Max(approachSpeed * restitutionPermille / Vector3i.Scale, minimumBounce);

        int newNormalSpeedA = (normalSpeedA + normalSpeedB - restitutionApproach) / 2;
        int newNormalSpeedB = (normalSpeedA + normalSpeedB + restitutionApproach) / 2;

        Vector3i normalVelocityA = normal.ScaleTo(normalSpeedA);
        Vector3i normalVelocityB = normal.ScaleTo(normalSpeedB);
        Vector3i tangentialVelocityA = motionA.velocity - normalVelocityA;
        Vector3i tangentialVelocityB = motionB.velocity - normalVelocityB;

        motionA.velocity = tangentialVelocityA + normal.ScaleTo(newNormalSpeedA);
        motionB.velocity = tangentialVelocityB + normal.ScaleTo(newNormalSpeedB);
        motionA.velocity = motionA.velocity.ClampMagnitude(motionA.maxSpeedPerTick);
        motionB.velocity = motionB.velocity.ClampMagnitude(motionB.maxSpeedPerTick);
    }

    void ReflectDynamicEntityFromStatic(EntityMotionRuntime motion, Vector3i normal)
    {
        int approachSpeed = Vector3i.Dot(motion.velocity, normal);
        Debug.Log($"[Collision] Reflect Entity from Static: entityVelocity={motion.velocity}, normal={normal}, approachSpeed={approachSpeed}");
        if (approachSpeed <= 0)
        {
            return;
        }

        int reflectionSpeed = Math.Max(approachSpeed * motion.bouncinessPermille / Vector3i.Scale, motion.pushImpulsePerCollision);
        Vector3i normalVelocity = normal.ScaleTo(approachSpeed);
        Vector3i tangentialVelocity = motion.velocity - normalVelocity;
        motion.velocity = tangentialVelocity - normal.ScaleTo(reflectionSpeed);
        motion.velocity = motion.velocity.ClampMagnitude(motion.maxSpeedPerTick);
    }

    static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
