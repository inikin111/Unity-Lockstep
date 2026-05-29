using System.Runtime.InteropServices;

namespace Lockstep.Packets
{
    public enum CommandType : byte
    {
        Move,
        Cancel,
        None
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    // Clients send this to server
    public struct InputPacket
    {
        public uint clientId;
        public uint tick;
        public Position inputPos;
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
    }

    // Client input
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Position
    {
        public int x;
        public int y;
        public int z;
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