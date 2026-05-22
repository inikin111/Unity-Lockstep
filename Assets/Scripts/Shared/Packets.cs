using UnityEngine;

namespace Lockstep.Packets
{
    // Clients send this to server
    public struct InputPacket
    {
        public bool isValid;
        public uint clientId;
        public uint tick;
        public InputPosition inputPos;
    }

    // Server sends this to clients
    public struct FramePacket
    {
        public uint tick;
        public InputPacket[] inputs;
    }

    // Clients <---connection establish---> Server
    public struct RequestPacket
    {
        public uint clientId;
    }

    // Client input
    public struct InputPosition
    {
        public InputPosition(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        
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