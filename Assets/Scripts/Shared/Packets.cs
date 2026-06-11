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
        Input,       // 客户端发送玩家输入数据
        Frame,       // 汇总帧数据
        ACK,         // 连接请求和确认应答
        StateSync,   // 服务端发送游戏状态快照
        ResendFrame, // 主动请求补发
        PlayerJoin   // 玩家加入通知
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketHeader
    {
        public PacketType packetType;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StateSyncPacket
    {
        public uint tick;
        public int playerCount;
        public int entityCount;
        public GameState gameState;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ResendFramePacket
    {
        public uint lastReceivedTick;
        public uint requestedTick;
        public FramePacket[] framePackets;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PlayerJoinPacket
    {
        public uint clientId;
        public uint joinTick;
        public Vector3i spawnPosition;
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
