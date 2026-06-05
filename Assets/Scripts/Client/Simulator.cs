using System.Collections.Generic;
using System.Linq;
using Lockstep.Packets;

public struct PlayerState
{
    public uint clientId;
    public CommandType commandType;
    public Vector3i targetPosition;
    public Vector3i localPosition;
    // Collider Data : Assuming every Object has a BoxCollider and the Center is the same as the Position
    public Vector3i colliderSizes;
}

public struct EntityState
{
    public uint entityId;
    public Vector3i position;
    public Vector3i colliderSize; //  Still assuming that.
    public EntityPhysics physics;
}

public struct GameState
{
    public PlayerState[] playerStates;
    public EntityState[] entityStates;
}

public class Simulator
{    
    public Dictionary<uint, PlayerState> playerStates { get; private set; } = new Dictionary<uint, PlayerState>();
    public Dictionary<uint, EntityState> entityStates { get; private set; } = new Dictionary<uint, EntityState>();
    // 模拟层保存10tick历史状态
    public RingBuffer<GameState> gameStateHistory = new RingBuffer<GameState>(10);
    // 拿脚填的数值
    int moveSpeedPerTick = 50;

    public void SimulateFrame(FramePacket framePacket)
    {
        // UnityEngine.Debug.Log($"[Simulator] Tick={framePacket.tick}, inputCount={(framePacket.inputs == null ? 0 : framePacket.inputs.Length)}, playerCount={playerStates.Count}");
        ApplyFrameInputs(framePacket);
        CalculateMovement();
        CalculateCollision();
        SaveGameState(framePacket.tick);
    }

    public void SetPlayerState(ClientPos[] clientPositions)
    {
        foreach (ClientPos clientPos in clientPositions)
        {
            if (!playerStates.TryGetValue(clientPos.clientId, out PlayerState playerState))
            {
                playerState = new PlayerState()
                {
                    clientId = clientPos.clientId,
                    commandType = CommandType.None,
                    targetPosition = default,
                    localPosition = clientPos.position,
                    colliderSizes = new Vector3i(1, 1, 1)
                };
            }
            else
            {
                playerState.localPosition = clientPos.position;
                playerState.clientId = clientPos.clientId;
            }

            playerStates[clientPos.clientId] = playerState;
            UnityEngine.Debug.Log($"[Simulator] Sync clientId={clientPos.clientId}, position={clientPos.position}, commandType={playerState.commandType}");
        }
    }

    public void SetEntityState(EntityState[] entities)
    {
        if (entities.Length == 0)
        {
            UnityEngine.Debug.LogWarning("No Entities");
            return;
        }

        foreach (var entity in entities)
        {
            if (!entityStates.TryGetValue(entity.entityId, out var entityState))
            {
                entityState = entity;
            }
            else
            {
                entityState.position = entity.position;
                entityState.colliderSize = entity.colliderSize;
                entityState.physics = entity.physics;
            }
            entityStates[entity.entityId] = entityState;
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
                case CommandType.None:
                    break;
            }
            playerStates[input.clientId] = playerState;

            UnityEngine.Debug.Log($"[Simulator] Input clientId={input.clientId}, commandType={input.commandType}, target={playerState.targetPosition}");
        }
    }

    void CalculateMovement()
    {
        foreach (var clientId in playerStates.Keys.ToArray())
        {
            var state = playerStates[clientId];

            if (state.commandType == CommandType.Move)
            {
                Vector3i delta = state.targetPosition - state.localPosition;
                int distanceToTarget = Vector3i.Distance(state.localPosition, state.targetPosition);

                UnityEngine.Debug.Log($"[Simulator] Move clientId={clientId}, from={state.localPosition}, target={state.targetPosition}, delta={delta}, dist={distanceToTarget}, moveSpeedPerTick={moveSpeedPerTick}");

                if (distanceToTarget <= moveSpeedPerTick)
                {
                    state.localPosition = state.targetPosition; // 到达目标位置
                    state.commandType = CommandType.None; // 停止移动
                    UnityEngine.Debug.Log($"[Simulator] Arrived clientId={clientId}, position={state.localPosition}");
                }
                else
                {
                    // 原理：保持delta的方向不变，缩放delta使得它的长度等于moveSpeedPerTick
                    Vector3i step = delta * moveSpeedPerTick / distanceToTarget;
                    state.localPosition += step;
                    UnityEngine.Debug.Log($"[Simulator] Step clientId={clientId}, step={step}, newPosition={state.localPosition}");
                }

                playerStates[clientId] = state; // 写回
            }
        }
    }

    void SaveGameState(uint tick)
    {
        gameStateHistory[tick] = new GameState
        {
            playerStates = playerStates.Values.ToArray(),
            entityStates = entityStates.Values.ToArray()
        };
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
                
                if (CheckOverlap(stateA, stateB))
                {
                    HandlePlayerPlayerCollision(playerA, playerB, ref stateA, ref stateB);
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
                if (CheckOverlap(player, entity))
                {
                    HandlePlayerEntityCollision(playerId, entityId, ref player, ref entity);
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
                
                // 只处理非静态物体之间的碰撞
                if (!stateA.physics.isStatic || !stateB.physics.isStatic)
                {
                    if (CheckOverlap(stateA, stateB))
                    {
                        HandleEntityEntityCollision(entityA, entityB, ref stateA, ref stateB);
                        entityStates[entityA] = stateA;
                        entityStates[entityB] = stateB;
                    }
                }
            }
        }
    }

    bool CheckOverlap(PlayerState player1, PlayerState player2)
    {
        return CheckOverlap(player1.localPosition, player1.colliderSizes, player2.localPosition, player2.colliderSizes);
    }

    bool CheckOverlap(Vector3i center1, Vector3i colliderSize1, Vector3i center2, Vector3i colliderSize2)
    {
        Vector3i halfExtend1 = colliderSize1 / 2;
        Vector3i halfExtend2 = colliderSize2 / 2;
        return System.Math.Abs(center1.x - center2.x) <= halfExtend1.x + halfExtend2.x
            && System.Math.Abs(center1.y - center2.y) <= halfExtend1.y + halfExtend2.y
            && System.Math.Abs(center1.z - center2.z) <= halfExtend1.z + halfExtend2.z;
    }

    bool CheckOverlap(PlayerState player, EntityState entity)
    {
        return CheckOverlap(player.localPosition, player.colliderSizes, entity.position, entity.colliderSize);
    }

    bool CheckOverlap(EntityState entity1, EntityState entity2)
    {
        return CheckOverlap(entity1.position, entity1.colliderSize, entity2.position, entity2.colliderSize);
    }

    void HandlePlayerPlayerCollision(uint playerA, uint playerB, ref PlayerState stateA, ref PlayerState stateB)
    {
        // 计算碰撞法线和重叠深度
        Vector3i diff = stateA.localPosition - stateB.localPosition;
        Vector3i halfExtendA = stateA.colliderSizes / 2;
        Vector3i halfExtendB = stateB.colliderSizes / 2;
        
        // 计算各轴的重叠深度
        Vector3i overlap = halfExtendA + halfExtendB - new Vector3i(
            System.Math.Abs(diff.x),
            System.Math.Abs(diff.y),
            System.Math.Abs(diff.z)
        );
        
        // 找出最小的穿透方向（碰撞法线）
        Vector3i separation = Vector3i.Zero;
        if (overlap.x <= overlap.y && overlap.x <= overlap.z)
        {
            int direction = diff.x > 0 ? 1 : -1;
            separation = new Vector3i(direction * overlap.x, 0, 0);
        }
        else if (overlap.y <= overlap.z)
        {
            int direction = diff.y > 0 ? 1 : -1;
            separation = new Vector3i(0, direction * overlap.y, 0);
        }
        else
        {
            int direction = diff.z > 0 ? 1 : -1;
            separation = new Vector3i(0, 0, direction * overlap.z);
        }
        
        // 分离两个玩家（各移动一半重叠深度）
        Vector3i halfSeparation = separation / 2;
        stateA.localPosition += halfSeparation;
        stateB.localPosition -= halfSeparation;
        
        UnityEngine.Debug.Log($"[Collision] Player-Player: {playerA} <-> {playerB}, separated by {separation}");
    }
    
    void HandlePlayerEntityCollision(uint playerId, uint entityId, ref PlayerState player, ref EntityState entity)
    {
        if (entity.physics.isStatic)
        {
            // 静态物体：将玩家推开
            Vector3i diff = player.localPosition - entity.position;
            Vector3i halfPlayerExtend = player.colliderSizes / 2;
            Vector3i halfEntityExtend = entity.colliderSize / 2;
            
            // 计算重叠深度
            Vector3i overlap = halfPlayerExtend + halfEntityExtend - new Vector3i(
                System.Math.Abs(diff.x),
                System.Math.Abs(diff.y),
                System.Math.Abs(diff.z)
            );
            
            // 找出最小穿透方向
            Vector3i separation = Vector3i.Zero;
            if (overlap.x <= overlap.y && overlap.x <= overlap.z)
            {
                int direction = diff.x > 0 ? 1 : -1;
                separation = new Vector3i(direction * overlap.x, 0, 0);
            }
            else if (overlap.y <= overlap.z)
            {
                int direction = diff.y > 0 ? 1 : -1;
                separation = new Vector3i(0, direction * overlap.y, 0);
            }
            else
            {
                int direction = diff.z > 0 ? 1 : -1;
                separation = new Vector3i(0, 0, direction * overlap.z);
            }
            
            // 将玩家移出碰撞区域
            player.localPosition += separation;
            
            UnityEngine.Debug.Log($"[Collision] Player-Entity (static): Player {playerId} pushed by {separation}");
        }
        else
        {
            // 动态物体：给物体施加力（玩家不受影响）
            Vector3i diff = entity.position - player.localPosition;
            int distance = Vector3i.Distance(player.localPosition, entity.position);
            
            if (distance > 0)
            {
                // 计算碰撞方向和力
                Vector3i collisionDir = diff / distance;
                Vector3i force = collisionDir * 100; // 推力大小
                
                // F = ma，所以 a = F/m
                entity.physics.acceleration = force / entity.physics.mass;
                
                // 更新速度：v = v + a*dt（假设dt=1 tick）
                entity.physics.velocity += entity.physics.acceleration;
                
                // 应用摩擦力
                if (entity.physics.friction > 0)
                {
                    Vector3i friction = entity.physics.velocity * (-entity.physics.friction) / 100;
                    entity.physics.velocity += friction;
                }
                
                // 分离位置避免持续碰撞
                Vector3i halfPlayerExtend = player.colliderSizes / 2;
                Vector3i halfEntityExtend = entity.colliderSize / 2;
                Vector3i overlap = halfPlayerExtend + halfEntityExtend - new Vector3i(
                    System.Math.Abs(diff.x),
                    System.Math.Abs(diff.y),
                    System.Math.Abs(diff.z)
                );
                
                Vector3i separation = Vector3i.Zero;
                if (overlap.x <= overlap.y && overlap.x <= overlap.z)
                {
                    int direction = diff.x > 0 ? 1 : -1;
                    separation = new Vector3i(direction * overlap.x, 0, 0);
                }
                else if (overlap.y <= overlap.z)
                {
                    int direction = diff.y > 0 ? 1 : -1;
                    separation = new Vector3i(0, direction * overlap.y, 0);
                }
                else
                {
                    int direction = diff.z > 0 ? 1 : -1;
                    separation = new Vector3i(0, 0, direction * overlap.z);
                }
                
                entity.position += separation;
                
                UnityEngine.Debug.Log($"[Collision] Player-Entity (dynamic): Entity {entityId} pushed, force={force}");
            }
        }
    }
    
    void HandleEntityEntityCollision(uint entityA, uint entityB, ref EntityState stateA, ref EntityState stateB)
    {
        // 计算碰撞参数
        Vector3i diff = stateA.position - stateB.position;
        int distance = Vector3i.Distance(stateA.position, stateB.position);
        
        if (distance == 0) return;
        
        Vector3i collisionNormal = diff / distance;
        Vector3i halfExtendA = stateA.colliderSize / 2;
        Vector3i halfExtendB = stateB.colliderSize / 2;
        
        // 计算重叠深度
        Vector3i overlap = halfExtendA + halfExtendB - new Vector3i(
            System.Math.Abs(diff.x),
            System.Math.Abs(diff.y),
            System.Math.Abs(diff.z)
        );
        
        // 找出最小穿透方向
        Vector3i separation = Vector3i.Zero;
        if (overlap.x <= overlap.y && overlap.x <= overlap.z)
        {
            int direction = diff.x > 0 ? 1 : -1;
            separation = new Vector3i(direction * overlap.x, 0, 0);
        }
        else if (overlap.y <= overlap.z)
        {
            int direction = diff.y > 0 ? 1 : -1;
            separation = new Vector3i(0, direction * overlap.y, 0);
        }
        else
        {
            int direction = diff.z > 0 ? 1 : -1;
            separation = new Vector3i(0, 0, direction * overlap.z);
        }
        
        // 根据质量分配分离距离
        if (stateA.physics.isStatic)
        {
            // A是静态的，只移动B
            stateB.position -= separation;
        }
        else if (stateB.physics.isStatic)
        {
            // B是静态的，只移动A
            stateA.position += separation;
        }
        else
        {
            // 两个都是动态的，根据质量比例分配
            int totalMass = stateA.physics.mass + stateB.physics.mass;
            float ratioA = (float)stateB.physics.mass / totalMass;
            float ratioB = (float)stateA.physics.mass / totalMass;
            
            stateA.position += separation * (int)(ratioA * 1000) / 1000;
            stateB.position -= separation * (int)(ratioB * 1000) / 1000;
            
            // 动量守恒：计算新的速度
            // v1_new = (v1*m1 - v2*m2 + 2*m2*v2) / (m1+m2)  (简化版本)
            Vector3i relVel = stateA.physics.velocity - stateB.physics.velocity;
            int velAlongNormal = relVel.x * collisionNormal.x + 
                               relVel.y * collisionNormal.y + 
                               relVel.z * collisionNormal.z;
            
            // 如果物体正在分离，不需要处理
            if (velAlongNormal > 0) return;
            
            // 弹性碰撞系数
            float restitution = 0.5f;
            float j = -(1 + restitution) * velAlongNormal;
            j /= (1f / stateA.physics.mass + 1f / stateB.physics.mass);
            
            Vector3i impulse = collisionNormal * (int)(j * 1000) / 1000;
            
            stateA.physics.velocity += impulse / stateA.physics.mass;
            stateB.physics.velocity -= impulse / stateB.physics.mass;
            
            // 应用摩擦力
            if (stateA.physics.friction > 0)
            {
                Vector3i frictionA = stateA.physics.velocity * (-stateA.physics.friction) / 100;
                stateA.physics.velocity += frictionA;
            }
            if (stateB.physics.friction > 0)
            {
                Vector3i frictionB = stateB.physics.velocity * (-stateB.physics.friction) / 100;
                stateB.physics.velocity += frictionB;
            }
        }
        
        UnityEngine.Debug.Log($"[Collision] Entity-Entity: {entityA} <-> {entityB}, handled");
    }
}