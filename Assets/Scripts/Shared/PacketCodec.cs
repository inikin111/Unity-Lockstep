using System;
using System.Text;

namespace Lockstep.Packets
{
    public static class PacketCodec
    {
        private const int InputPacketByteLength = (sizeof(uint) * 2) + (sizeof(int) * 3) + sizeof(CommandType);
        private const int FramePacketHeaderByteLength = sizeof(uint);
        private const int ACKPacketByteLength = sizeof(uint);

        public static byte[] InputPacketToBytes(InputPacket packet)
        {
            byte[] bytes = new byte[InputPacketByteLength];
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

            var result = new InputPacket
            {
                clientId = BitConverter.ToUInt32(bytes, 0),
                tick = BitConverter.ToUInt32(bytes, sizeof(uint)),
                inputPos = new InputPosition
                {
                    x = BitConverter.ToInt32(bytes, sizeof(uint) * 2),
                    y = BitConverter.ToInt32(bytes, sizeof(uint) * 2 + sizeof(int)),
                    z = BitConverter.ToInt32(bytes, sizeof(uint) * 2 + sizeof(int) * 2)
                },
                commandType = (CommandType)bytes[sizeof(uint) * 2 + sizeof(int) * 3]
            };

            return result;
        }

        public static byte[] ACKPacketToBytes(ACKPacket packet)
        {
            byte[] bytes = new byte[ACKPacketByteLength];

            Buffer.BlockCopy(BitConverter.GetBytes(packet.clientId), 0, bytes, 0, sizeof(uint));

            return bytes;
        }

        public static ACKPacket BytesToACKPacket(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length < ACKPacketByteLength)
            {
                throw new ArgumentException($"ACKPacket data must be at least {ACKPacketByteLength} bytes.", nameof(bytes));
            }

            return new ACKPacket
            {
                clientId = BitConverter.ToUInt32(bytes, 0)
            };
        }

        public static byte[] FramePacketToBytes(FramePacket packet)
        {
            InputPacket[] inputs = packet.inputs ?? Array.Empty<InputPacket>();
            byte[] bytes = new byte[FramePacketHeaderByteLength + inputs.Length * InputPacketByteLength];

            Buffer.BlockCopy(BitConverter.GetBytes(packet.tick), 0, bytes, 0, sizeof(uint));

            for (int i = 0; i < inputs.Length; i++)
            {
                byte[] inputBytes = InputPacketToBytes(inputs[i]);
                Buffer.BlockCopy(inputBytes, 0, bytes, FramePacketHeaderByteLength + i * InputPacketByteLength, InputPacketByteLength);
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
            int remainingLength = bytes.Length - FramePacketHeaderByteLength;

            if (remainingLength % InputPacketByteLength != 0)
            {
                throw new ArgumentException($"FramePacket payload length must be a multiple of {InputPacketByteLength} bytes.", nameof(bytes));
            }

            uint inputCount = (uint)(remainingLength / InputPacketByteLength);
            int expectedLength = FramePacketHeaderByteLength + (int)inputCount * InputPacketByteLength;

            if (bytes.Length < expectedLength)
            {
                throw new ArgumentException($"FramePacket data must be at least {expectedLength} bytes.", nameof(bytes));
            }

            InputPacket[] inputs = new InputPacket[inputCount];
            for (int i = 0; i < inputCount; i++)
            {
                byte[] inputBytes = new byte[InputPacketByteLength];
                Buffer.BlockCopy(bytes, FramePacketHeaderByteLength + i * InputPacketByteLength, inputBytes, 0, InputPacketByteLength);
                inputs[i] = BytesToInputPacket(inputBytes);
            }

            return new FramePacket
            {
                tick = tick,
                inputs = inputs
            };
        }
    }
}