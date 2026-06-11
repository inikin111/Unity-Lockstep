using System;
using System.Collections.Generic;
using System.Linq;
using Lockstep.Packets;

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
    public RingBuffer<GameState> gameStateHistory = new RingBuffer<GameState>(50);
    // 拿脚填的数值
    int moveSpeedPerTick = 200;

    public void SimulateFrame(FramePacket framePacket)
    {
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
            return;
        }

        foreach (PlayerState player in players)
        {
            playerStates[player.clientId] = player;
        }
    }

    public void SetEntityState(EntityState[] entities)
    {
        if (entities.Length == 0)
        {
            return;
        }

        foreach (EntityState entity in entities)
        {
            entityStates[entity.entityId] = entity;
        }
    }

    public int GetGameStateChecksum(uint tick)
    {
        uint checksum = 2166136261;
        GameState gameState = gameStateHistory[tick];

        unchecked
        {
            HashPlayerStates(ref checksum, gameState.playerStates);
            HashEntityStates(ref checksum, gameState.entityStates);
            HashEntityMotionFrames(ref checksum, gameState.entityMotionFrames);
            return (int)checksum;
        }
    }

    static void HashPlayerStates(ref uint checksum, PlayerState[] states)
    {
        states ??= Array.Empty<PlayerState>();
        HashInt(ref checksum, states.Length);

        foreach (PlayerState state in states.OrderBy(state => state.clientId))
        {
            HashUInt(ref checksum, state.clientId);
            HashInt(ref checksum, (int)state.commandType);
            HashVector3i(ref checksum, state.targetPosition);
            HashVector3i(ref checksum, state.frameVelocity);
            HashCollisionBodyState(ref checksum, state.body);
        }
    }

    static void HashEntityStates(ref uint checksum, EntityState[] states)
    {
        states ??= Array.Empty<EntityState>();
        HashInt(ref checksum, states.Length);

        foreach (EntityState state in states.OrderBy(state => state.entityId))
        {
            HashUInt(ref checksum, state.entityId);
            HashCollisionBodyState(ref checksum, state.body);
        }
    }

    static void HashEntityMotionFrames(ref uint checksum, EntityMotionFrame[] frames)
    {
        frames ??= Array.Empty<EntityMotionFrame>();
        HashInt(ref checksum, frames.Length);

        foreach (EntityMotionFrame frame in frames.OrderBy(frame => frame.entityId))
        {
            HashUInt(ref checksum, frame.entityId);
            HashVector3i(ref checksum, frame.velocity);
        }
    }

    static void HashCollisionBodyState(ref uint checksum, CollisionBodyState body)
    {
        HashInt(ref checksum, (int)body.colliderType);
        HashVector3i(ref checksum, body.position);
        HashVector3i(ref checksum, body.colliderSize);
        HashInt(ref checksum, body.colliderRadius);
    }

    static void HashVector3i(ref uint checksum, Vector3i value)
    {
        HashInt(ref checksum, value.x);
        HashInt(ref checksum, value.y);
        HashInt(ref checksum, value.z);
    }

    static void HashUInt(ref uint checksum, uint value)
    {
        HashByte(ref checksum, (byte)value);
        HashByte(ref checksum, (byte)(value >> 8));
        HashByte(ref checksum, (byte)(value >> 16));
        HashByte(ref checksum, (byte)(value >> 24));
    }

    static void HashInt(ref uint checksum, int value)
    {
        HashUInt(ref checksum, (uint)value);
    }

    static void HashByte(ref uint checksum, byte value)
    {
        checksum ^= value;
        checksum *= 16777619;
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
                playerState = new PlayerState
                {
                    clientId = input.clientId,
                    commandType = CommandType.None,
                    targetPosition = Vector3i.Zero,
                    frameVelocity = Vector3i.Zero,
                    body = new CollisionBodyState
                    {
                        position = Vector3i.Zero,
                        colliderSize = PlayerSimulationConfig.ColliderSize,
                        colliderRadius = PlayerSimulationConfig.ColliderRadius,
                        colliderType = ColliderType.Sphere
                    }
                };
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

                if (distanceToTarget <= moveSpeedPerTick)
                {
                    state.position = state.targetPosition; // 到达目标位置
                    state.commandType = CommandType.None; // 停止移动
                }
                else
                {
                    Vector3i step = delta.ScaleTo(moveSpeedPerTick);
                    state.position += step;
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
    }

    void HandlePlayerEntityCollisionWall(uint playerId, uint entityId, ref PlayerState player, ref EntityState entity)
    {
        if (!TryGetBoxSphereCollision(entity.body, player.body, out Vector3i normal, out int penetration))
        {
            return;
        }

        Vector3i separation = normal.ScaleTo(penetration);
        player.position += separation;
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
        int distanceToFaceZ = halfExtents.z - Math.Abs(localCenter.z);

        normal = new Vector3i(localCenter.x >= 0 ? Vector3i.Scale : -Vector3i.Scale, 0, 0);
        penetration = sphere.colliderRadius + distanceToFaceX;

        if (distanceToFaceZ < distanceToFaceX)
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
