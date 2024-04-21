﻿using System.Text;
using Google.ProtocolBuffers;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Games.Common;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;

namespace MHServerEmu.Games.Entities.PowerCollections
{
    [Flags]
    public enum PowerCollectionRecordFlags
    {
        None = 0,
        PowerRefCountIsOne = 1 << 0,
        PowerRankIsZero = 1 << 1,
        CharacterLevelIsOne = 1 << 2,
        CharacterLevelIsFromPreviousRecord = 1 << 3,
        CombatLevelIsOne = 1 << 4,
        CombatLevelIsFromPreviousRecord = 1 << 5,
        CombatLevelIsSameAsCharacterLevel = 1 << 6,
        ItemLevelIsOne = 1 << 7,
        ItemVariationIsOne = 1 << 8
    }

    public class PowerCollectionRecord
    {
        public PrototypeId PowerPrototypeId { get; set; }
        public PowerCollectionRecordFlags Flags { get; set; }
        public PowerIndexProperties IndexProps { get; set; }
        public uint PowerRefCount { get; set; }

        public PowerCollectionRecord(CodedInputStream stream, PowerCollectionRecord previousRecord)
        {
            PowerPrototypeId = stream.ReadPrototypeRef<PowerPrototype>();
            Flags = (PowerCollectionRecordFlags)stream.ReadRawVarint32();

            IndexProps = new();

            IndexProps.PowerRank = Flags.HasFlag(PowerCollectionRecordFlags.PowerRankIsZero) ? 0 : (int)stream.ReadRawVarint32();

            // CharacterLevel
            if (Flags.HasFlag(PowerCollectionRecordFlags.CharacterLevelIsOne))
                IndexProps.CharacterLevel = 1;
            else if (Flags.HasFlag(PowerCollectionRecordFlags.CharacterLevelIsFromPreviousRecord))
                IndexProps.CharacterLevel = previousRecord.IndexProps.CharacterLevel;
            else
                IndexProps.CharacterLevel = (int)stream.ReadRawVarint32();

            // CombatLevel
            if (Flags.HasFlag(PowerCollectionRecordFlags.CombatLevelIsOne))
                IndexProps.CombatLevel = 1;
            else if (Flags.HasFlag(PowerCollectionRecordFlags.CombatLevelIsFromPreviousRecord))
                IndexProps.CombatLevel = previousRecord.IndexProps.CombatLevel;
            else if (Flags.HasFlag(PowerCollectionRecordFlags.CombatLevelIsSameAsCharacterLevel))
                IndexProps.CombatLevel = IndexProps.CharacterLevel;
            else
                IndexProps.CombatLevel = (int)stream.ReadRawVarint32();

            IndexProps.ItemLevel = Flags.HasFlag(PowerCollectionRecordFlags.ItemLevelIsOne) ? 1 : (int)stream.ReadRawVarint32();
            IndexProps.ItemVariation = Flags.HasFlag(PowerCollectionRecordFlags.ItemVariationIsOne) ? 1.0f : stream.ReadRawFloat();
            PowerRefCount = Flags.HasFlag(PowerCollectionRecordFlags.PowerRefCountIsOne) ? 1 : stream.ReadRawVarint32();
        }

        public PowerCollectionRecord() { IndexProps = new(); }

        public void Encode(CodedOutputStream stream)
        {
            stream.WritePrototypeRef<PowerPrototype>(PowerPrototypeId);
            stream.WriteRawVarint32((uint)Flags);

            if (Flags.HasFlag(PowerCollectionRecordFlags.PowerRankIsZero) == false)
                stream.WriteRawVarint32((uint)IndexProps.PowerRank);

            if (Flags.HasFlag(PowerCollectionRecordFlags.CharacterLevelIsOne) == false && Flags.HasFlag(PowerCollectionRecordFlags.CharacterLevelIsFromPreviousRecord) == false)
                stream.WriteRawVarint32((uint)IndexProps.CharacterLevel);

            if (Flags.HasFlag(PowerCollectionRecordFlags.CombatLevelIsOne) == false && Flags.HasFlag(PowerCollectionRecordFlags.CombatLevelIsFromPreviousRecord) == false
                && Flags.HasFlag(PowerCollectionRecordFlags.CombatLevelIsSameAsCharacterLevel) == false)
                stream.WriteRawVarint32((uint)IndexProps.CombatLevel);

            if (Flags.HasFlag(PowerCollectionRecordFlags.ItemLevelIsOne) == false)
                stream.WriteRawVarint32((uint)IndexProps.ItemLevel);

            if (Flags.HasFlag(PowerCollectionRecordFlags.ItemVariationIsOne) == false)
                stream.WriteRawFloat(IndexProps.ItemVariation);

            if (Flags.HasFlag(PowerCollectionRecordFlags.PowerRefCountIsOne) == false)
                stream.WriteRawVarint32(PowerRefCount);
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            sb.AppendLine($"PowerPrototypeId: {GameDatabase.GetPrototypeName(PowerPrototypeId)}");
            sb.AppendLine($"Flags: {Flags}");
            sb.AppendLine($"IndexProps: {IndexProps}");
            sb.AppendLine($"PowerRefCount: {PowerRefCount}");

            return sb.ToString();
        }
    }
}
