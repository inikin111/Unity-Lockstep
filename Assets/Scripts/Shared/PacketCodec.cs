using System;
using System.Text;

namespace Lockstep.Packets
{
    public static class PacketCodec
    {
        private const int InputPacketByteLength = sizeof(bool) + sizeof(uint) + sizeof(uint) + sizeof(int) * 3;
        private const int FramePacketHeaderByteLength = sizeof(uint) * 2;
        private const int RequestPacketByteLength = sizeof(uint);

        public static byte[] InputPacketToBytes(InputPacket packet)
        {
            byte[] bytes = new byte[InputPacketByteLength];

            int offset = 0;
            Buffer.BlockCopy(BitConverter.GetBytes(packet.isValid), 0, bytes, offset, sizeof(bool));
            offset += sizeof(bool);
            Buffer.BlockCopy(BitConverter.GetBytes(packet.clientId), 0, bytes, offset, sizeof(uint));
            offset += sizeof(uint);
            Buffer.BlockCopy(BitConverter.GetBytes(packet.tick), 0, bytes, offset, sizeof(uint));
            offset += sizeof(uint);
            Buffer.BlockCopy(BitConverter.GetBytes(packet.inputPos.x), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(packet.inputPos.y), 0, bytes, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(BitConverter.GetBytes(packet.inputPos.z), 0, bytes, offset, sizeof(int));

            return bytes;
        }

        public static InputPacket BytesToInputPacket(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length < InputPacketByteLength)
            {
                throw new ArgumentException($"InputPacket data must be at least {InputPacketByteLength} bytes.", nameof(bytes));
            }

            return new InputPacket
            {
                isValid = BitConverter.ToBoolean(bytes, 0),
                clientId = BitConverter.ToUInt32(bytes, sizeof(bool)),
                tick = BitConverter.ToUInt32(bytes, sizeof(bool) + sizeof(uint)),
                inputPos = new InputPosition
                {
                    x = BitConverter.ToInt32(bytes, sizeof(bool) + sizeof(uint) + sizeof(uint)),
                    y = BitConverter.ToInt32(bytes, sizeof(bool) + sizeof(uint) + sizeof(uint) + sizeof(int)),
                    z = BitConverter.ToInt32(bytes, sizeof(bool) + sizeof(uint) + sizeof(uint) + sizeof(int) * 2)
                }
            };
        }

        public static byte[] StringToBytes(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return Encoding.UTF8.GetBytes(value);
        }

        public static string BytesToString(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            return Encoding.UTF8.GetString(bytes);
        }

        public static byte[] RequestPacketToBytes(RequestPacket packet)
        {
            byte[] bytes = new byte[RequestPacketByteLength];

            Buffer.BlockCopy(BitConverter.GetBytes(packet.clientId), 0, bytes, 0, sizeof(uint));

            return bytes;
        }

        public static RequestPacket BytesToRequestPacket(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length < RequestPacketByteLength)
            {
                throw new ArgumentException($"RequestPacket data must be at least {RequestPacketByteLength} bytes.", nameof(bytes));
            }

            return new RequestPacket
            {
                clientId = BitConverter.ToUInt32(bytes, 0)
            };
        }

        public static byte[] FramePacketToBytes(FramePacket packet)
        {
            InputPacket[] inputs = packet.inputs ?? Array.Empty<InputPacket>();
            byte[] bytes = new byte[FramePacketHeaderByteLength + inputs.Length * InputPacketByteLength];

            Buffer.BlockCopy(BitConverter.GetBytes(packet.tick), 0, bytes, 0, sizeof(uint));
            Buffer.BlockCopy(BitConverter.GetBytes((uint)inputs.Length), 0, bytes, sizeof(uint), sizeof(uint));

            for (int i = 0; i < inputs.Length; i++)
            {
                int offset = FramePacketHeaderByteLength + i * InputPacketByteLength;
                int inner = offset;
                Buffer.BlockCopy(BitConverter.GetBytes(inputs[i].isValid), 0, bytes, inner, sizeof(bool));
                inner += sizeof(bool);
                Buffer.BlockCopy(BitConverter.GetBytes(inputs[i].clientId), 0, bytes, inner, sizeof(uint));
                inner += sizeof(uint);
                Buffer.BlockCopy(BitConverter.GetBytes(inputs[i].tick), 0, bytes, inner, sizeof(uint));
                inner += sizeof(uint);
                Buffer.BlockCopy(BitConverter.GetBytes(inputs[i].inputPos.x), 0, bytes, inner, sizeof(int));
                inner += sizeof(int);
                Buffer.BlockCopy(BitConverter.GetBytes(inputs[i].inputPos.y), 0, bytes, inner, sizeof(int));
                inner += sizeof(int);
                Buffer.BlockCopy(BitConverter.GetBytes(inputs[i].inputPos.z), 0, bytes, inner, sizeof(int));
            }

            return bytes;
        }

        public static FramePacket BytesToFramePacket(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length < FramePacketHeaderByteLength)
            {
                throw new ArgumentException($"FramePacket data must be at least {FramePacketHeaderByteLength} bytes.", nameof(bytes));
            }

            uint tick = BitConverter.ToUInt32(bytes, 0);
            uint inputCount = BitConverter.ToUInt32(bytes, sizeof(uint));
            int expectedLength = FramePacketHeaderByteLength + (int)inputCount * InputPacketByteLength;

            if (bytes.Length < expectedLength)
            {
                throw new ArgumentException($"FramePacket data must be at least {expectedLength} bytes.", nameof(bytes));
            }

            InputPacket[] inputs = new InputPacket[inputCount];
            for (int i = 0; i < inputCount; i++)
            {
                int offset = FramePacketHeaderByteLength + i * InputPacketByteLength;
                inputs[i] = new InputPacket
                {
                    isValid = BitConverter.ToBoolean(bytes, offset),
                    clientId = BitConverter.ToUInt32(bytes, offset + sizeof(bool)),
                    tick = BitConverter.ToUInt32(bytes, offset + sizeof(bool) + sizeof(uint)),
                    inputPos = new InputPosition
                    {
                        x = BitConverter.ToInt32(bytes, offset + sizeof(bool) + sizeof(uint) + sizeof(uint)),
                        y = BitConverter.ToInt32(bytes, offset + sizeof(bool) + sizeof(uint) + sizeof(uint) + sizeof(int)),
                        z = BitConverter.ToInt32(bytes, offset + sizeof(bool) + sizeof(uint) + sizeof(uint) + sizeof(int) * 2)
                    }
                };
            }

            return new FramePacket
            {
                tick = tick,
                inputs = inputs
            };
        }
    }
}