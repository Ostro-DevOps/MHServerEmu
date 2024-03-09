﻿using System.Text;
using Google.ProtocolBuffers;
using MHServerEmu.Common.Encoders;
using MHServerEmu.Common.Extensions;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Calligraphy.Attributes;
using MHServerEmu.Games.GameData.Prototypes;

namespace MHServerEmu.Games.Missions
{
    [AssetEnum((int)Invalid)]
    public enum MissionState
    {
        Invalid = 0,
        Inactive = 1,
        Available = 2,
        Active = 3,
        Completed = 4,
        Failed = 5,
    }

    public class Mission
    {
        public MissionState State { get; set; }
        public ulong TimeExpireCurrentState { get; set; }
        public PrototypeId PrototypeId { get; set; }
        public int Random { get; set; }
        public Objective[] Objectives { get; set; }
        public ulong[] Participants { get; set; }
        public bool Suspended { get; set; }

        public MissionManager MissionManager { get; }
        public Game Game { get; }

        public Mission(CodedInputStream stream, BoolDecoder boolDecoder)
        {            
            State = (MissionState)stream.ReadRawInt32();
            TimeExpireCurrentState = stream.ReadRawVarint64();
            PrototypeId = stream.ReadPrototypeEnum<Prototype>();
            Random = stream.ReadRawInt32();

            Objectives = new Objective[stream.ReadRawVarint64()];
            for (int i = 0; i < Objectives.Length; i++)
                Objectives[i] = new(stream);

            Participants = new ulong[stream.ReadRawVarint64()];
            for (int i = 0; i < Participants.Length; i++)
                Participants[i] = stream.ReadRawVarint64();

            Suspended = boolDecoder.ReadBool(stream);
        }

        public Mission(MissionState state, ulong timeExpireCurrentState, PrototypeId prototypeId,
            int random, Objective[] objectives, ulong[] participants, bool suspended)
        {
            State = state;
            TimeExpireCurrentState = timeExpireCurrentState;
            PrototypeId = prototypeId;
            Random = random;
            Objectives = objectives;
            Participants = participants;
            Suspended = suspended;
        }

        public Mission(PrototypeId prototypeId, int random)
        {
            State = MissionState.Active;
            TimeExpireCurrentState = 0x0;
            PrototypeId = prototypeId;
            Random = random;
            Objectives = new Objective[] { new(0x0, MissionObjectiveState.Active, 0x0, Array.Empty<InteractionTag>(), 0x0, 0x0, 0x0, 0x0) };
            Participants = Array.Empty<ulong>();
            Suspended = false;
        }

        public Mission(MissionManager missionManager, PrototypeId missionRef)
        {
            MissionManager = missionManager;
            Game = MissionManager.Game;
            PrototypeId = missionRef;

            // TODO other fields
        }

        public void Encode(CodedOutputStream stream, BoolEncoder boolEncoder)
        {            
            stream.WriteRawInt32((int)State);
            stream.WriteRawVarint64(TimeExpireCurrentState);
            stream.WritePrototypeEnum<Prototype>(PrototypeId);
            stream.WriteRawInt32(Random);

            stream.WriteRawVarint64((ulong)Objectives.Length);
            foreach (Objective objective in Objectives) objective.Encode(stream);

            stream.WriteRawVarint64((ulong)Participants.Length);
            foreach (ulong Participant in Participants) stream.WriteRawVarint64(Participant);

            boolEncoder.WriteBuffer(stream);   // Suspended
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            sb.AppendLine($"State: {State}");
            sb.AppendLine($"TimeExpireCurrentState: 0x{TimeExpireCurrentState:X}");
            sb.AppendLine($"PrototypeId: {GameDatabase.GetPrototypeName(PrototypeId)}");
            sb.AppendLine($"Random: 0x{Random:X}");
            for (int i = 0; i < Objectives.Length; i++) sb.AppendLine($"Objective{i}: {Objectives[i]}");
            for (int i = 0; i < Participants.Length; i++) sb.AppendLine($"Participant{i}: {Participants[i]}");
            sb.AppendLine($"Suspended: {Suspended}");
            return sb.ToString();
        }

    }
}
