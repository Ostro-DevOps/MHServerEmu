﻿using MHServerEmu.Games.Regions;
using MHServerEmu.Games.Generators.Prototypes;
using MHServerEmu.Games.Common;
using MHServerEmu.Common;
using MHServerEmu.Common.Logging;

namespace MHServerEmu.Games.Generators.Regions
{
    public class StaticRegionGenerator : RegionGenerator
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        public override void GenerateRegion(int randomSeed, Region region)
        {
            StartArea = null;
            GRandom random = new(randomSeed);
            StaticRegionGeneratorPrototype regionGeneratorProto = (StaticRegionGeneratorPrototype)GeneratorPrototype;
            StaticAreaPrototype[] staticAreas = regionGeneratorProto.StaticAreas;
            ulong areaRef = region.RegionPrototype.GetDefaultArea(region);

            foreach (StaticAreaPrototype staticAreaProto in staticAreas)
            {
                Vector3 areaOrigin = new(staticAreaProto.X, staticAreaProto.Y, staticAreaProto.Z);
                Area area = region.CreateArea(staticAreaProto.Area, areaOrigin);
                if (area != null)
                {
                    AddAreaToMap(staticAreaProto.Area, area);
                    if (staticAreaProto.Area == areaRef)
                        StartArea = area;
                }
            }
            if (staticAreas != null)
                DoConnection(random, region, staticAreas, regionGeneratorProto);

        }

        private void DoConnection(GRandom random, Region region, StaticAreaPrototype[] staticAreas, StaticRegionGeneratorPrototype regionGeneratorProto)
        {
            RegionProgressionGraph graph = region.ProgressionGraph;

            if (StartArea != null && graph.GetRoot() == null )
            {
                graph.SetRoot(StartArea);
            }

            if (staticAreas.Length > 1)
            {
                if (regionGeneratorProto.Connections != null)
                {
                    List<AreaConnectionPrototype> workingConnectionList = new (regionGeneratorProto.Connections);

                    if (workingConnectionList.Count == 0)
                    {
                        Logger.Error("Calligraphy Error: More than one area in region but there are no connections specified.");
                        return;
                    }

                    List<ulong> nextConnections = new();
                    List<ulong> prevConnections = new()
                    {
                        StartArea.GetPrototypeDataRef()
                    };
                    ConnectNextAreas(random, workingConnectionList, prevConnections, nextConnections, graph);
                }
            }
        }

        public static bool ConnectionListContainsArea(List<ulong> connections, ulong area)
        {
            return connections.Contains(area);
        }

        public static bool GenerateConnectionFromQueriedPoints(GRandom random, out Vector3 connection, Area areaA, Area areaB)
        {
            List<Vector3> sharedConnections = new ();
            connection = null;

            if (!GetSharedConnections(sharedConnections, areaA, areaB)) return false;

            Picker<Vector3> picker = new (random);
            foreach (var point in sharedConnections)
                picker.Add(point);

            if (picker.Empty()) return false;

            if (!picker.Pick(out connection)) return false;

            return true;
        }

        public void ConnectNextAreas(GRandom random, List<AreaConnectionPrototype> workingConnectionList, List<ulong> prevConnections, List<ulong> nextConnections, RegionProgressionGraph graph)
        {
            int failout = 100;
            foreach (var areaConnectProto in workingConnectionList.TakeWhile(_ => failout-- > 0))
            {
                if (areaConnectProto == null) continue;

                Area areaA = GetAreaFromPrototypeRef(areaConnectProto.AreaA);
                Area areaB = GetAreaFromPrototypeRef(areaConnectProto.AreaB);

                if ((areaA == null && areaB == null)) continue;

                if (ConnectionListContainsArea(prevConnections, areaConnectProto.AreaA))
                {
                    if (areaConnectProto.ConnectAllShared)
                    {
                        List<Vector3> sharedConnections = new ();
                        GetSharedConnections(sharedConnections, areaA, areaB);
                        SetSharedConnections(sharedConnections, areaA, areaB);
                    }
                    else
                    {
                        if (GenerateConnectionFromQueriedPoints(random, out Vector3 connection, areaA, areaB) == false) continue;
                        Area.CreateConnection(areaA, areaB, connection, ConnectPosition.One);                        
                    }

                    graph.AddLink(areaA, areaB);
                    nextConnections.Add(areaConnectProto.AreaB);
                    continue;
                }

                if (ConnectionListContainsArea(prevConnections, areaConnectProto.AreaB))
                {
                    if (areaConnectProto.ConnectAllShared)
                    {
                        List<Vector3> sharedConnections = new ();
                        GetSharedConnections(sharedConnections, areaA, areaB);
                        SetSharedConnections(sharedConnections, areaB, areaA);
                    }
                    else
                    {
                        if (GenerateConnectionFromQueriedPoints(random, out Vector3 connection, areaA, areaB) == false) continue;
                        Area.CreateConnection(areaB, areaA, connection, ConnectPosition.One);
                    }

                    graph.AddLink(areaB, areaA);
                    nextConnections.Add(areaConnectProto.AreaA);
                    continue;
                }
            }

            if (nextConnections.Count > 0)
            {
                prevConnections.Clear();
                prevConnections.AddRange(nextConnections);
                nextConnections.Clear();
                ConnectNextAreas(random, workingConnectionList, prevConnections, nextConnections, graph);
            }

            if (failout == 0)
                Logger.Error("We overstayed our welcome trying to connect areas.");

            return;
        }

    }
}
