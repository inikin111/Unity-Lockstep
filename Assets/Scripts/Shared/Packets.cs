using System.Runtime.InteropServices;

namespace Lockstep.Packets
{
    public enum CommandType : byte
    {
        Move,
        Cancel,
        None
    }

    public enum PacketType : byte
    {
        Input,
        Frame,
        ACK,
        StateSync,
        ResendFrame
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketHeader
    {
        public PacketType packetType;
    }

    // [StructLayout(LayoutKind.Sequential, Pack = 1)]
    // public struct StateSyncPacket
    // {
    //     public uint tick;
    //     public GameState gameState;
    // }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ResendFramePacket
    {
        public uint lastReceivedTick;
        public uint requestedTick;
        public FramePacket[] framePackets;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    // Clients send this to server
    public struct InputPacket
    {
        public uint clientId;
        public uint tick;
        public Vector3i inputPos;
        public CommandType commandType;
    }

    // Server sends this to clients
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FramePacket
    {
        public uint tick;
        public InputPacket[] inputs;
    }

    // Clients <---connection establish---> Server
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ACKPacket
    {
        public uint clientId;
        public ClientPos[] clientPos;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ClientPos
    {
        public uint id;
        public Vector3i position;
        public int X => position.x;
        public int Y => position.y;
        public int Z => position.z;
    }

    // Legacy design, just keep it for now
    public enum InputType : byte
    {
        Up,
        Down,
        Left,
        Right,
        None
    }
}