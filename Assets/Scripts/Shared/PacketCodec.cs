using System;

namespace Lockstep.Packets
{
    public static class PacketCodec
    {
        const int InputPacketLength = (sizeof(uint) * 2) + (sizeof(int) * 3) + sizeof(CommandType);
        const int FramePacketHeaderLength = sizeof(uint);
        const int ACKPacketHeaderLength = sizeof(uint) * 2;
        const int ClientPosLength = sizeof(uint) + (sizeof(int) * 3);
        const int PacketHeaderLength = sizeof(PacketType);

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
            Buffer.BlockCopy(BitConverter.GetBytes(packet.startTick), 0, bytes, sizeof(uint), sizeof(uint));
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
                startTick = BitConverter.ToUInt32(bytes, offset + sizeof(uint)),
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
    }
}
