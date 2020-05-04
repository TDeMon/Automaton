namespace CryoFall.Automaton.Scripts.Automaton.Logic
{
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.GameEngine.Common.Primitives;
    using System.Linq;

    internal interface IMovementPathCalculator
    {
        IMovementPath GetPath(Vector2D startPos, IWorldObject targetObject);
    }

    class MovementPathCalculator : IMovementPathCalculator
    {
        public IMovementPath GetPath(Vector2D startPos, IWorldObject targetObject)
        {
            var shape = targetObject.PhysicsBody?.Shapes.FirstOrDefault(s => s.CollisionGroup == CollisionGroups.HitboxMelee);
            Vector2D targetPos;
            if (shape == null)
            {
                Api.Logger.Error("Automaton: target object has no HitBoxMelee shape " + targetObject);
                return new StraightMovementPath(startPos, startPos);
            }
            else
            {
                targetPos = targetObject.PhysicsBody.Position;
            }
            return new StraightMovementPath(startPos, targetPos);
        }
    }
}