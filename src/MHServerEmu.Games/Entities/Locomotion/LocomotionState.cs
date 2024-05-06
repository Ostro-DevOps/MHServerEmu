﻿using System.Text;
using Google.ProtocolBuffers;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Serialization;
using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.Common;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Navi;

namespace MHServerEmu.Games.Entities.Locomotion
{
    [Flags]
    public enum LocomotionMessageFlags : uint
    {
        None                    = 0,
        HasFullOrientation      = 1 << 0,
        NoLocomotionState       = 1 << 1,
        RelativeToPreviousState = 1 << 2,   // See LocomotionState::GetFieldFlags()
        HasLocomotionFlags      = 1 << 3,
        HasMethod               = 1 << 4,
        UpdatePathNodes         = 1 << 5,
        LocomotionFinished      = 1 << 6,
        HasMoveSpeed            = 1 << 7,
        HasHeight               = 1 << 8,
        HasFollowEntityId       = 1 << 9,
        HasFollowEntityRange    = 1 << 10,
        HasEntityPrototypeId    = 1 << 11
    }

    [Flags]
    public enum LocomotionFlags : ulong
    {
        None                        = 0,
        IsLocomoting                = 1 << 0,
        IsWalking                   = 1 << 1,
        IsLooking                   = 1 << 2,
        SkipCurrentSpeedRate        = 1 << 3,
        LocomotionNoEntityCollide   = 1 << 4,
        IsMovementPower             = 1 << 5,
        DisableOrientation          = 1 << 6,
        IsDrivingMovementMode       = 1 << 7,
        MoveForward                 = 1 << 8,
        MoveTo                      = 1 << 9,
        IsSyncMoving                = 1 << 10,
        IgnoresWorldCollision       = 1 << 11,
    }

    public class LocomotionState // TODO: Change to struct? Consider how this is used before doing it
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        // NOTE: Due to how LocomotionState serialization is implemented, we should be able to
        // get away with using C# auto properties instead of private fields.
        public LocomotionFlags LocomotionFlags { get; set; }
        public LocomotorMethod Method { get; set; }
        public float BaseMoveSpeed { get; set; }
        public int Height { get; set; }
        public ulong FollowEntityId { get; set; }
        public float FollowEntityRangeStart { get; set; }
        public float FollowEntityRangeEnd { get; set; }
        public int PathGoalNodeIndex { get; set; }
        public List<NaviPathNode> PathNodes { get; set; } = new();

        public LocomotionState()
        {
            Method = LocomotorMethod.Default;
            PathNodes = new();
        }

        public LocomotionState(float baseMoveSpeed)
        {
            BaseMoveSpeed = baseMoveSpeed;
            PathNodes = new();
        }

        public LocomotionState(LocomotionState other)
        {
            Set(other);
        }

        // NOTE: LocomotionState serialization implementation is similar to what PowerCollection is doing
        // (i.e. separate static serialization methods for serialization and deserialization rather than
        // combined ISerialize implementention we have seen everywhere else).

        public static bool SerializeTo(Archive archive, NaviPathNode pathNode, Vector3 previousVertex)
        {
            bool success = true;
            
            // Encode offset from the previous vertex
            Vector3 offset = pathNode.Vertex - previousVertex;
            success &= Serializer.TransferVectorFixed(archive, ref offset, 3);

            // Pack vertex side + radius into a single value
            int vertexSideRadius = (int)MathF.Round(pathNode.Radius);
            if (pathNode.VertexSide == NaviSide.Left) vertexSideRadius = -vertexSideRadius;
            success &= Serializer.Transfer(archive, ref vertexSideRadius);

            return success;
        }

        public static bool SerializeFrom(Archive archive, NaviPathNode pathNode, Vector3 previousVertex)
        {
            bool success = true;

            // Decode offset and combine it with the previous vertex
            Vector3 offset = Vector3.Zero;
            success &= Serializer.TransferVectorFixed(archive, ref offset, 3);
            pathNode.Vertex = offset + previousVertex;

            // Vertex side and radius are encoded together in the same value
            int vertexSideRadius = 0;
            success &= Serializer.Transfer(archive, ref vertexSideRadius);
            if (vertexSideRadius < 0)
            {
                pathNode.VertexSide = NaviSide.Left;
                pathNode.Radius = -vertexSideRadius;
            }
            else if (vertexSideRadius > 0)
            {
                pathNode.VertexSide = NaviSide.Right;
                pathNode.Radius = vertexSideRadius;
            }
            else /* if (vertexSideRadius == 0) */
            {
                pathNode.VertexSide = NaviSide.Point;
                pathNode.Radius = 0f;
            }

            return success;
        }

        public static bool SerializeTo(Archive archive, LocomotionState state, LocomotionMessageFlags flags)
        {
            bool success = true;

            if (flags.HasFlag(LocomotionMessageFlags.HasLocomotionFlags))
            {
                ulong locomotionFlags = (ulong)state.LocomotionFlags;
                success &= Serializer.Transfer(archive, ref locomotionFlags);
            }

            if (flags.HasFlag(LocomotionMessageFlags.HasMethod))
            {
                uint method = (uint)state.Method;
                success &= Serializer.Transfer(archive, ref method);
            }

            if (flags.HasFlag(LocomotionMessageFlags.HasMoveSpeed))
            {
                float moveSpeed = state.BaseMoveSpeed;
                success &= Serializer.TransferFloatFixed(archive, ref moveSpeed, 0);
            }

            if (flags.HasFlag(LocomotionMessageFlags.HasHeight))
            {
                uint height = (uint)state.Height;
                success &= Serializer.Transfer(archive, ref height);
            }

            if (flags.HasFlag(LocomotionMessageFlags.HasFollowEntityId))
            {
                ulong followEntityId = state.FollowEntityId;
                success &= Serializer.Transfer(archive, ref followEntityId);
            }

            if (flags.HasFlag(LocomotionMessageFlags.HasFollowEntityRange))
            {
                float rangeStart = state.FollowEntityRangeStart;
                float rangeEnd = state.FollowEntityRangeEnd;
                success &= Serializer.TransferFloatFixed(archive, ref rangeStart, 0);
                success &= Serializer.TransferFloatFixed(archive, ref rangeEnd, 0);
            }

            if (flags.HasFlag(LocomotionMessageFlags.UpdatePathNodes))
            {
                if (state.PathGoalNodeIndex < 0) Logger.Warn("SerializeTo(): state.PathGoalNodeIndex < 0");

                uint pathGoalNodeIndex = (uint)state.PathGoalNodeIndex;
                success &= Serializer.Transfer(archive, ref pathGoalNodeIndex);

                uint pathNodeCount = (uint)state.PathNodes.Count;
                success &= Serializer.Transfer(archive, ref pathNodeCount);

                if (pathNodeCount > 0)
                {
                    Vector3 previousVertex = Vector3.Zero;
                    foreach (NaviPathNode pathNode in state.PathNodes)
                    {
                        success &= SerializeTo(archive, pathNode, previousVertex);
                        previousVertex = pathNode.Vertex;
                    }
                }
            }

            return success;
        }

        public static bool SerializeFrom(Archive archive, LocomotionState state, LocomotionMessageFlags flags)
        {
            bool success = true;

            // NOTE: when the RelativeToPreviousState flag is not set and the value is not serialized,
            // it means the value is zero. When the flag IS set and the value is not serialized,
            // it means that the value has not changed relative to some previous locomotion state.

            if (flags.HasFlag(LocomotionMessageFlags.HasLocomotionFlags))
            {
                ulong locomotionFlags = 0;
                success &= Serializer.Transfer(archive, ref locomotionFlags);
                state.LocomotionFlags = (LocomotionFlags)locomotionFlags;       // NOTE: Is this correct? BitSet<12ul>::operator=
            }
            else if (flags.HasFlag(LocomotionMessageFlags.RelativeToPreviousState) == false)
                state.LocomotionFlags = LocomotionFlags.None;

            if (flags.HasFlag(LocomotionMessageFlags.HasMethod))
            {
                uint method = 0;
                success &= Serializer.Transfer(archive, ref method);
                state.Method = (LocomotorMethod)method;
            }
            else if (flags.HasFlag(LocomotionMessageFlags.RelativeToPreviousState) == false)
                state.Method = LocomotorMethod.Ground;

            if (flags.HasFlag(LocomotionMessageFlags.HasMoveSpeed))
            {
                float moveSpeed = 0f;
                success &= Serializer.TransferFloatFixed(archive, ref moveSpeed, 0);
                state.BaseMoveSpeed = moveSpeed;
            }
            else if (flags.HasFlag(LocomotionMessageFlags.RelativeToPreviousState) == false)
                state.BaseMoveSpeed = 0f;

            if (flags.HasFlag(LocomotionMessageFlags.HasHeight))
            {
                uint height = 0;
                success &= Serializer.Transfer(archive, ref height);
                state.Height = (int)height;
            }
            else if (flags.HasFlag(LocomotionMessageFlags.RelativeToPreviousState) == false)
                state.Height = 0;

            if (flags.HasFlag(LocomotionMessageFlags.HasFollowEntityId))
            {
                ulong followEntityId = 0;
                success &= Serializer.Transfer(archive, ref followEntityId);
                state.FollowEntityId = followEntityId;
            }
            else if (flags.HasFlag(LocomotionMessageFlags.RelativeToPreviousState) == false)
                state.FollowEntityId = 0;

            if (flags.HasFlag(LocomotionMessageFlags.HasFollowEntityRange))
            {
                float rangeStart = 0f;
                float rangeEnd = 0f;
                success &= Serializer.TransferFloatFixed(archive, ref rangeStart, 0);
                success &= Serializer.TransferFloatFixed(archive, ref rangeEnd, 0);
                state.FollowEntityRangeStart = rangeStart;
                state.FollowEntityRangeEnd = rangeEnd;
            }
            else if (flags.HasFlag(LocomotionMessageFlags.RelativeToPreviousState) == false)
            {
                state.FollowEntityRangeStart = 0f;
                state.FollowEntityRangeEnd = 0f;
            }

            if (flags.HasFlag(LocomotionMessageFlags.UpdatePathNodes))
            {
                uint pathGoalNodeIndex = 0;
                success &= Serializer.Transfer(archive, ref pathGoalNodeIndex);
                state.PathGoalNodeIndex = (int)pathGoalNodeIndex;

                state.PathNodes.Clear();
                uint pathNodeCount = 0;
                success &= Serializer.Transfer(archive, ref pathNodeCount);

                if (pathNodeCount > 0)
                {
                    Vector3 previousVertex = Vector3.Zero;
                    for (uint i = 0; i < pathNodeCount; i++)
                    {
                        NaviPathNode pathNode = new();
                        success &= SerializeFrom(archive, pathNode, previousVertex);
                        previousVertex = pathNode.Vertex;
                        state.PathNodes.Add(pathNode);
                    }
                }
            }
            else if (flags.HasFlag(LocomotionMessageFlags.RelativeToPreviousState) == false)
            {
                state.PathGoalNodeIndex = 0;
                state.PathNodes.Clear();
            }

            return success;
        }

        public void Decode(CodedInputStream stream, LocomotionMessageFlags flags)
        {
            PathNodes.Clear();

            if (flags.HasFlag(LocomotionMessageFlags.HasLocomotionFlags))
                LocomotionFlags = (LocomotionFlags)stream.ReadRawVarint64();

            if (flags.HasFlag(LocomotionMessageFlags.HasMethod))
                Method = (LocomotorMethod)stream.ReadRawVarint32();

            if (flags.HasFlag(LocomotionMessageFlags.HasMoveSpeed))
                BaseMoveSpeed = stream.ReadRawZigZagFloat(0);

            if (flags.HasFlag(LocomotionMessageFlags.HasHeight))
                Height = (int)stream.ReadRawVarint32();

            if (flags.HasFlag(LocomotionMessageFlags.HasFollowEntityId))
                FollowEntityId = stream.ReadRawVarint64();

            if (flags.HasFlag(LocomotionMessageFlags.HasFollowEntityRange))
            {
                FollowEntityRangeStart = stream.ReadRawZigZagFloat(0);
                FollowEntityRangeEnd = stream.ReadRawZigZagFloat(0);
            }

            if (flags.HasFlag(LocomotionMessageFlags.UpdatePathNodes))
            {
                PathGoalNodeIndex = (int)stream.ReadRawVarint32();
                int count = (int)stream.ReadRawVarint64();

                Vector3 previousVertex = Vector3.Zero;
                for (int i = 0; i < count; i++)
                {
                    NaviPathNode pathNode = new();
                    pathNode.Decode(stream, previousVertex);
                    previousVertex = pathNode.Vertex;
                    PathNodes.Add(pathNode);
                }
            }
        }

        public void Encode(CodedOutputStream stream, LocomotionMessageFlags flags)
        {
            if (flags.HasFlag(LocomotionMessageFlags.HasLocomotionFlags))
                stream.WriteRawVarint64((ulong)LocomotionFlags);

            if (flags.HasFlag(LocomotionMessageFlags.HasMethod))
                stream.WriteRawVarint32((uint)Method);

            if (flags.HasFlag(LocomotionMessageFlags.HasMoveSpeed))
                stream.WriteRawZigZagFloat(BaseMoveSpeed, 0);

            if (flags.HasFlag(LocomotionMessageFlags.HasHeight))
                stream.WriteRawVarint32((uint)Height);

            if (flags.HasFlag(LocomotionMessageFlags.HasFollowEntityId))
                stream.WriteRawVarint64(FollowEntityId);

            if (flags.HasFlag(LocomotionMessageFlags.HasFollowEntityRange))
            {
                stream.WriteRawZigZagFloat(FollowEntityRangeStart, 0);
                stream.WriteRawZigZagFloat(FollowEntityRangeEnd, 0);
            }

            if (flags.HasFlag(LocomotionMessageFlags.UpdatePathNodes))
            {
                stream.WriteRawVarint32((uint)PathGoalNodeIndex);
                stream.WriteRawVarint64((ulong)PathNodes.Count);

                Vector3 previousVertex = Vector3.Zero;
                foreach (NaviPathNode naviVector in PathNodes)
                {
                    naviVector.Encode(stream, previousVertex);
                    previousVertex = naviVector.Vertex;
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            sb.AppendLine($"{nameof(LocomotionFlags)}: {LocomotionFlags}");
            sb.AppendLine($"{nameof(Method)}: {Method}");
            sb.AppendLine($"{nameof(BaseMoveSpeed)}: {BaseMoveSpeed}");
            sb.AppendLine($"{nameof(Height)}: {Height}");
            sb.AppendLine($"{nameof(FollowEntityId)}: {FollowEntityId}");
            sb.AppendLine($"{nameof(FollowEntityRangeStart)}: {FollowEntityRangeStart}");
            sb.AppendLine($"{nameof(FollowEntityRangeEnd)}: {FollowEntityRangeEnd}");
            sb.AppendLine($"{nameof(PathGoalNodeIndex)}: {PathGoalNodeIndex}");
            for (int i = 0; i < PathNodes.Count; i++)
                sb.AppendLine($"{nameof(PathNodes)}[{i}]: {PathNodes[i]}");
            return sb.ToString();
        }

        public void StateFrom(LocomotionState locomotionState)
        {
            // TODO Replace with SerializeFrom()
            Set(locomotionState);
        }

        public void Set(LocomotionState other)
        {
            LocomotionFlags = other.LocomotionFlags;
            Method = other.Method;
            BaseMoveSpeed = other.BaseMoveSpeed;
            Height = other.Height;
            FollowEntityId = other.FollowEntityId;
            FollowEntityRangeStart = other.FollowEntityRangeStart;
            FollowEntityRangeEnd = other.FollowEntityRangeEnd;
            PathGoalNodeIndex = other.PathGoalNodeIndex;

            // NOTE: Is it okay to add path nodes here by reference? Do we need a copy?
            // Review this if/when we change NaviPathNode to struct.
            //PathNodes = new(other.PathNodes);
            PathNodes.Clear();
            PathNodes.AddRange(other.PathNodes);
        }
    }
}
