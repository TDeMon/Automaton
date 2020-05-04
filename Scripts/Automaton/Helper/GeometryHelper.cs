using AtomicTorch.CBND.CoreMod.Systems.Physics;
using AtomicTorch.CBND.GameApi.Data.Physics;
using AtomicTorch.CBND.GameApi.Scripting;
using AtomicTorch.GameEngine.Common.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryoFall.Automaton.Scripts.Automaton.Helper
{
    class GeometryHelper
    {
        public static Vector2D GetCenterPosition(IPhysicsBody target)
        {
            var shape = target.Shapes.FirstOrDefault(s => s.CollisionGroup == CollisionGroups.HitboxMelee);
            if (shape == null)
            {
                Api.Logger.Error("Automaton: target object has no HitBoxMelee shape " + target);
                return target.Position;
            }
            return ShapeCenter(shape) + target.Position;
        }

        private static Vector2D ShapeCenter(IPhysicsShape shape)
        {
            if (shape != null)
            {
                switch (shape.ShapeType)
                {
                    case ShapeType.Rectangle:
                        var shapeRectangle = (RectangleShape)shape;
                        return shapeRectangle.Position + shapeRectangle.Size / 2d;
                    case ShapeType.Point:
                        var shapePoint = (PointShape)shape;
                        return shapePoint.Point;
                    case ShapeType.Circle:
                        var shapeCircle = (CircleShape)shape;
                        return shapeCircle.Center;
                    case ShapeType.Line:
                        break;
                    case ShapeType.LineSegment:
                        var lineSegmentShape = (LineSegmentShape)shape;
                        return new Vector2D((lineSegmentShape.Point1.X + lineSegmentShape.Point2.X) / 2d,
                                     (lineSegmentShape.Point1.Y + lineSegmentShape.Point2.Y) / 2d);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return new Vector2D(0, 0);
        }
    }
}
