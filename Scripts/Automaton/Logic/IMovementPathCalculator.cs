namespace CryoFall.Automaton.Scripts.Automaton.Logic
{
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Doors;
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.CoreMod.Systems.WorldObjectOwners;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Physics;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Extensions;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.GameEngine.Common.Primitives;
    using CryoFall.Automaton.Scripts.Automaton.Helper;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal interface IMovementPathCalculator
    {
        IMovementPath GetPath(ICharacter startPos, IWorldObject targetObject);
    }

    class MovementPathCalculator : IMovementPathCalculator
    {
        public IMovementPath GetPath(ICharacter startPos, IWorldObject targetObject)
        {
            var shape = targetObject.PhysicsBody?.Shapes.FirstOrDefault(s => s.CollisionGroup == CollisionGroups.HitboxMelee);
            Vector2D targetPos;
            if (shape == null)
            {
                Api.Logger.Error("Automaton: target object has no HitBoxMelee shape " + targetObject);
                return new StraightMovementPath(startPos.Position, startPos.Position, targetObject);
            }
            else
            {
                targetPos = GeometryHelper.GetCenterPosition(targetObject.PhysicsBody);
            }
            return new StraightMovementPath(startPos.Position, targetPos, targetObject);
        }
    }

    // See http://theory.stanford.edu/~amitp/GameProgramming/AStarComparison.html
    class AStarPathCalculator : IMovementPathCalculator
    {
        private const double MAX_PATH_TILES = 25;

        public IMovementPath GetPath(ICharacter user, IWorldObject targetObject)
        {
            Tile playerTile = user.Tile;
            Vector2Ushort targetTilePos = targetObject.TilePosition;

            if (playerTile.Position == targetTilePos) // User activated the script while already standing near to the target
            {
                return new StraightMovementPath(user.Position, GeometryHelper.TileCenter(playerTile), targetObject);
            }

            SortedSet<PathCandidate> candidates = new SortedSet<PathCandidate>();
            HashSet<Tile> visited = new HashSet<Tile>();
            candidates.Add(PathCandidate.Init(playerTile, targetTilePos));

            // Api.Logger.Dev($"Automaton: finding path from {playerTile.Position} to {targetTilePos}");

            while (candidates.Count != 0)
            {
                PathCandidate withLeastWeight = candidates.Min();
                candidates.Remove(withLeastWeight);
                var undiscovered = new HashSet<Tile>(withLeastWeight.Neighbours).Except(visited);
                visited.Add(withLeastWeight.Head);

                foreach (Tile next in undiscovered)
                {
                    if (next.Position == targetTilePos)
                    {
                        return new WaypointMovementPath(withLeastWeight.Fork(next).AsTilesCenters(), targetObject);
                    }

                    if (CanTravel(user, withLeastWeight.Head, next) && withLeastWeight.Length < MAX_PATH_TILES)
                    {
                        candidates.Add(withLeastWeight.Fork(next));
                    }
                }
            }

            // Api.Logger.Error($"Automaton: failed to find proper path from {playerTile.Position} to {targetTilePos}.");
            return new NoPathPath(user);
        }

        class PathCandidate : IComparable<PathCandidate>
        {
            private Vector2Ushort target;
            private List<Tile> tiles = new List<Tile>();
            public double Length { get; private set; } // that's g(n)

            private PathCandidate() 
            { 

            }

            public static PathCandidate Init(Tile start, Vector2Ushort target)
            {
                PathCandidate newOne = new PathCandidate();
                newOne.tiles.Add(start);
                newOne.target = target;
                return newOne;
            }

            public PathCandidate Fork(Tile tile)
            {
                PathCandidate result = new PathCandidate();
                
                result.tiles.AddRange(tiles);
                result.tiles.Add(tile);

                result.Length = Length + Head.Position.TileDistanceTo(tile.Position);

                return result;
            }

            // that's h(n)
            public double GetDistanceToTargetTile()
            {
                if (tiles.Count == 0) return 0;

                return Head.Position.TileDistanceTo(target);
            }

            public IEnumerable<Tile> Neighbours => Head.EightNeighborTiles;

            public Tile Head => tiles[tiles.Count - 1];

            public List<Vector2D> AsTilesCenters()
            {
                return tiles.ConvertAll(tile => GeometryHelper.TileCenter(tile)).ToList();
            }

            public double Weight => Length + GetDistanceToTargetTile() * 1.5; // Simply adding two number resulted in a very long freeze. Idk why.

            public int CompareTo(PathCandidate other)
            {
                return (int)(Weight * 1000) - (int)(other.Weight * 1000);
            }
        }

        class PathComparer : IComparer<PathCandidate>
        {
            public int Compare(PathCandidate x, PathCandidate y)
            {
                return x.CompareTo(y);
            }
        }

        private bool CanTravel(ICharacter user, Tile from, Tile to)
        {
            if (to.ProtoTile.Kind == TileKind.Water)
            {
                return false;
            }

            if (from.Height != to.Height && (from.IsSlope || to.IsSlope))
            {
                return true;
            }

            using var collisions = user.PhysicsBody.PhysicsSpace.TestLine(
                fromPosition: GeometryHelper.TileCenter(from),
                toPosition: GeometryHelper.TileCenter(to),
                collisionGroup: CollisionGroups.HitboxMelee,
                sendDebugEvent: false
                );

            
            return collisions.AsList()
                //.Where(test => !(test.PhysicsBody.AssociatedWorldObject is IProtoObjectDoor door && door.GetPrivateState(door as IStaticWorldObject).Owners.Contains(user.Name)))
                .Where(test => test.PhysicsBody.AssociatedWorldObject is IStaticWorldObject || 
                               test.PhysicsBody.AssociatedProtoTile != null )
                .Count() == 0;
        }
    }

}