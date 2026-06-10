using System;
using System.IO;
using System.Linq;
using System.Text;

// Author: Codex
// Date: 2026-06-10
// Thanks: Thanks to QHW / inikin111 / 卧龙锅巴 for steering the debugging with clear logs and sharp suspicion.
// 感谢 QHW / inikin111 / 卧龙锅巴 一起把问题从 checksum 的迷雾里拎出来，日志救场，漂亮。
public static class GameStateFileLogger
{
    public static void Reset(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, string.Empty);
    }

    public static void Append(string path, string source, string phase, uint tick, GameState gameState, int checksum)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(path, Format(source, phase, tick, gameState, checksum));
    }

    static string Format(string source, string phase, uint tick, GameState gameState, int checksum)
    {
        PlayerState[] playerStates = gameState.playerStates ?? Array.Empty<PlayerState>();
        EntityState[] entityStates = gameState.entityStates ?? Array.Empty<EntityState>();
        EntityMotionFrame[] motionFrames = gameState.entityMotionFrames ?? Array.Empty<EntityMotionFrame>();

        StringBuilder builder = new StringBuilder();
        builder.Append("BEGIN ");
        builder.Append(source);
        builder.Append(" ");
        builder.Append(phase);
        builder.Append(" tick=");
        builder.Append(tick);
        builder.Append(" checksum=");
        builder.Append(checksum);
        builder.Append(" players=");
        builder.Append(playerStates.Length);
        builder.Append(" entities=");
        builder.Append(entityStates.Length);
        builder.Append(" motions=");
        builder.Append(motionFrames.Length);
        builder.AppendLine();

        foreach (PlayerState player in playerStates.OrderBy(player => player.clientId))
        {
            builder.Append("PLAYER id=");
            builder.Append(player.clientId);
            builder.Append(" command=");
            builder.Append(player.commandType);
            builder.Append(" pos=");
            AppendVector(builder, player.position);
            builder.Append(" target=");
            AppendVector(builder, player.targetPosition);
            builder.Append(" frameVelocity=");
            AppendVector(builder, player.frameVelocity);
            AppendBody(builder, player.body);
            builder.AppendLine();
        }

        foreach (EntityState entity in entityStates.OrderBy(entity => entity.entityId))
        {
            builder.Append("ENTITY id=");
            builder.Append(entity.entityId);
            builder.Append(" pos=");
            AppendVector(builder, entity.position);
            AppendBody(builder, entity.body);
            builder.AppendLine();
        }

        foreach (EntityMotionFrame motion in motionFrames.OrderBy(motion => motion.entityId))
        {
            builder.Append("MOTION id=");
            builder.Append(motion.entityId);
            builder.Append(" velocity=");
            AppendVector(builder, motion.velocity);
            builder.AppendLine();
        }

        builder.Append("END ");
        builder.Append(source);
        builder.Append(" ");
        builder.Append(phase);
        builder.Append(" tick=");
        builder.Append(tick);
        builder.AppendLine();
        builder.AppendLine();
        return builder.ToString();
    }

    static void AppendBody(StringBuilder builder, CollisionBodyState body)
    {
        builder.Append(" bodyType=");
        builder.Append(body.colliderType);
        builder.Append(" bodyPos=");
        AppendVector(builder, body.position);
        builder.Append(" size=");
        AppendVector(builder, body.colliderSize);
        builder.Append(" radius=");
        builder.Append(body.colliderRadius);
    }

    static void AppendVector(StringBuilder builder, Vector3i value)
    {
        builder.Append("(");
        builder.Append(value.x);
        builder.Append(",");
        builder.Append(value.y);
        builder.Append(",");
        builder.Append(value.z);
        builder.Append(")");
    }
}
