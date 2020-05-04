namespace CryoFall.Automaton.Scripts.Automaton.Logic
{
    using AtomicTorch.GameEngine.Common.Primitives;

    internal interface IMovementPath
    {
        Vector2D StartPoint { get; }

        Vector2D EndpointPoint { get; }
    }

    class StraightMovementPath : IMovementPath
    {
        public Vector2D StartPoint { get; private set; }

        public Vector2D EndpointPoint { get; private set; }

        public StraightMovementPath(Vector2D startPoint, Vector2D endpointPoint)
        {
            StartPoint = startPoint;
            EndpointPoint = endpointPoint;
        }

    }
}