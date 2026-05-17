using System;
using System.Text;

namespace Lockstep.Network
{
    public static class PacketCodec
    {
        private const int InputPacketByteLength = sizeof(uint) * 3;
        private const int FramePacketHeaderByteLength = sizeof(uint) * 2;
        private const int ConnectionPacketByteLength = sizeof(uint);

        public static byte[] InputPacketToBytes(InputPacket packet)
        {
            byte[] bytes = new byte[InputPacketByteLength];

            Buffer.BlockCopy(BitConverter.GetBytes(packet.clientId), 0, bytes, 0, sizeof(uint));
            Buffer.BlockCopy(BitConverter.GetBytes(packet.tick), 0, bytes, sizeof(uint), sizeof(uint));
            Buffer.BlockCopy(BitConverter.GetBytes((int)packet.input), 0, bytes, sizeof(uint) * 2, sizeof(uint));

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
                clientId = BitConverter.ToUInt32(bytes, 0),
                tick = BitConverter.ToUInt32(bytes, sizeof(uint)),
                input = (InputType)BitConverter.ToInt32(bytes, sizeof(uint) * 2)
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

        public static byte[] ConnectionPacketToBytes(ConnectionPacket packet)
        {
            byte[] bytes = new byte[ConnectionPacketByteLength];

            Buffer.BlockCopy(BitConverter.GetBytes(packet.clientId), 0, bytes, 0, sizeof(uint));

            return bytes;
        }

        public static ConnectionPacket BytesToConnectionPacket(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length < ConnectionPacketByteLength)
            {
                throw new ArgumentException($"ConnectionPacket data must be at least {ConnectionPacketByteLength} bytes.", nameof(bytes));
            }

            return new ConnectionPacket
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
                Buffer.BlockCopy(BitConverter.GetBytes(inputs[i].clientId), 0, bytes, offset, sizeof(uint));
                Buffer.BlockCopy(BitConverter.GetBytes(inputs[i].tick), 0, bytes, offset + sizeof(uint), sizeof(uint));
                Buffer.BlockCopy(BitConverter.GetBytes((int)inputs[i].input), 0, bytes, offset + sizeof(uint) * 2, sizeof(uint));
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
                    clientId = BitConverter.ToUInt32(bytes, offset),
                    tick = BitConverter.ToUInt32(bytes, offset + sizeof(uint)),
                    input = (InputType)BitConverter.ToInt32(bytes, offset + sizeof(uint) * 2)
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