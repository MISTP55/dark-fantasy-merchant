using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The traffic of one city's harbour: ships cross the city grid between its edge and a berth.
    /// A berth is reserved from the moment its ship enters the grid until that ship has left it by
    /// the edge. Ships that find no free berth queue, first come first served. A ship is only a key
    /// here: it keeps its own map position and route out of it.
    /// </summary>
    public sealed class Harbour
    {
        private sealed class Entry
        {
            public Ship Ship;
            public HarbourState State;
            public int Berth = -1;

            // Distance from the berth along its exit path, in cells: 0 at the berth, the path's length at the edge.
            public float Distance;
        }

        private readonly float cellsPerSecond;

        // Per berth: the exit path from the berth (first) to the edge (last), and its length.
        private readonly List<Vector2Int>[] paths;
        private readonly float[] lengths;
        private readonly bool[] reserved;

        // Every ship here, in the order they came.
        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<Ship, Entry> byShip = new Dictionary<Ship, Entry>();

        // Reused by every step.
        private readonly List<Entry> snapshot = new List<Entry>();

        private bool advancing;
        private int freeBerths;

        public Harbour(CityGrid grid, float cellsPerSecond)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (!HarbourPaths.TryValidate(grid, out string problem))
            {
                throw new ArgumentException(problem, nameof(grid));
            }

            if (!(cellsPerSecond > 0f) || float.IsInfinity(cellsPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(cellsPerSecond), "The speed must be positive and finite.");
            }

            this.cellsPerSecond = cellsPerSecond;
            int count = grid.Berths.Count;
            paths = new List<Vector2Int>[count];
            lengths = new float[count];
            reserved = new bool[count];
            for (int i = 0; i < count; i++)
            {
                paths[i] = new List<Vector2Int>();
                if (!HarbourPaths.TryFindExit(grid, grid.Berths[i], paths[i]))
                {
                    throw new ArgumentException("A berth has no way out.", nameof(grid));
                }

                lengths[i] = HarbourPaths.LengthOf(paths[i]);
            }

            freeBerths = count;
        }

        /// <summary>Raised when a queued ship enters the grid.</summary>
        public event Action<Ship> Entered;

        /// <summary>Raised when a ship reaches its berth.</summary>
        public event Action<Ship> Berthed;

        /// <summary>Raised when a ship has left the grid by the edge.</summary>
        public event Action<Ship> Exited;

        public int Capacity => paths.Length;

        public int FreeBerths => freeBerths;

        public bool Contains(Ship ship)
        {
            return ship != null && byShip.ContainsKey(ship);
        }

        public HarbourState StateOf(Ship ship)
        {
            return ship != null && byShip.TryGetValue(ship, out Entry entry) ? entry.State : HarbourState.AtSea;
        }

        /// <summary>Fills the list with every ship here, in the order they came.</summary>
        public void GetShips(List<Ship> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                results.Add(entries[i].Ship);
            }
        }

        /// <summary>Lets a ship in: it arrives from the edge when a berth is free and nobody is queued, else it queues.</summary>
        public HarbourState Admit(Ship ship)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            if (byShip.ContainsKey(ship))
            {
                throw new InvalidOperationException("The ship is already in the harbour.");
            }

            var entry = new Entry { Ship = ship, State = HarbourState.Waiting };
            entries.Add(entry);
            byShip.Add(ship, entry);
            if (!HasWaiting(entry))
            {
                TryEnter(entry);
            }

            return entry.State;
        }

        /// <summary>
        /// Puts a ship straight at the free berth nearest the edge, docked, as if it had always lain there: no event
        /// is raised. False, and the ship is left out, when no berth is free or ships are queued for one.
        /// </summary>
        public bool Place(Ship ship)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            if (byShip.ContainsKey(ship))
            {
                throw new InvalidOperationException("The ship is already in the harbour.");
            }

            var entry = new Entry { Ship = ship, State = HarbourState.Waiting };
            if (HasWaiting(entry) || !TryEnter(entry))
            {
                return false;
            }

            entry.Distance = 0f;
            entry.State = HarbourState.Docked;
            entries.Add(entry);
            byShip.Add(ship, entry);
            return true;
        }

        /// <summary>Takes a waiting ship out of the queue.</summary>
        public bool Withdraw(Ship ship)
        {
            if (ship == null || !byShip.TryGetValue(ship, out Entry entry) || entry.State != HarbourState.Waiting)
            {
                return false;
            }

            Forget(entry);
            return true;
        }

        /// <summary>Sends an arriving or docked ship back to the edge; an arriving one turns round where it is.</summary>
        public void Leave(Ship ship)
        {
            Entry entry = Find(ship);
            if (entry.State == HarbourState.Leaving)
            {
                return;
            }

            if (entry.State != HarbourState.Arriving && entry.State != HarbourState.Docked)
            {
                throw new InvalidOperationException("Only a ship on the grid can leave.");
            }

            entry.State = HarbourState.Leaving;
        }

        /// <summary>Turns a leaving ship round to its berth, which is still reserved.</summary>
        public void Return(Ship ship)
        {
            Entry entry = Find(ship);
            if (entry.State != HarbourState.Leaving)
            {
                throw new InvalidOperationException("Only a leaving ship can return.");
            }

            entry.State = HarbourState.Arriving;
        }

        /// <summary>Forgets a ship in any state, frees its berth and lets the queue in.</summary>
        public void Remove(Ship ship)
        {
            Entry entry = Find(ship);
            Forget(entry);
            AdmitQueue();
        }

        /// <param name="gridPoint">The ship's place on the grid; cell (x, y) has its centre at the point (x, y).</param>
        /// <returns>False when the ship is not on the grid.</returns>
        public bool TryGetPosition(Ship ship, out Vector2 gridPoint)
        {
            gridPoint = default;
            if (ship == null || !byShip.TryGetValue(ship, out Entry entry) || entry.Berth < 0)
            {
                return false;
            }

            List<Vector2Int> path = paths[entry.Berth];
            float remaining = entry.Distance;
            for (int i = 1; i < path.Count; i++)
            {
                float step = HarbourPaths.StepLength(path[i - 1], path[i]);
                if (remaining <= step)
                {
                    gridPoint = Vector2.Lerp(path[i - 1], path[i], remaining / step);
                    return true;
                }

                remaining -= step;
            }

            gridPoint = path[path.Count - 1];
            return true;
        }

        /// <summary>Moves every ship on the grid, then lets the queue in. The rest of a step is not carried over.</summary>
        public void Advance(float seconds)
        {
            if (!(seconds > 0f))
            {
                return;
            }

            if (advancing)
            {
                throw new InvalidOperationException("Advance cannot be called from an event of the harbour.");
            }

            advancing = true;
            try
            {
                MoveShips(cellsPerSecond * seconds);
            }
            finally
            {
                advancing = false;
                snapshot.Clear();
            }

            AdmitQueue();
        }

        private void MoveShips(float travel)
        {
            snapshot.Clear();
            snapshot.AddRange(entries);
            for (int i = 0; i < snapshot.Count; i++)
            {
                Entry entry = snapshot[i];
                if (entry.Berth < 0 || !byShip.TryGetValue(entry.Ship, out Entry current) || current != entry)
                {
                    continue;
                }

                if (entry.State == HarbourState.Arriving)
                {
                    entry.Distance -= travel;
                    if (entry.Distance <= 0f)
                    {
                        entry.Distance = 0f;
                        entry.State = HarbourState.Docked;
                        Berthed?.Invoke(entry.Ship);
                    }
                }
                else if (entry.State == HarbourState.Leaving)
                {
                    entry.Distance += travel;
                    if (entry.Distance >= lengths[entry.Berth])
                    {
                        Forget(entry);
                        Exited?.Invoke(entry.Ship);
                    }
                }
            }
        }

        private Entry Find(Ship ship)
        {
            if (ship == null || !byShip.TryGetValue(ship, out Entry entry))
            {
                throw new InvalidOperationException("The ship is not in the harbour.");
            }

            return entry;
        }

        // Frees the berth, if any, and drops the entry.
        private void Forget(Entry entry)
        {
            if (entry.Berth >= 0)
            {
                reserved[entry.Berth] = false;
                freeBerths++;
                entry.Berth = -1;
            }

            entries.Remove(entry);
            byShip.Remove(entry.Ship);
        }

        // Reserves the free berth nearest the edge (lowest index on a tie) for a waiting ship, if there is one.
        private bool TryEnter(Entry entry)
        {
            int best = -1;
            for (int i = 0; i < paths.Length; i++)
            {
                if (!reserved[i] && (best < 0 || lengths[i] < lengths[best]))
                {
                    best = i;
                }
            }

            if (best < 0)
            {
                return false;
            }

            reserved[best] = true;
            freeBerths--;
            entry.Berth = best;
            entry.Distance = lengths[best];
            entry.State = HarbourState.Arriving;
            return true;
        }

        // True when a ship other than the given one is queued: a free berth is then theirs, in order.
        private bool HasWaiting(Entry except)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != except && entries[i].State == HarbourState.Waiting)
                {
                    return true;
                }
            }

            return false;
        }

        private void AdmitQueue()
        {
            for (int i = 0; i < entries.Count && freeBerths > 0; i++)
            {
                Entry entry = entries[i];
                if (entry.State == HarbourState.Waiting && TryEnter(entry))
                {
                    Entered?.Invoke(entry.Ship);
                }
            }
        }
    }
}
