namespace Lockstep.Network
{
    public struct InputPacket
    {
        public uint clientId;
        public uint tick;
        public InputType input;
    }

    public struct FramePacket
    {
        public uint tick;
        public InputPacket[] inputs;
    }

    public struct ConnectionPacket
    {
        public uint clientId;
    }

    public enum InputType : byte
    {
        Up,
        Down,
        Left,
        Right,
        None
    }
}