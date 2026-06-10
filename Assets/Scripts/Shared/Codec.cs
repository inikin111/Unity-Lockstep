using System;

namespace Lockstep.Packets
{
    public static class PacketCodec
    {
        const int InputPacketLength = (sizeof(uint) * 2) + (sizeof(int) * 3) + sizeof(CommandType);
        const int FramePacketHeaderLength = sizeof(uint);
        const int ACKPacketHeaderLength = sizeof(uint);
        const int ClientPosLength = sizeof(uint) + (sizeof(int) * 3);
        const int PacketHeaderLength = sizeof(PacketType);
        const int CollisionBodyStateLength = sizeof(ColliderType) + (sizeof(int) * 6) + sizeof(int);
        const int PlayerStateLength = sizeof(uint) + sizeof(CommandType) + (sizeof(int) * 6) + CollisionBodyStateLength;
        const int EntityStateLength = sizeof(uint) + CollisionBodyStateLength;
        const int EntityMotionFrameLength = sizeof(uint) + (sizeof(int) * 3);
        const int StateSyncPacketHeaderLength = sizeof(uint) + sizeof(int) * 2;

        public static byte[] PacketHeaderToBytes(PacketHeader header)
        {
            return new byte[] { (byte)header.packetType };
        }

        public static PacketHeader ReadPacketHeaderFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 1)
            {
                throw new ArgumentException("Byte array must contain at least 1 byte to convert to PacketHeader.", nameof(bytes));
            }

            return new PacketHeader
            {
                packetType = (PacketType)bytes[0]
            };
        }
        
        public static byte[] StateSyncPacketToBytes(StateSyncPacket packet)
        {
            PlayerState[] playerStates = packet.gameState.playerStates ?? Array.Empty<PlayerState>();
            EntityState[] entityStates = packet.gameState.entityStates ?? Array.Empty<EntityState>();
            EntityMotionFrame[] entityMotionFrames = packet.gameState.entityMotionFrames ?? Array.Empty<EntityMotionFrame>();
            byte[] bytes = new byte[StateSyncPacketHeaderLength + GetGameStateByteLength(packet.gameState)];
            int offset = 0;

            Buffer.BlockCopy(BitConverter.GetBytes(packet.tick), 0, bytes, offset, sizeof(uint));
            offset += sizeof(uint);
            Buffer.BlockCopy(BitConverter.GetBytes(playerStates.Length), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(entityStates.Length), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);

            for (int i = 0; i < playerStates.Length; i++)
            {
                byte[] playerBytes = PlayerStateToBytes(playerStates[i]);
                Buffer.BlockCopy(playerBytes, 0, bytes, offset, playerBytes.Length);
                offset += playerBytes.Length;
            }

            for (int i = 0; i < entityStates.Length; i++)
            {
                byte[] entityBytes = EntityStateToBytes(entityStates[i]);
                Buffer.BlockCopy(entityBytes, 0, bytes, offset, entityBytes.Length);
                offset += entityBytes.Length;
            }

            for (int i = 0; i < entityMotionFrames.Length; i++)
            {
                byte[] motionFrameBytes = EntityMotionFrameToBytes(entityMotionFrames[i]);
                Buffer.BlockCopy(motionFrameBytes, 0, bytes, offset, motionFrameBytes.Length);
                offset += motionFrameBytes.Length;
            }

            return bytes;
        }

        public static StateSyncPacket ReadStateSyncPacketBody(byte[] bytes, int offset = PacketHeaderLength)
        {
            return BytesToStateSyncPacket(bytes, offset);
        }

        public static StateSyncPacket BytesToStateSyncPacket(byte[] bytes, int offset = 0)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            EnsureAvailable(bytes, offset, StateSyncPacketHeaderLength, nameof(StateSyncPacket));

            uint tick = BitConverter.ToUInt32(bytes, offset);
            offset += sizeof(uint);
            int playerCount = BitConverter.ToInt32(bytes, offset);
            offset += sizeof(int);
            int entityCount = BitConverter.ToInt32(bytes, offset);
            offset += sizeof(int);

            if (playerCount < 0)
            {
                throw new ArgumentException("StateSyncPacket playerCount cannot be negative.", nameof(bytes));
            }

            if (entityCount < 0)
            {
                throw new ArgumentException("StateSyncPacket entityCount cannot be negative.", nameof(bytes));
            }

            EnsureAvailable(bytes, offset, playerCount * PlayerStateLength + entityCount * EntityStateLength, nameof(StateSyncPacket));

            PlayerState[] playerStates = new PlayerState[playerCount];
            for (int i = 0; i < playerStates.Length; i++)
            {
                playerStates[i] = BytesToPlayerState(bytes, offset);
                offset += PlayerStateLength;
            }

            EntityState[] entityStates = new EntityState[entityCount];
            for (int i = 0; i < entityStates.Length; i++)
            {
                entityStates[i] = BytesToEntityState(bytes, offset);
                offset += EntityStateLength;
            }

            int remainingLength = bytes.Length - offset;
            if (remainingLength % EntityMotionFrameLength != 0)
            {
                throw new ArgumentException($"StateSyncPacket motion frame payload length must be a multiple of {EntityMotionFrameLength} bytes.", nameof(bytes));
            }

            EntityMotionFrame[] entityMotionFrames = new EntityMotionFrame[remainingLength / EntityMotionFrameLength];
            for (int i = 0; i < entityMotionFrames.Length; i++)
            {
                entityMotionFrames[i] = BytesToEntityMotionFrame(bytes, offset);
                offset += EntityMotionFrameLength;
            }

            return new StateSyncPacket
            {
                tick = tick,
                playerCount = playerCount,
                entityCount = entityCount,
                gameState = new GameState
                {
                    playerStates = playerStates,
                    entityStates = entityStates,
                    entityMotionFrames = entityMotionFrames
                }
            };
        }

        static int GetGameStateByteLength(GameState gameState)
        {
            int playerCount = gameState.playerStates?.Length ?? 0;
            int entityCount = gameState.entityStates?.Length ?? 0;
            int entityMotionFrameCount = gameState.entityMotionFrames?.Length ?? 0;

            return playerCount * PlayerStateLength
                + entityCount * EntityStateLength
                + entityMotionFrameCount * EntityMotionFrameLength;
        }

        public static byte[] EntityStateToBytes(EntityState state)
        {
            byte[] bytes = new byte[EntityStateLength];
            int offset = 0;

            Buffer.BlockCopy(BitConverter.GetBytes(state.entityId), 0, bytes, offset, sizeof(uint));
            offset += sizeof(uint);
            byte[] bodyBytes = CollisionBodyStateToBytes(state.body);
            Buffer.BlockCopy(bodyBytes, 0, bytes, offset, bodyBytes.Length);

            return bytes;
        }

        public static EntityState BytesToEntityState(byte[] bytes, int offset = 0)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            EnsureAvailable(bytes, offset, EntityStateLength, nameof(EntityState));

            return new EntityState
            {
                entityId = BitConverter.ToUInt32(bytes, offset),
                body = BytesToCollisionBodyState(bytes, offset + sizeof(uint))
            };
        }

        public static byte[] EntityMotionFrameToBytes(EntityMotionFrame frame)
        {
            byte[] bytes = new byte[EntityMotionFrameLength];
            int offset = 0;

            Buffer.BlockCopy(BitConverter.GetBytes(frame.entityId), 0, bytes, offset, sizeof(uint));
            offset += sizeof(uint);
            Buffer.BlockCopy(BitConverter.GetBytes(frame.velocity.x), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(frame.velocity.y), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(frame.velocity.z), 0, bytes, offset, sizeof(int));

            return bytes;
        }

        public static EntityMotionFrame BytesToEntityMotionFrame(byte[] bytes, int offset = 0)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            EnsureAvailable(bytes, offset, EntityMotionFrameLength, nameof(EntityMotionFrame));

            return new EntityMotionFrame
            {
                entityId = BitConverter.ToUInt32(bytes, offset),
                velocity = new Vector3i
                {
                    x = BitConverter.ToInt32(bytes, offset + sizeof(uint)),
                    y = BitConverter.ToInt32(bytes, offset + sizeof(uint) + sizeof(int)),
                    z = BitConverter.ToInt32(bytes, offset + sizeof(uint) + sizeof(int) * 2)
                }
            };
        }

        public static byte[] PlayerStateToBytes(PlayerState state)
        {
            byte[] bytes = new byte[sizeof(uint) + sizeof(CommandType) + sizeof(int) * 6 + CollisionBodyStateLength];
            int offset = 0;

            Buffer.BlockCopy(BitConverter.GetBytes(state.clientId), 0, bytes, offset, sizeof(uint));
            offset += sizeof(uint);
            bytes[offset] = (byte)state.commandType;
            offset += sizeof(CommandType);
            Buffer.BlockCopy(BitConverter.GetBytes(state.targetPosition.x), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(state.targetPosition.y), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(state.targetPosition.z), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(state.frameVelocity.x), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(state.frameVelocity.y), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(state.frameVelocity.z), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            byte[] bodyBytes = CollisionBodyStateToBytes(state.body);
            Buffer.BlockCopy(bodyBytes, 0, bytes, offset, bodyBytes.Length);

            return bytes;
        }

        public static PlayerState BytesToPlayerState(byte[] bytes, int offset = 0)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            EnsureAvailable(bytes, offset, PlayerStateLength, nameof(PlayerState));

            return new PlayerState
            {
                clientId = BitConverter.ToUInt32(bytes, offset),
                commandType = (CommandType)bytes[offset + sizeof(uint)],
                targetPosition = new Vector3i
                {
                    x = BitConverter.ToInt32(bytes, offset + sizeof(uint) + sizeof(CommandType)),
                    y = BitConverter.ToInt32(bytes, offset + sizeof(uint) + sizeof(CommandType) + sizeof(int)),
                    z = BitConverter.ToInt32(bytes, offset + sizeof(uint) + sizeof(CommandType) + sizeof(int) * 2)
                },
                frameVelocity = new Vector3i
                {
                    x = BitConverter.ToInt32(bytes, offset + sizeof(uint) + sizeof(CommandType) + sizeof(int) * 3),
                    y = BitConverter.ToInt32(bytes, offset + sizeof(uint) + sizeof(CommandType) + sizeof(int) * 4),
                    z = BitConverter.ToInt32(bytes, offset + sizeof(uint) + sizeof(CommandType) + sizeof(int) * 5)
                },
                body = BytesToCollisionBodyState(bytes, offset + sizeof(uint) + sizeof(CommandType) + sizeof(int) * 6)
            };
        }

        public static byte[] CollisionBodyStateToBytes(CollisionBodyState body)
        {
            byte[] bytes = new byte[sizeof(ColliderType) + sizeof(int) * 6 + sizeof(int)];
            int offset = 0;

            bytes[offset] = (byte)body.colliderType;
            offset += sizeof(ColliderType);
            Buffer.BlockCopy(BitConverter.GetBytes(body.position.x), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(body.position.y), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(body.position.z), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(body.colliderSize.x), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(body.colliderSize.y), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(body.colliderSize.z), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(body.colliderRadius), 0, bytes, offset, sizeof(int));

            return bytes;
        }

        public static CollisionBodyState BytesToCollisionBodyState(byte[] bytes, int offset = 0)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            EnsureAvailable(bytes, offset, CollisionBodyStateLength, nameof(CollisionBodyState));

            return new CollisionBodyState
            {
                colliderType = (ColliderType)bytes[offset],
                position = new Vector3i
                {
                    x = BitConverter.ToInt32(bytes, offset + sizeof(ColliderType)),
                    y = BitConverter.ToInt32(bytes, offset + sizeof(ColliderType) + sizeof(int)),
                    z = BitConverter.ToInt32(bytes, offset + sizeof(ColliderType) + sizeof(int) * 2)
                },
                colliderSize = new Vector3i
                {
                    x = BitConverter.ToInt32(bytes, offset + sizeof(ColliderType) + sizeof(int) * 3),
                    y = BitConverter.ToInt32(bytes, offset + sizeof(ColliderType) + sizeof(int) * 4),
                    z = BitConverter.ToInt32(bytes, offset + sizeof(ColliderType) + sizeof(int) * 5)
                },
                colliderRadius = BitConverter.ToInt32(bytes, offset + sizeof(ColliderType) + sizeof(int) * 6)
            };
        }

        public static InputPacket ReadInputPacketBody(byte[] bytes, int offset = PacketHeaderLength)
        {
            return BytesToInputPacket(bytes, offset);
        }

        public static byte[] InputPacketToBytes(InputPacket packet)
        {
            byte[] bytes = new byte[InputPacketLength];
            int offset = 0;

            Buffer.BlockCopy(BitConverter.GetBytes(packet.clientId), 0, bytes, offset, sizeof(uint));
            offset += sizeof(uint);
            Buffer.BlockCopy(BitConverter.GetBytes(packet.tick), 0, bytes, offset, sizeof(uint));
            offset += sizeof(uint);
            Buffer.BlockCopy(BitConverter.GetBytes(packet.inputPos.x), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(packet.inputPos.y), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(packet.inputPos.z), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            bytes[offset] = (byte)packet.commandType;

            return bytes;
        }

        public static InputPacket BytesToInputPacket(byte[] bytes, int offset = 0)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length < InputPacketLength)
            {
                throw new ArgumentException($"InputPacket data must be at least {InputPacketLength} bytes.", nameof(bytes));
            }

            var result = new InputPacket
            {
                clientId = BitConverter.ToUInt32(bytes, offset),
                tick = BitConverter.ToUInt32(bytes, offset + sizeof(uint)),
                inputPos = new Vector3i
                {
                    x = BitConverter.ToInt32(bytes, offset + sizeof(uint) * 2),
                    y = BitConverter.ToInt32(bytes, offset + sizeof(uint) * 2 + sizeof(int)),
                    z = BitConverter.ToInt32(bytes, offset + sizeof(uint) * 2 + sizeof(int) * 2)
                },
                commandType = (CommandType)bytes[offset + sizeof(uint) * 2 + sizeof(int) * 3]
            };

            return result;
        }

        public static byte[] ACKPacketToBytes(ACKPacket packet)
        {
            byte[] bytes = new byte[ACKPacketHeaderLength + packet.clientPos.Length * ClientPosLength];

            Buffer.BlockCopy(BitConverter.GetBytes(packet.clientId), 0, bytes, 0, sizeof(uint));
            for (int i = 0; i < packet.clientPos.Length; i++)
            {
                int offset = ACKPacketHeaderLength + i * ClientPosLength;
                Buffer.BlockCopy(BitConverter.GetBytes(packet.clientPos[i].id), 0, bytes, offset, sizeof(uint));
                offset += sizeof(uint);
                Buffer.BlockCopy(BitConverter.GetBytes(packet.clientPos[i].X), 0, bytes, offset, sizeof(int));
                offset += sizeof(int);
                Buffer.BlockCopy(BitConverter.GetBytes(packet.clientPos[i].Y), 0, bytes, offset, sizeof(int));
                offset += sizeof(int);
                Buffer.BlockCopy(BitConverter.GetBytes(packet.clientPos[i].Z), 0, bytes, offset, sizeof(int));
            }

            return bytes;
        }

        public static ACKPacket ReadACKPacketBody(byte[] bytes, int offset = PacketHeaderLength)
        {
            return BytesToACKPacket(bytes, offset);
        }

        public static ACKPacket BytesToACKPacket(byte[] bytes, int offset = 0)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length < ACKPacketHeaderLength)
            {
                throw new ArgumentException($"ACKPacket data must be at least {ACKPacketHeaderLength} bytes.", nameof(bytes));
            }

            ClientPos[] positions = new ClientPos[(bytes.Length - offset - ACKPacketHeaderLength) / ClientPosLength];
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = new ClientPos
                {
                    id = BitConverter.ToUInt32(bytes, offset + ACKPacketHeaderLength + i * ClientPosLength),
                    position = new Vector3i
                    {
                        x = BitConverter.ToInt32(bytes, offset + ACKPacketHeaderLength + i * ClientPosLength + sizeof(uint)),
                        y = BitConverter.ToInt32(bytes, offset + ACKPacketHeaderLength + i * ClientPosLength + sizeof(uint) + sizeof(int)),
                        z = BitConverter.ToInt32(bytes, offset + ACKPacketHeaderLength + i * ClientPosLength + sizeof(uint) + sizeof(int) * 2)
                    }
                };
            }

            return new ACKPacket
            {
                clientId = BitConverter.ToUInt32(bytes, offset),
                clientPos = positions
            };
        }

        public static FramePacket ReadFramePacketBody(byte[] bytes, int offset = PacketHeaderLength)
        {
            return BytesToFramePacket(bytes, offset);
        }

        public static byte[] FramePacketToBytes(FramePacket packet)
        {
            InputPacket[] inputs = packet.inputs ?? Array.Empty<InputPacket>();
            byte[] bytes = new byte[FramePacketHeaderLength + inputs.Length * InputPacketLength];

            Buffer.BlockCopy(BitConverter.GetBytes(packet.tick), 0, bytes, 0, sizeof(uint));

            for (int i = 0; i < inputs.Length; i++)
            {
                WriteInputPacket(inputs[i], bytes, FramePacketHeaderLength + i * InputPacketLength);
            }

            return bytes;
        }

        public static FramePacket BytesToFramePacket(byte[] bytes, int offset = 0)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length < FramePacketHeaderLength)
            {
                throw new ArgumentException($"FramePacket data must be at least {FramePacketHeaderLength} bytes.", nameof(bytes));
            }

            uint tick = BitConverter.ToUInt32(bytes, offset);
            int remainingLength = bytes.Length - offset - FramePacketHeaderLength;

            if (remainingLength % InputPacketLength != 0)
            {
                throw new ArgumentException($"FramePacket payload length must be a multiple of {InputPacketLength} bytes.", nameof(bytes));
            }

            uint inputCount = (uint)(remainingLength / InputPacketLength);
            int expectedLength = FramePacketHeaderLength + (int)inputCount * InputPacketLength;

            if (bytes.Length < expectedLength)
            {
                throw new ArgumentException($"FramePacket data must be at least {expectedLength} bytes.", nameof(bytes));
            }

            InputPacket[] inputs = new InputPacket[inputCount];
            for (int i = 0; i < inputCount; i++)
            {
                inputs[i] = ReadInputPacket(bytes, offset + FramePacketHeaderLength + i * InputPacketLength);
            }

            return new FramePacket
            {
                tick = tick,
                inputs = inputs
            };
        }

        static void WriteInputPacket(InputPacket packet, byte[] bytes, int offset)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(packet.clientId), 0, bytes, offset, sizeof(uint));
            offset += sizeof(uint);
            Buffer.BlockCopy(BitConverter.GetBytes(packet.tick), 0, bytes, offset, sizeof(uint));
            offset += sizeof(uint);
            Buffer.BlockCopy(BitConverter.GetBytes(packet.inputPos.x), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(packet.inputPos.y), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(packet.inputPos.z), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            bytes[offset] = (byte)packet.commandType;
        }

        static InputPacket ReadInputPacket(byte[] bytes, int offset)
        {
            return new InputPacket
            {
                clientId = BitConverter.ToUInt32(bytes, offset),
                tick = BitConverter.ToUInt32(bytes, offset + sizeof(uint)),
                inputPos = new Vector3i
                {
                    x = BitConverter.ToInt32(bytes, offset + sizeof(uint) * 2),
                    y = BitConverter.ToInt32(bytes, offset + sizeof(uint) * 2 + sizeof(int)),
                    z = BitConverter.ToInt32(bytes, offset + sizeof(uint) * 2 + sizeof(int) * 2)
                },
                commandType = (CommandType)bytes[offset + sizeof(uint) * 2 + sizeof(int) * 3]
            };
        }

        static void EnsureAvailable(byte[] bytes, int offset, int length, string packetName)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative.");
            }

            if (length < 0 || bytes.Length - offset < length)
            {
                throw new ArgumentException($"{packetName} data must contain at least {length} bytes from offset {offset}.", nameof(bytes));
            }
        }
    }
}
