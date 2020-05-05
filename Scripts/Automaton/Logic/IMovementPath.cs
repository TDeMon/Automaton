namespace CryoFall.Automaton.Scripts.Automaton.Logic
{
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.GameEngine.Common.Primitives;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal interface IMovementPath // implements java.util.Iterator
    {
        Vector2D StartPoint { get; }

        Vector2D EndpointPoint { get; }

        IWorldObject? Target { get; } 

        // Should not return StartPoint.
        Vector2D NextPoint { get; }

        // For debug purposes
        List<Vector2D> Points { get; }

        // Returns next point or EndpointPoint.
        bool HasNext { get; }

        double Length { get; }
    }

    class StraightMovementPath : IMovementPath
    {
        private bool EndpointNotAccessed = true;

        public Vector2D StartPoint { get; private set; }

        public Vector2D EndpointPoint { get; private set; }

        public IWorldObject Target { get; private set; }

        public Vector2D NextPoint => EndpointPoint;

        public List<Vector2D> Points
        {
            get
            {
                List<Vector2D> pts = new List<Vector2D>(2);
                pts.Add(StartPoint);
                pts.Add(EndpointPoint);
                return pts;
            }
        }

        public bool HasNext
        {
            get
            {
                if (EndpointNotAccessed)
                {
                    EndpointNotAccessed = false;
                    return true;
                }
                return false;
            }
        }

        public double Length => StartPoint.DistanceTo(EndpointPoint);

        public StraightMovementPath(Vector2D startPoint, Vector2D endpointPoint, IWorldObject target)
        {
            StartPoint = startPoint;
            EndpointPoint = endpointPoint;
            Target = target;
        }
    }

    class WaypointMovementPath : IMovementPath
    {
        private int currentPointIndex = 0;
        private List<Vector2D> points;

        public Vector2D StartPoint => points[0];

        public Vector2D EndpointPoint => points[points.Count - 1];

        public IWorldObject Target { get; private set; }

        public List<Vector2D> Points => points;

        public Vector2D NextPoint
        {
            get
            {
                if (!HasNext)
                {
                    return EndpointPoint;
                }

                return points[++currentPointIndex];
            }
        }

        public bool HasNext => currentPointIndex < points.Count - 1;

        public double Length
        {
            get
            {
                double total = 0;

                for (int i = 0; i < points.Count - 2; i++)
                {
                    total += points[i].DistanceTo(points[i + 1]);
                }

                return total;
            }
        }

        public WaypointMovementPath(List<Vector2D> points, IWorldObject target)
        {
            if (points == null || points.Count == 0) throw new ArgumentException($"Path should consist of at least 1 point. It has {points?.Count} points", "points");
            this.points = points;
            Target = target;
        }
    }

    class NoPathPath : IMovementPath
    {
        private ICharacter Player;

        public NoPathPath(ICharacter player)
        {
            Player = player;
        }

        public Vector2D StartPoint => Player.Position;

        public Vector2D EndpointPoint => Player.Position;

        public IWorldObject Target => Player;

        public Vector2D NextPoint => Player.Position;

        public List<Vector2D> Points => new List<Vector2D> (Enumerable.Repeat(Player.Position, 1));

        public bool HasNext => false;

        public double Length => 0;
    }
}