using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepPressure
{
    /// <summary>Fixed-step finite six-species pipe inventory. Room exchange is registered explicitly by life-support facilities.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed partial class GasNetworkSimulator : MonoBehaviour
    {
        public bool paused;
        [Range(.1f, 8)] public float simulationSpeed = 1;
        [Range(.01f, .5f)] public float fixedStepSeconds = .1f;
        public GasNode[] nodes = Array.Empty<GasNode>();
        public GasLink[] links = Array.Empty<GasLink>();
        public double ElapsedSeconds { get; private set; }
        public int StepCount { get; private set; }
        public GasMixture InitialTotal { get; private set; }
        public string LastValidation { get; private set; } = "Not initialized";
        double accumulatedTime;

        sealed class Transfer
        {
            public GasLink link;
            public int source, destination;
            public GasMixture packet;
            public bool reversed;
        }
        void Start() => ResetSimulation();
        void Update()
        {
            if (paused) return;
            accumulatedTime += Math.Min(.25, Time.unscaledDeltaTime) * simulationSpeed;
            int iterations = 0;
            while (accumulatedTime >= fixedStepSeconds && iterations++ < 32)
            {
                Step(fixedStepSeconds); accumulatedTime -= fixedStepSeconds;
            }
        }
        [ContextMenu("Reset Gas Network")]
        public void ResetSimulation()
        {
            nodes = GetComponentsInChildren<GasNode>(true);
            links = GetComponentsInChildren<GasLink>(true);
            foreach (GasNode node in nodes) node.ResetInventory();
            foreach (GasLink link in links) { link.lastFlowMolPerSecond = 0; link.status = "Idle"; }
            accumulatedTime = 0; ElapsedSeconds = 0; StepCount = 0; InitialTotal = TotalInventory(); LastValidation = "Conserved";
        }
        public GasMixture TotalInventory()
        {
            GasMixture total = default;
            foreach (var node in nodes) if (node != null) total += node.gas;
            return total;
        }
        /// <summary>Commission newly constructed nodes without refilling existing tanks or rewinding time.</summary>
        public void RefreshTopologyPreservingGas()
        {
            var previous = new HashSet<GasNode>(nodes ?? Array.Empty<GasNode>());
            nodes = GetComponentsInChildren<GasNode>(true);
            links = GetComponentsInChildren<GasLink>(true);
            foreach (GasNode node in nodes) if (!previous.Contains(node)) node.ResetInventory();
            InitialTotal = TotalInventory();
            LastValidation = "All " + GasMixture.SpeciesCount + " species conserved";
        }
        public void RegisterExternalExchange(GasMixture delta) { InitialTotal += delta; }
        public bool VerifyConservation(out string message)
        {
            var total = TotalInventory();
            foreach (GasNode node in nodes)
                if (node != null && !node.gas.IsFiniteAndNonnegative) { message = node.displayName + ": invalid inventory"; return false; }
            for (int species = 0; species < GasMixture.SpeciesCount; species++)
                if (Math.Abs(total[species] - InitialTotal[species]) > Math.Max(1e-7, InitialTotal[species] * 1e-9))
                { message = "Species " + species + " inventory drift"; return false; }
            message = "All " + GasMixture.SpeciesCount + " species conserved"; return true;
        }
        public void Step(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            int count = nodes.Length;
            var nodeIndex = new Dictionary<GasNode, int>();
            var snapshot = new GasMixture[count];
            var capacities = new double[count];
            var blocked = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (nodes[i] == null) continue;
                nodeIndex[nodes[i]] = i; snapshot[i] = nodes[i].gas;
                capacities[i] = Math.Max(0, nodes[i].CapacityMol - snapshot[i].Total);
                nodes[i].lastInflowMolPerSecond = nodes[i].lastOutflowMolPerSecond = 0;
                nodes[i].status = !nodes[i].isActiveAndEnabled ? "Disabled" : snapshot[i].Total < 1e-7 ? "Empty / waiting for feed" : "Idle / balanced";
                if (nodes[i].kind != GasNodeKind.Separator) continue;
                bool product = HasAvailableOutlet(nodes[i], GasOutputPort.OxygenProduct);
                bool tail = HasAvailableOutlet(nodes[i], GasOutputPort.TailGas);
                if (!product || !tail)
                {
                    blocked[i] = true;
                    nodes[i].status = !product ? "Stopped: O2 outlet shut / full" : "Stopped: tail outlet shut / full";
                }
            }
            var transfers = new List<Transfer>();
            foreach (GasLink link in links)
            {
                if (link == null) continue;
                link.lastFlowMolPerSecond = 0; link.status = "Idle / balanced";
                if (!link.isActiveAndEnabled || !link.isOpen || link.valve <= 0) { link.status = "Valve closed"; continue; }
                if (link.from == null || link.to == null || link.from == link.to || !nodeIndex.TryGetValue(link.from, out int source) || !nodeIndex.TryGetValue(link.to, out int destination))
                { link.status = "Missing / invalid endpoint"; continue; }
                if (!nodes[source].isActiveAndEnabled || !nodes[destination].isActiveAndEnabled) { link.status = "Node disabled"; continue; }
                if (blocked[source] || blocked[destination]) { link.status = "Separator interlock"; continue; }
                if (link.fromPort != GasOutputPort.Mixed && nodes[source].kind != GasNodeKind.Separator)
                { link.status = "Filtered port requires separator"; continue; }
                if (nodes[source].kind == GasNodeKind.Separator && link.fromPort == GasOutputPort.Mixed)
                { link.status = "Choose O2 or tail outlet"; continue; }
                double pressureDifference = nodes[source].PressureKPa - nodes[destination].PressureKPa;
                bool reversed = pressureDifference < 0 && link.CanReverse;
                if (reversed) { int oldSource = source; source = destination; destination = oldSource; pressureDifference = -pressureDifference; }
                if (pressureDifference <= 1e-8) { link.status = "No forward pressure gradient"; continue; }
                if (capacities[destination] <= 1e-8) { link.status = "Outlet full / pressure limit"; continue; }
                GasMixture available = snapshot[source].Filter(link.fromPort);
                if (available.Total <= 1e-9) { link.status = "No eligible gas"; continue; }
                if (!DeepGasFacility.Accepts(nodes[destination],available)) { link.status = "气体成分不符合储罐用途"; continue; }
                // Partial-pressure fraction sets each separator branch's share of the total stream.
                double fraction = available.Total / Math.Max(1e-9, snapshot[source].Total);
                double amount = Math.Min(Math.Max(0, link.maxFlowMolPerSecond), pressureDifference * Math.Max(0, link.conductanceMolPerSecondPerKPa)) * Mathf.Clamp01(link.valve) * seconds * fraction;
                double kSource = GasMixture.GasConstant * (nodes[source].temperatureC + 273.15) / (nodes[source].volumeM3 * 1000);
                double kDestination = GasMixture.GasConstant * (nodes[destination].temperatureC + 273.15) / (nodes[destination].volumeM3 * 1000);
                amount = Math.Min(amount, pressureDifference / (kSource + kDestination));
                amount = Math.Min(amount, available.Total);
                if (amount <= 1e-12) continue;
                transfers.Add(new Transfer { link = link, source = source, destination = destination, reversed = reversed, packet = available.Scaled(amount / available.Total) });
            }
            // Build all requests before allocating any inventory; hierarchy/link order cannot win a race.
            var requestedOut = new GasMixture[count];
            var requestedIn = new double[count];
            foreach (Transfer transfer in transfers) { requestedOut[transfer.source] += transfer.packet; requestedIn[transfer.destination] += transfer.packet.Total; }
            var outgoingScale = new double[count]; var incomingScale = new double[count];
            for (int i = 0; i < count; i++)
            {
                outgoingScale[i] = incomingScale[i] = 1;
                if (nodes[i] == null) continue;
                double throughput = Math.Max(0, nodes[i].throughputMolPerSecond) * seconds;
                if (requestedOut[i].Total > 0) outgoingScale[i] = Math.Min(1, throughput / requestedOut[i].Total);
                for (int species = 0; species < GasMixture.SpeciesCount; species++)
                    if (requestedOut[i][species] > 0) outgoingScale[i] = Math.Min(outgoingScale[i], Math.Max(0, snapshot[i][species]) / requestedOut[i][species]);
                if (requestedIn[i] > 0) incomingScale[i] = Math.Min(1, Math.Min(capacities[i], throughput) / requestedIn[i]);
            }
            var changes = new GasMixture[count];
            foreach (Transfer transfer in transfers)
            {
                GasMixture actual = transfer.packet.Scaled(Math.Min(outgoingScale[transfer.source], incomingScale[transfer.destination]));
                changes[transfer.source] -= actual; changes[transfer.destination] += actual;
                double flow = actual.Total / seconds;
                transfer.link.lastFlowMolPerSecond = transfer.reversed ? -flow : flow;
                transfer.link.status = flow > 1e-8 ? (transfer.reversed ? "Reverse flow" : "Flowing") : "Capacity / throughput limited";
                nodes[transfer.source].lastOutflowMolPerSecond += flow; nodes[transfer.destination].lastInflowMolPerSecond += flow;
            }
            for (int i = 0; i < count; i++)
            {
                if (nodes[i] == null) continue;
                nodes[i].gas = snapshot[i] + changes[i];
                if (blocked[i] || !nodes[i].isActiveAndEnabled) continue;
                if (nodes[i].lastInflowMolPerSecond + nodes[i].lastOutflowMolPerSecond > 1e-8) nodes[i].status = nodes[i].kind == GasNodeKind.Separator ? "Separating O2 / tail" : "Flowing";
                else if (capacities[i] <= 1e-8) nodes[i].status = nodes[i].kind == GasNodeKind.Regulator ? "At regulator setpoint" : "Full: pressure limit";
            }
            ElapsedSeconds += seconds; StepCount++;
            if (!VerifyConservation(out string message)) { LastValidation = message; paused = true; Debug.LogError("Deep Pressure gas simulation stopped: " + message, this); }
            else LastValidation = message;
        }
        bool HasAvailableOutlet(GasNode separator, GasOutputPort port)
        {
            foreach (GasLink link in links)
            {
                if (link == null || !link.isActiveAndEnabled || !link.isOpen || link.valve <= 0 || link.from != separator || link.fromPort != port || link.to == null || link.to == separator || !link.to.isActiveAndEnabled || Array.IndexOf(nodes,link.to) < 0) continue;
                if (link.maxFlowMolPerSecond <= 0 || link.conductanceMolPerSecondPerKPa <= 0 || link.to.throughputMolPerSecond <= 0) continue;
                if (link.to.CapacityMol - link.to.gas.Total > 1e-8 && link.to.PressureKPa < separator.maxPressureKPa - 1e-6) return true;
            }
            return false;
        }
    }
}
